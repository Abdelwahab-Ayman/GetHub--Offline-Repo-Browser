using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // Paints the commit history as GitHub-style rows with a lane graph on the left.
    // Rows are drawn manually so the graph stays smooth while scrolling and the
    // whole view follows the active dark/light palette.
    public class CommitGraphView : ScrollableControl
    {
        public const int RowHeight = 48;

        private const int LaneWidth = 14;
        private const int GraphPadding = 18;
        private const int TopPadding = 12;
        private const int ShaWidth = 64;
        private const int ShaHeight = 20;
        private const int RightPadding = 16;

        private readonly List<Row> _rows = new List<Row>();
        private readonly Font _shaFont;
        private List<CommitInfo> _commits = new List<CommitInfo>();
        private string _statusTitle = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _showStatus = true;
        private int _graphWidth = 56;
        private int _hoverIndex = -1;
        private int _selectedIndex = -1;

        public event EventHandler CommitActivated;
        public event EventHandler SelectionChanged;

        public CommitGraphView()
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
            Cursor = Cursors.Default;
            AccessibleName = "Commit history";
            AccessibleRole = AccessibleRole.List;
            _shaFont = new Font("Consolas", 8F, FontStyle.Regular, GraphicsUnit.Point);
        }

        public int CommitCount
        {
            get { return _rows.Count; }
        }

        public CommitInfo SelectedCommit
        {
            get
            {
                if (_selectedIndex < 0 || _selectedIndex >= _rows.Count)
                {
                    return null;
                }

                return _rows[_selectedIndex].Commit;
            }
        }

        public void SetCommits(List<CommitInfo> commits)
        {
            _commits = commits == null ? new List<CommitInfo>() : commits;
            _hoverIndex = -1;
            _selectedIndex = -1;
            AutoScrollPosition = Point.Empty;
            BuildRows();
            _showStatus = _rows.Count == 0;
            if (_showStatus)
            {
                _statusTitle = "No commits found";
                _statusMessage = "This branch has no commits, or the history could not be read.";
            }

            AutoScrollMinSize = _rows.Count == 0
                ? Size.Empty
                : new Size(0, (TopPadding * 2) + (_rows.Count * RowHeight));
            Invalidate();
            RaiseSelectionChanged();
        }

        public void SetStatus(string title, string message)
        {
            _commits = new List<CommitInfo>();
            _rows.Clear();
            _hoverIndex = -1;
            _selectedIndex = -1;
            _statusTitle = title == null ? string.Empty : title;
            _statusMessage = message == null ? string.Empty : message;
            _showStatus = true;
            AutoScrollMinSize = Size.Empty;
            AutoScrollPosition = Point.Empty;
            Invalidate();
            RaiseSelectionChanged();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            graphics.Clear(UiTheme.Canvas);

            if (_showStatus || _rows.Count == 0)
            {
                PaintStatus(graphics);
                return;
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int offset = AutoScrollPosition.Y;
            int first = Math.Max(0, (-offset - TopPadding) / RowHeight);
            int last = Math.Min(_rows.Count - 1, first + (ClientSize.Height / RowHeight) + 1);

            for (int i = first; i <= last; i++)
            {
                PaintRow(graphics, _rows[i], i, TopPadding + (i * RowHeight) + offset);
            }
        }

        protected override void OnScroll(ScrollEventArgs se)
        {
            base.OnScroll(se);
            Invalidate();
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
            if (index >= 0)
            {
                Focus();
                SelectIndex(index);
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = HitTest(e.Y);
            if (index < 0)
            {
                return;
            }

            _selectedIndex = index;
            Invalidate();
            RaiseSelectionChanged();
            RaiseActivated();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down ||
                keyData == Keys.PageUp || keyData == Keys.PageDown ||
                keyData == Keys.Home || keyData == Keys.End ||
                keyData == Keys.Enter)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_rows.Count == 0)
            {
                return;
            }

            int page = Math.Max(1, (ClientSize.Height / RowHeight) - 2);
            int selected = _selectedIndex;
            if (e.KeyCode == Keys.Up)
            {
                selected = selected <= 0 ? 0 : selected - 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Down)
            {
                selected = selected < 0 ? 0 : Math.Min(_rows.Count - 1, selected + 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                selected = Math.Max(0, (selected < 0 ? 0 : selected) - page);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                selected = Math.Min(_rows.Count - 1, (selected < 0 ? 0 : selected) + page);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                selected = 0;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                selected = _rows.Count - 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                RaiseActivated();
                e.Handled = true;
            }

            if (selected != _selectedIndex && selected >= 0 && selected < _rows.Count)
            {
                _selectedIndex = selected;
                EnsureSelectionVisible();
                Invalidate();
                RaiseSelectionChanged();
            }
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

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shaFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private void RaiseActivated()
        {
            CommitInfo commit = SelectedCommit;
            if (commit == null)
            {
                return;
            }

            EventHandler handler = CommitActivated;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void SelectIndex(int index)
        {
            if (index < 0 || index >= _rows.Count || index == _selectedIndex)
            {
                return;
            }

            _selectedIndex = index;
            Invalidate();
            RaiseSelectionChanged();
        }

        private void RaiseSelectionChanged()
        {
            EventHandler handler = SelectionChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void EnsureSelectionVisible()
        {
            if (_selectedIndex < 0)
            {
                return;
            }

            int top = TopPadding + (_selectedIndex * RowHeight);
            int viewTop = -AutoScrollPosition.Y;
            int viewBottom = viewTop + ClientSize.Height;
            if (top < viewTop)
            {
                AutoScrollPosition = new Point(0, top);
            }
            else if (top + RowHeight > viewBottom)
            {
                AutoScrollPosition = new Point(0, (top + RowHeight) - ClientSize.Height);
            }
        }

        private int HitTest(int y)
        {
            if (_rows.Count == 0)
            {
                return -1;
            }

            int contentY = y - AutoScrollPosition.Y - TopPadding;
            if (contentY < 0)
            {
                return -1;
            }

            int index = contentY / RowHeight;
            return index >= 0 && index < _rows.Count ? index : -1;
        }

        private void PaintRow(Graphics graphics, Row row, int index, int top)
        {
            int mid = top + (RowHeight / 2);
            int bottom = top + RowHeight;

            if (index == _selectedIndex)
            {
                using (SolidBrush select = new SolidBrush(UiTheme.Selection))
                {
                    graphics.FillRectangle(select, 0, top, ClientSize.Width, RowHeight);
                }
            }
            else if (index == _hoverIndex)
            {
                using (SolidBrush hover = new SolidBrush(UiTheme.HoverBackground))
                {
                    graphics.FillRectangle(hover, 0, top, ClientSize.Width, RowHeight);
                }
            }

            using (Pen divider = new Pen(UiTheme.HoverBackground))
            {
                graphics.DrawLine(divider, 0, bottom - 1, ClientSize.Width, bottom - 1);
            }

            int lanes = Math.Max(row.Before.Length, row.After.Length);
            for (int i = 0; i < lanes; i++)
            {
                float x = GraphPadding + (i * LaneWidth);
                Color laneColor = LaneColor(i);
                bool beforeActive = i < row.Before.Length && !string.IsNullOrEmpty(row.Before[i]);
                bool afterActive = i < row.After.Length && !string.IsNullOrEmpty(row.After[i]);
                bool newLane = !beforeActive;

                using (Pen pen = new Pen(laneColor, 2F))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    if (beforeActive)
                    {
                        graphics.DrawLine(pen, x, top, x, mid);
                    }

                    // Newly created lanes are reached with a diagonal from the commit dot,
                    // so only the first-parent lane (the dot lane) continues straight down.
                    if (afterActive && (!newLane || i == row.DotLane))
                    {
                        graphics.DrawLine(pen, x, mid, x, bottom);
                    }
                }
            }

            float dotX = GraphPadding + (row.DotLane * LaneWidth);
            foreach (int lane in row.ParentLanes)
            {
                if (lane == row.DotLane)
                {
                    continue;
                }

                float laneX = GraphPadding + (lane * LaneWidth);
                using (Pen edge = new Pen(LaneColor(lane), 2F))
                {
                    edge.StartCap = LineCap.Round;
                    edge.EndCap = LineCap.Round;
                    graphics.DrawBezier(edge, dotX, mid, dotX, mid + 9, laneX, mid + 5, laneX, bottom);
                }
            }

            using (SolidBrush dot = new SolidBrush(LaneColor(row.DotLane)))
            {
                graphics.FillEllipse(dot, dotX - 4F, mid - 4F, 8F, 8F);
            }

            int textLeft = _graphWidth + 6;
            int textRight = ClientSize.Width - RightPadding - ShaWidth - 10;
            int textWidth = textRight - textLeft;
            if (textWidth < 24)
            {
                textWidth = 24;
            }

            TextRenderer.DrawText(
                graphics,
                row.Commit.Subject,
                UiTheme.BodyBold,
                new Rectangle(textLeft, top + 7, textWidth, 20),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

            string meta = row.Commit.AuthorDisplay + "   \u00b7   " +
                Display.RelativeTime(row.Commit.AuthorDateUtc);
            TextRenderer.DrawText(
                graphics,
                meta,
                UiTheme.SmallFont,
                new Rectangle(textLeft, top + 27, textWidth, 16),
                UiTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

            Rectangle pill = new Rectangle(
                ClientSize.Width - RightPadding - ShaWidth,
                top + 14,
                ShaWidth,
                ShaHeight);
            using (GraphicsPath path = RoundedRectangle(pill, 9))
            {
                using (SolidBrush fill = new SolidBrush(UiTheme.Surface))
                {
                    graphics.FillPath(fill, path);
                }

                using (Pen border = new Pen(UiTheme.Border))
                {
                    graphics.DrawPath(border, path);
                }
            }

            TextRenderer.DrawText(
                graphics,
                row.Commit.ShortSha,
                _shaFont,
                pill,
                UiTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

            if (index == _selectedIndex && Focused)
            {
                ControlPaint.DrawFocusRectangle(
                    graphics,
                    new Rectangle(2, top + 2, ClientSize.Width - 6, RowHeight - 5));
            }
        }

        private void PaintStatus(Graphics graphics)
        {
            int width = ClientSize.Width;
            if (width <= 0)
            {
                return;
            }

            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int textWidth = Math.Min(520, Math.Max(180, width - 80));
            int center = width / 2;

            Size titleSize = TextRenderer.MeasureText(
                _statusTitle,
                UiTheme.BodyBold,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            Size messageSize = TextRenderer.MeasureText(
                _statusMessage,
                UiTheme.SmallFont,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            int total = 64 + 16 + titleSize.Height + 8 + messageSize.Height;
            int top = (ClientSize.Height - total) / 2;
            if (top < 24)
            {
                top = 24;
            }

            RepoIcons.Draw(
                graphics,
                RepoTabStrip.HistoryIndex,
                new Rectangle(center - 32, top, 64, 64),
                UiTheme.Muted,
                1.15F);

            int titleTop = top + 80;
            TextRenderer.DrawText(
                graphics,
                _statusTitle,
                UiTheme.BodyBold,
                new Rectangle(center - (textWidth / 2), titleTop, textWidth, titleSize.Height + 2),
                UiTheme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                _statusMessage,
                UiTheme.SmallFont,
                new Rectangle(
                    center - (textWidth / 2),
                    titleTop + titleSize.Height + 8,
                    textWidth,
                    messageSize.Height + 2),
                UiTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
        }

        private void BuildRows()
        {
            _rows.Clear();
            List<string> lanes = new List<string>();
            int maxLanes = 1;

            foreach (CommitInfo commit in _commits)
            {
                string sha = commit.Sha == null ? string.Empty : commit.Sha;
                Row row = new Row();
                row.Commit = commit;
                row.Before = lanes.ToArray();

                int dot = IndexOf(lanes, sha);
                if (dot < 0)
                {
                    dot = FreeLane(lanes);
                    lanes[dot] = sha;
                }

                row.DotLane = dot;

                // The commit has landed, so its lane is free again for the first parent.
                lanes[dot] = null;
                row.ParentLanes = new List<int>();

                for (int i = 0; i < commit.Parents.Count; i++)
                {
                    string parent = commit.Parents[i];
                    if (string.IsNullOrEmpty(parent))
                    {
                        continue;
                    }

                    int lane = IndexOf(lanes, parent);
                    if (lane < 0)
                    {
                        lane = i == 0 ? dot : FreeLane(lanes);
                        lanes[lane] = parent;
                    }

                    row.ParentLanes.Add(lane);
                    if (lane + 1 > maxLanes)
                    {
                        maxLanes = lane + 1;
                    }
                }

                TrimLanes(lanes);
                row.After = lanes.ToArray();
                if (row.After.Length > maxLanes)
                {
                    maxLanes = row.After.Length;
                }

                if (row.DotLane + 1 > maxLanes)
                {
                    maxLanes = row.DotLane + 1;
                }

                _rows.Add(row);
            }

            _graphWidth = Math.Min(200, Math.Max(56, GraphPadding + (maxLanes * LaneWidth) + 10));
        }

        private static int IndexOf(List<string> lanes, string sha)
        {
            if (string.IsNullOrEmpty(sha))
            {
                return -1;
            }

            for (int i = 0; i < lanes.Count; i++)
            {
                if (string.Equals(lanes[i], sha, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FreeLane(List<string> lanes)
        {
            for (int i = 0; i < lanes.Count; i++)
            {
                if (string.IsNullOrEmpty(lanes[i]))
                {
                    return i;
                }
            }

            lanes.Add(null);
            return lanes.Count - 1;
        }

        private static void TrimLanes(List<string> lanes)
        {
            for (int i = lanes.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(lanes[i]))
                {
                    return;
                }

                lanes.RemoveAt(i);
            }
        }

        private static Color LaneColor(int lane)
        {
            if (lane < 0)
            {
                lane = 0;
            }

            if (UiTheme.IsLight)
            {
                switch (lane % 6)
                {
                    case 0:
                        return ColorTranslator.FromHtml("#0969da");
                    case 1:
                        return ColorTranslator.FromHtml("#8250df");
                    case 2:
                        return ColorTranslator.FromHtml("#1a7f37");
                    case 3:
                        return ColorTranslator.FromHtml("#bc4c00");
                    case 4:
                        return ColorTranslator.FromHtml("#bf3989");
                    default:
                        return ColorTranslator.FromHtml("#0550ae");
                }
            }

            switch (lane % 6)
            {
                case 0:
                    return ColorTranslator.FromHtml("#2f81f7");
                case 1:
                    return ColorTranslator.FromHtml("#a371f7");
                case 2:
                    return ColorTranslator.FromHtml("#3fb950");
                case 3:
                    return ColorTranslator.FromHtml("#db6d28");
                case 4:
                    return ColorTranslator.FromHtml("#f778ba");
                default:
                    return ColorTranslator.FromHtml("#79c0ff");
            }
        }

        private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class Row
        {
            public CommitInfo Commit;
            public int DotLane;
            public string[] Before;
            public string[] After;
            public List<int> ParentLanes;
        }
    }
}
