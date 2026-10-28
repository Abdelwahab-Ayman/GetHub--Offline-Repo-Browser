using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // The "Diff" section of the History tab: every changed file rendered as a
    // unified diff with old/new line gutters. Supports both horizontal and vertical
    // scrolling and a row cap so very large commits stay responsive.
    public class CommitDiffView : ScrollableControl
    {
        private const int LineHeight = 18;
        private const int FileHeaderHeight = 28;
        private const int GutterWidth = 42;
        private const int TextLeft = 96;
        private const int SplitDivider = 320;
        private const int MaxRows = 20000;

        private static readonly Regex HunkPattern = new Regex(
            "^@@ -(\\d+)(?:,(\\d+))? \\+(\\d+)(?:,(\\d+))? @@(.*)$",
            RegexOptions.Compiled);

        private static readonly Color AdditionDark = ColorTranslator.FromHtml("#12261e");
        private static readonly Color AdditionLight = ColorTranslator.FromHtml("#e6ffec");
        private static readonly Color DeletionDark = ColorTranslator.FromHtml("#2d1214");
        private static readonly Color DeletionLight = ColorTranslator.FromHtml("#ffebe9");

        private enum RowKind
        {
            File,
            Hunk,
            Split,
            Note,
            Context,
            Addition,
            Deletion,
            Blank
        }

        private sealed class Row
        {
            public RowKind Kind;
            public string Text;
            public int OldLine;
            public int NewLine;
            public int FileIndex;
            public bool TruncatedRow;
            public string LeftText;
            public string RightText;
            public RowKind LeftKind;
            public RowKind RightKind;
        }

        private readonly Font _monoFont;
        private readonly List<Row> _rows = new List<Row>();
        private readonly List<int> _rowTop = new List<int>();
        private readonly List<int> _fileRow = new List<int>();
        private int _totalHeight;
        private int _contentWidth;
        private int _selectedFileIndex = -1;
        private bool _truncated;
        private string _statusTitle = string.Empty;
        private string _statusMessage = string.Empty;

        public CommitDiffView()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = UiTheme.Canvas;
            AutoScroll = true;
            AccessibleName = "Commit diff";
            AccessibleRole = AccessibleRole.Document;
            _monoFont = new Font("Consolas", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            SetStatus("Diff", "Select a commit in the graph to read its diff.");
        }

        public int RowCount
        {
            get { return _rows.Count; }
        }

        public bool IsTruncated
        {
            get { return _truncated; }
        }

        public int SelectedFileIndex
        {
            get { return _selectedFileIndex; }
        }

        public void SetDetail(CommitDetail detail)
        {
            _rows.Clear();
            _fileRow.Clear();
            _selectedFileIndex = -1;
            _truncated = false;
            _statusTitle = string.Empty;
            _statusMessage = string.Empty;

            if (detail == null || detail.Files == null || detail.Files.Count == 0)
            {
                SetStatus("No diff", "This commit does not change any files.");
                return;
            }

            for (int i = 0; i < detail.Files.Count && _rows.Count < MaxRows; i++)
            {
                AppendFile(detail.Files[i], i);
            }

            if (_rows.Count >= MaxRows)
            {
                // AddRow refuses rows beyond the cap, so append the notice directly.
                _rows.Add(new Row
                {
                    Kind = RowKind.Note,
                    Text = "Diff truncated at " + MaxRows.ToString("N0") + " rows. Open this commit on GitHub for the full patch."
                });
            }
            _truncated = detail.Truncated;

            RebuildLayout();
            AutoScrollPosition = Point.Empty;
            Invalidate();
        }

        public void SetStatus(string title, string message)
        {
            _rows.Clear();
            _rowTop.Clear();
            _fileRow.Clear();
            _statusTitle = title == null ? string.Empty : title;
            _statusMessage = message == null ? string.Empty : message;
            _totalHeight = 0;
            _contentWidth = 0;
            _selectedFileIndex = -1;
            _truncated = false;
            AutoScrollMinSize = Size.Empty;
            AutoScrollPosition = Point.Empty;
            Invalidate();
        }

        // Scrolls the diff to the header of the given changed file.
        public void ScrollToFile(int fileIndex)
        {
            if (fileIndex < 0 || fileIndex >= _fileRow.Count)
            {
                return;
            }

            _selectedFileIndex = fileIndex;
            int row = _fileRow[fileIndex];
            int top = row >= 0 && row < _rowTop.Count ? _rowTop[row] : 0;
            int x = -AutoScrollPosition.X;
            AutoScrollPosition = new Point(x, top);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.Clear(UiTheme.Canvas);

            if (_rows.Count == 0)
            {
                CommitStatusPainter.Paint(graphics, ClientSize, _statusTitle, _statusMessage);
                return;
            }

            int offsetX = AutoScrollPosition.X;
            int offsetY = AutoScrollPosition.Y;
            int width = ClientSize.Width;

            int first = FirstVisibleRow(-offsetY);
            int y = _rowTop[first] + offsetY;
            for (int i = first; i < _rows.Count && y < ClientSize.Height; i++)
            {
                int height = _rows[i].Kind == RowKind.File ? FileHeaderHeight : LineHeight;
                PaintRow(graphics, _rows[i], y, offsetX, width, height);
                y += height;
            }
        }

        private void PaintRow(
            Graphics graphics,
            Row row,
            int top,
            int offsetX,
            int width,
            int height)
        {
            bool light = UiTheme.IsLight;
            int textX = TextLeft + offsetX;

            if (row.Kind == RowKind.File)
            {
                using (SolidBrush fill = new SolidBrush(UiTheme.Surface))
                {
                    graphics.FillRectangle(fill, 0, top, width, height);
                }

                using (Pen border = new Pen(UiTheme.Border))
                {
                    graphics.DrawLine(border, 0, top + height - 1, width, top + height - 1);
                }

                int badgeLeft = 18 + offsetX;
                Rectangle badge = new Rectangle(badgeLeft, top + 6, 16, 16);
                Color badgeColor = ColorTranslator.FromHtml("#9e6a03");
                string status = GetStatus(row.Text);
                if (status == "A")
                {
                    badgeColor = ColorTranslator.FromHtml("#238636");
                }
                else if (status == "D")
                {
                    badgeColor = ColorTranslator.FromHtml("#da3633");
                }
                else if (status == "R" || status == "C")
                {
                    badgeColor = ColorTranslator.FromHtml("#1f6feb");
                }

                using (SolidBrush fill = new SolidBrush(badgeColor))
                {
                    graphics.FillRectangle(fill, badge);
                }

                TextRenderer.DrawText(
                    graphics,
                    row.Text.Length > 0 ? row.Text.Substring(0, 1) : "M",
                    UiTheme.SmallFont,
                    badge,
                    Color.White,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                string path = row.Text.Length > 2 ? row.Text.Substring(2).Trim() : row.Text;
                TextRenderer.DrawText(
                    graphics,
                    path,
                    UiTheme.BodyBold,
                    new Rectangle(badge.Right + 8, top, Math.Max(40, width - badge.Right - 26), height),
                    UiTheme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding | TextFormatFlags.PathEllipsis);

                if (row.TruncatedRow)
                {
                    TextRenderer.DrawText(
                        graphics,
                        "truncated",
                        UiTheme.SmallFont,
                        new Rectangle(Math.Max(20, width - 76), top, 66, height),
                        UiTheme.Faint,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                        TextFormatFlags.NoPadding);
                }

                return;
            }

            Color background = Color.Empty;
            bool hasBackground = false;
            if (row.Kind == RowKind.Addition)
            {
                background = light ? AdditionLight : AdditionDark;
                hasBackground = true;
            }
            else if (row.Kind == RowKind.Deletion)
            {
                background = light ? DeletionLight : DeletionDark;
                hasBackground = true;
            }
            else if (row.Kind == RowKind.Hunk)
            {
                background = UiTheme.Surface;
                hasBackground = true;
            }

            if (hasBackground)
            {
                using (SolidBrush fill = new SolidBrush(background))
                {
                    graphics.FillRectangle(fill, 0, top, width, height);
                }
            }

            if (row.Kind == RowKind.Hunk || row.Kind == RowKind.Note)
            {
                TextRenderer.DrawText(
                    graphics,
                    row.Text,
                    UiTheme.SmallFont,
                    new Rectangle(18 + offsetX, top, Math.Max(40, width - 36), height),
                    row.Kind == RowKind.Note ? UiTheme.Muted : UiTheme.Accent,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding);
                return;
            }

            // Line number gutters.
            string oldText = row.OldLine > 0 ? row.OldLine.ToString() : string.Empty;
            string newText = row.NewLine > 0 ? row.NewLine.ToString() : string.Empty;
            TextRenderer.DrawText(
                graphics,
                oldText,
                _monoFont,
                new Rectangle(offsetX, top, GutterWidth - 6, height),
                UiTheme.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
            TextRenderer.DrawText(
                graphics,
                newText,
                _monoFont,
                new Rectangle(offsetX + GutterWidth, top, GutterWidth - 6, height),
                UiTheme.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

            using (Pen border = new Pen(UiTheme.Border))
            {
                graphics.DrawLine(border, offsetX + (GutterWidth * 2) + 2, top, offsetX + (GutterWidth * 2) + 2, top + height);
            }

            Color textColor = row.Kind == RowKind.Addition
                ? (light ? ColorTranslator.FromHtml("#116329") : ColorTranslator.FromHtml("#7ee787"))
                : row.Kind == RowKind.Deletion
                    ? (light ? ColorTranslator.FromHtml("#a40e26") : ColorTranslator.FromHtml("#ffa198"))
                    : UiTheme.Text;

            TextRenderer.DrawText(
                graphics,
                row.Text,
                _monoFont,
                new Rectangle(textX, top, Math.Max(40, _contentWidth - TextLeft), height),
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
        }

        private string GetStatus(string headerText)
        {
            if (string.IsNullOrEmpty(headerText))
            {
                return "M";
            }

            return headerText.Substring(0, 1);
        }

        private void AppendFile(CommitFile file, int fileIndex)
        {
            string display = file.StatusLetter + " " + file.DisplayPath;
            Row header = new Row
            {
                Kind = RowKind.File,
                Text = display,
                FileIndex = fileIndex,
                TruncatedRow = file.Truncated
            };
            _fileRow.Add(_rows.Count);
            AddRow(header);

            if (string.IsNullOrEmpty(file.Patch))
            {
                AddRow(new Row
                {
                    Kind = RowKind.Note,
                    Text = file.IsBinary
                        ? "Binary file \u2014 no textual diff to display."
                        : "No patch available for this file. GitHub omits patches for large or empty diffs."
                });
                return;
            }

            string[] lines = file.Patch.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            int oldLine = 0;
            int newLine = 0;
            bool inHunk = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("@@", StringComparison.Ordinal))
                {
                    Match match = HunkPattern.Match(line);
                    if (match.Success)
                    {
                        int.TryParse(match.Groups[1].Value, out oldLine);
                        int.TryParse(match.Groups[3].Value, out newLine);
                    }

                    inHunk = true;
                    AddRow(new Row { Kind = RowKind.Hunk, Text = line });
                    continue;
                }

                if (!inHunk)
                {
                    // GitHub patches start at the first hunk; anything before it is
                    // metadata we do not need to repeat.
                    continue;
                }

                if (line.Length == 0)
                {
                    // The final split after the patch's trailing newline.
                    continue;
                }

                char marker = line[0];
                if (marker == '+')
                {
                    AddRow(new Row
                    {
                        Kind = RowKind.Addition,
                        Text = line.Substring(1),
                        NewLine = newLine
                    });
                    newLine++;
                }
                else if (marker == '-')
                {
                    AddRow(new Row
                    {
                        Kind = RowKind.Deletion,
                        Text = line.Substring(1),
                        OldLine = oldLine
                    });
                    oldLine++;
                }
                else if (marker == '\\')
                {
                    AddRow(new Row { Kind = RowKind.Note, Text = line });
                }
                else
                {
                    AddRow(new Row
                    {
                        Kind = RowKind.Context,
                        Text = marker == ' ' ? line.Substring(1) : line,
                        OldLine = oldLine,
                        NewLine = newLine
                    });
                    oldLine++;
                    newLine++;
                }
            }
        }

        private void AddRow(Row row)
        {
            if (_rows.Count >= MaxRows)
            {
                return;
            }

            _rows.Add(row);
        }

        private void RebuildLayout()
        {
            _rowTop.Clear();
            int y = 0;
            int widest = 0;
            for (int i = 0; i < _rows.Count; i++)
            {
                _rowTop.Add(y);
                Row row = _rows[i];
                if (row.Kind == RowKind.File)
                {
                    y += FileHeaderHeight;
                    widest = Math.Max(widest, MeasureTextWidth(row.Text) + TextLeft + 40);
                }
                else
                {
                    int rowWidth = row.Kind == RowKind.Hunk || row.Kind == RowKind.Note
                        ? MeasureTextWidth(row.Text) + 40
                        : MeasureTextWidth(row.Text) + TextLeft + 24;
                    widest = Math.Max(widest, rowWidth);
                    y += LineHeight;
                }
            }

            _totalHeight = y + 8;
            _contentWidth = Math.Min(6000, Math.Max(ClientSize.Width, widest));
            AutoScrollMinSize = new Size(_contentWidth, _totalHeight);
        }

        private int MeasureTextWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            return TextRenderer.MeasureText(
                text,
                _monoFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
        }

        private int FirstVisibleRow(int top)
        {
            int low = 0;
            int high = _rowTop.Count - 1;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (_rowTop[middle] < top)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle;
                }
            }

            return Math.Max(0, low);
        }

    }
}
