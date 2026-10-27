using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // The secondary tab strip inside the History tab: Graph, Commit message,
    // Files and Diff. Text-only and owner-drawn so it follows the theme instantly.
    public class CommitSectionTabs : Control
    {
        public const int PreferredHeight = 40;
        public const int GraphIndex = 0;
        public const int MessageIndex = 1;
        public const int FilesIndex = 2;
        public const int DiffIndex = 3;

        private static readonly string[] Titles = new string[]
        {
            "Graph",
            "Commit message",
            "Files",
            "Diff"
        };

        private readonly int[] _tabLeft = new int[Titles.Length];
        private readonly int[] _tabWidth = new int[Titles.Length];
        private int _layoutWidth = -1;
        private int _hoverIndex = -1;
        private int _selectedIndex;

        public event EventHandler SelectedIndexChanged;

        public CommitSectionTabs()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);
            BackColor = UiTheme.Surface;
            Font = UiTheme.BodyFont;
            Height = PreferredHeight;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleName = "Commit sections";
            AccessibleRole = AccessibleRole.PageTabList;
        }

        public static int Count
        {
            get { return Titles.Length; }
        }

        public static string GetTitle(int index)
        {
            if (index < 0 || index >= Titles.Length)
            {
                return string.Empty;
            }

            return Titles[index];
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int index = Math.Max(0, Math.Min(Titles.Length - 1, value));
                if (_selectedIndex == index)
                {
                    return;
                }

                _selectedIndex = index;
                Invalidate();
                EventHandler handler = SelectedIndexChanged;
                if (handler != null)
                {
                    handler(this, EventArgs.Empty);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            EnsureLayout();

            Graphics graphics = e.Graphics;
            graphics.Clear(UiTheme.Surface);

            for (int i = 0; i < Titles.Length; i++)
            {
                int left = _tabLeft[i];
                int width = _tabWidth[i];
                bool selected = i == _selectedIndex;
                bool hovered = i == _hoverIndex;

                if (hovered && !selected)
                {
                    using (GraphicsPath path = RoundedRectangle(
                        new Rectangle(left + 2, 5, Math.Max(1, width - 4), Height - 8), 6))
                    using (SolidBrush fill = new SolidBrush(UiTheme.HoverBackground))
                    {
                        graphics.FillPath(fill, path);
                    }
                }

                TextRenderer.DrawText(
                    graphics,
                    Titles[i],
                    selected ? UiTheme.BodyBold : UiTheme.BodyFont,
                    new Rectangle(left, 0, width, Height - 2),
                    selected ? UiTheme.Text : UiTheme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding);

                if (selected)
                {
                    using (SolidBrush underline = new SolidBrush(UiTheme.Accent))
                    {
                        graphics.FillRectangle(underline, left + 10, Height - 2, Math.Max(1, width - 20), 2);
                    }
                }
            }

            using (Pen border = new Pen(UiTheme.Border))
            {
                graphics.DrawLine(border, 0, Height - 1, Width, Height - 1);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int index = HitTest(e.X);
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

            int index = HitTest(e.X);
            if (index >= 0)
            {
                Focus();
                SelectedIndex = index;
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right ||
                keyData == Keys.Home || keyData == Keys.End)
            {
                return true;
            }

            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left)
            {
                SelectedIndex = Math.Max(0, _selectedIndex - 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                SelectedIndex = Math.Min(Titles.Length - 1, _selectedIndex + 1);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                SelectedIndex = Titles.Length - 1;
                e.Handled = true;
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

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _layoutWidth = -1;
            Invalidate();
        }

        private int HitTest(int x)
        {
            for (int i = 0; i < Titles.Length; i++)
            {
                if (x >= _tabLeft[i] && x < _tabLeft[i] + _tabWidth[i])
                {
                    return i;
                }
            }

            return -1;
        }

        private void EnsureLayout()
        {
            if (_layoutWidth == Width)
            {
                return;
            }

            _layoutWidth = Width;

            int left = 18;
            int pad = 14;
            int gap = 8;
            int[] textWidths = new int[Titles.Length];
            int total = left + 8;
            for (int i = 0; i < Titles.Length; i++)
            {
                textWidths[i] = TextRenderer.MeasureText(
                    Titles[i],
                    UiTheme.BodyBold,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                total += (pad * 2) + textWidths[i];
            }

            total += gap * (Titles.Length - 1);

            while (total > Width && (pad > 7 || gap > 3))
            {
                if (pad > 7)
                {
                    pad--;
                }

                if (gap > 3)
                {
                    gap--;
                }

                total = left + 8 + (gap * (Titles.Length - 1));
                for (int i = 0; i < Titles.Length; i++)
                {
                    total += (pad * 2) + textWidths[i];
                }
            }

            int x = left;
            for (int i = 0; i < Titles.Length; i++)
            {
                _tabLeft[i] = x;
                _tabWidth[i] = (pad * 2) + textWidths[i];
                x += _tabWidth[i] + gap;
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
    }
}
