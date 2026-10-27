using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // The "Files" section of the History tab: a sticky header and one row per
    // changed file with its status letter and line counts. Clicking a row asks the
    // host to open the Diff section at that file.
    public class CommitFilesView : ScrollableControl
    {
        public const int HeaderHeight = 34;
        public const int RowHeight = 40;

        private readonly List<CommitFile> _files = new List<CommitFile>();
        private string _statusTitle = string.Empty;
        private string _statusMessage = string.Empty;
        private int _hoverIndex = -1;
        private int _selectedIndex = -1;

        public event EventHandler FileActivated;

        public CommitFilesView()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);
            BackColor = UiTheme.Canvas;
            AutoScroll = true;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleName = "Changed files";
            AccessibleRole = AccessibleRole.List;
            SetStatus("Files", "Select a commit in the graph to list its changed files.");
        }

        public int FileCount
        {
            get { return _files.Count; }
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
        }

        public CommitFile SelectedFile
        {
            get
            {
                if (_selectedIndex < 0 || _selectedIndex >= _files.Count)
                {
                    return null;
                }

                return _files[_selectedIndex];
            }
        }

        public void SetFiles(CommitDetail detail)
        {
            _files.Clear();
            if (detail != null && detail.Files != null)
            {
                _files.AddRange(detail.Files);
            }

            _statusTitle = string.Empty;
            _statusMessage = string.Empty;
            _hoverIndex = -1;
            _selectedIndex = -1;
            AutoScrollPosition = Point.Empty;

            if (_files.Count == 0)
            {
                AutoScrollMinSize = Size.Empty;
                SetStatus("No file changes", "This commit does not change any files.");
                return;
            }

            UpdateScrollExtent();
            Invalidate();
        }

        public void SetStatus(string title, string message)
        {
            _files.Clear();
            _statusTitle = title == null ? string.Empty : title;
            _statusMessage = message == null ? string.Empty : message;
            _hoverIndex = -1;
            _selectedIndex = -1;
            AutoScrollMinSize = Size.Empty;
            AutoScrollPosition = Point.Empty;
            Invalidate();
        }

        public void ClearSelection()
        {
            if (_selectedIndex != -1)
            {
                _selectedIndex = -1;
                Invalidate();
            }
        }

        public bool SelectPath(string path)
        {
            for (int i = 0; i < _files.Count; i++)
            {
                if (string.Equals(_files[i].Path, path, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedIndex = i;
                    EnsureSelectionVisible();
                    Invalidate();
                    return true;
                }
            }

            return false;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollExtent();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.Clear(UiTheme.Canvas);

            if (_files.Count == 0)
            {
                CommitStatusPainter.Paint(graphics, ClientSize, _statusTitle, _statusMessage);
                return;
            }

            int width = ClientSize.Width;
            int offset = AutoScrollPosition.Y;

            // Rows scroll underneath the header, which stays pinned to the top.
            Region oldClip = graphics.Clip;
            graphics.SetClip(new Rectangle(0, HeaderHeight, width, Math.Max(0, ClientSize.Height - HeaderHeight)));
            int first = Math.Max(0, (-offset - HeaderHeight) / RowHeight);
            int last = Math.Min(_files.Count - 1, first + (ClientSize.Height / RowHeight) + 2);
            for (int i = first; i <= last; i++)
            {
                PaintRow(graphics, _files[i], i, HeaderHeight + (i * RowHeight) + offset, width);
            }

            graphics.Clip = oldClip;
            PaintHeader(graphics, width);
        }

        private void PaintHeader(Graphics graphics, int width)
        {
            using (SolidBrush fill = new SolidBrush(UiTheme.Surface))
            {
                graphics.FillRectangle(fill, 0, 0, width, HeaderHeight);
            }

            int additions = 0;
            int deletions = 0;
            for (int i = 0; i < _files.Count; i++)
            {
                additions += _files[i].Additions;
                deletions += _files[i].Deletions;
            }

            string label = _files.Count == 1 ? "1 file changed" : _files.Count + " files changed";
            TextRenderer.DrawText(
                graphics,
                label,
                UiTheme.BodyBold,
                new Rectangle(18, 0, Math.Max(40, width - 190), HeaderHeight),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            int right = width - 18;
            right = DrawCount(graphics, right, "+" + additions, UiTheme.Green);
            right = DrawCount(graphics, right - 12, "\u2212" + deletions, UiTheme.Red);

            using (Pen border = new Pen(UiTheme.Border))
            {
                graphics.DrawLine(border, 0, HeaderHeight - 1, width, HeaderHeight - 1);
            }
        }

        private static int DrawCount(Graphics graphics, int right, string text, Color color)
        {
            Size size = TextRenderer.MeasureText(
                graphics,
                text,
                UiTheme.BodyBold,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(
                graphics,
                text,
                UiTheme.BodyBold,
                new Rectangle(right - size.Width - 2, 0, size.Width + 2, HeaderHeight),
                color,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
            return right - size.Width - 2;
        }

        private void PaintRow(Graphics graphics, CommitFile file, int index, int top, int width)
        {
            Rectangle bounds = new Rectangle(0, top, width, RowHeight);
            Rectangle inner = new Rectangle(1, top, width - 2, RowHeight - 1);

            if (index == _selectedIndex)
            {
                using (SolidBrush selection = new SolidBrush(UiTheme.Selection))
                {
                    graphics.FillRectangle(selection, inner);
                }
            }
            else if (index == _hoverIndex)
            {
                using (SolidBrush hover = new SolidBrush(UiTheme.HoverBackground))
                {
                    graphics.FillRectangle(hover, inner);
                }
            }

            using (Pen border = new Pen(UiTheme.Border))
            {
                graphics.DrawLine(border, 18, bounds.Bottom - 1, width - 18, bounds.Bottom - 1);
            }

            // Status badge.
            Rectangle badge = new Rectangle(18, top + 11, 18, 18);
            Color badgeColor = StatusColor(file.Status);
            using (SolidBrush fill = new SolidBrush(badgeColor))
            {
                graphics.FillRectangle(fill, badge);
            }

            TextRenderer.DrawText(
                graphics,
                file.StatusLetter,
                UiTheme.SmallFont,
                badge,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            int countsWidth = 132;
            int pathLeft = badge.Right + 10;
            int pathWidth = Math.Max(40, width - pathLeft - countsWidth - 18);
            string path = file.DisplayPath;
            TextRenderer.DrawText(
                graphics,
                path,
                UiTheme.BodyFont,
                new Rectangle(pathLeft, top, pathWidth, RowHeight),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding | TextFormatFlags.PathEllipsis);

            int right = width - 18;
            Size deletionsSize = TextRenderer.MeasureText(
                graphics,
                "\u2212" + file.Deletions,
                UiTheme.BodyFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(
                graphics,
                "\u2212" + file.Deletions,
                UiTheme.BodyFont,
                new Rectangle(right - deletionsSize.Width - 2, top, deletionsSize.Width + 2, RowHeight),
                file.Deletions > 0 ? UiTheme.Red : UiTheme.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

            int additionsRight = right - deletionsSize.Width - 12;
            Size additionsSize = TextRenderer.MeasureText(
                graphics,
                "+" + file.Additions,
                UiTheme.BodyFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(
                graphics,
                "+" + file.Additions,
                UiTheme.BodyFont,
                new Rectangle(additionsRight - additionsSize.Width - 2, top, additionsSize.Width + 2, RowHeight),
                file.Additions > 0 ? UiTheme.Green : UiTheme.Faint,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
        }

        private static Color StatusColor(string status)
        {
            switch ((status == null ? string.Empty : status).ToLowerInvariant())
            {
                case "added":
                    return ColorTranslator.FromHtml("#238636");
                case "removed":
                case "deleted":
                    return ColorTranslator.FromHtml("#da3633");
                case "renamed":
                case "copied":
                    return ColorTranslator.FromHtml("#1f6feb");
                default:
                    return ColorTranslator.FromHtml("#9e6a03");
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = HitTest(e.Y);
            if (index != _hoverIndex)
            {
                _hoverIndex = index;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = HitTest(e.Y);
            if (index < 0)
            {
                return;
            }

            Focus();
            _selectedIndex = index;
            Invalidate();
            RaiseFileActivated();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down ||
                keyData == Keys.Home || keyData == Keys.End ||
                keyData == Keys.PageUp || keyData == Keys.PageDown)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_files.Count == 0)
            {
                return;
            }

            int selected = _selectedIndex < 0 ? 0 : _selectedIndex;
            if (e.KeyCode == Keys.Up)
            {
                selected--;
            }
            else if (e.KeyCode == Keys.Down)
            {
                selected++;
            }
            else if (e.KeyCode == Keys.Home)
            {
                selected = 0;
            }
            else if (e.KeyCode == Keys.End)
            {
                selected = _files.Count - 1;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                selected -= Math.Max(1, (ClientSize.Height / RowHeight) - 1);
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                selected += Math.Max(1, (ClientSize.Height / RowHeight) - 1);
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                if (_selectedIndex >= 0)
                {
                    RaiseFileActivated();
                    e.Handled = true;
                }

                return;
            }
            else
            {
                return;
            }

            selected = Math.Max(0, Math.Min(_files.Count - 1, selected));
            if (selected != _selectedIndex)
            {
                _selectedIndex = selected;
                EnsureSelectionVisible();
                Invalidate();
            }

            e.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        private void RaiseFileActivated()
        {
            EventHandler handler = FileActivated;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private int HitTest(int y)
        {
            int contentY = y - HeaderHeight - AutoScrollPosition.Y;
            if (contentY < 0)
            {
                return -1;
            }

            int index = contentY / RowHeight;
            return index >= 0 && index < _files.Count ? index : -1;
        }

        private void EnsureSelectionVisible()
        {
            if (_selectedIndex < 0)
            {
                return;
            }

            int top = HeaderHeight + (_selectedIndex * RowHeight);
            int viewTop = -AutoScrollPosition.Y + HeaderHeight;
            int viewBottom = -AutoScrollPosition.Y + ClientSize.Height;
            if (top < viewTop)
            {
                AutoScrollPosition = new Point(0, top - HeaderHeight);
            }
            else if (top + RowHeight > viewBottom)
            {
                AutoScrollPosition = new Point(0, (top + RowHeight) - ClientSize.Height);
            }
        }

        private void UpdateScrollExtent()
        {
            if (_files.Count == 0)
            {
                AutoScrollMinSize = Size.Empty;
                return;
            }

            int height = HeaderHeight + (_files.Count * RowHeight) + 4;
            if (AutoScrollMinSize.Height != height)
            {
                AutoScrollMinSize = new Size(0, height);
            }
        }
    }
}
