using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // GitHub-style repository section tabs (Code, Issues, Pull requests, ...).
    // The strip paints itself so it follows the dark/light palette instantly and
    // compresses its spacing instead of clipping when the window is at its minimum size.
    public class RepoTabStrip : Control
    {
        public const int PreferredHeight = 48;
        public const int HistoryIndex = 7;

        private static readonly string[] TabTitles = new string[]
        {
            "Code",
            "Issues",
            "Pull requests",
            "Actions",
            "Projects",
            "Wiki",
            "Insights",
            "History"
        };

        private static readonly string[] TabPaths = new string[]
        {
            string.Empty,
            "/issues",
            "/pulls",
            "/actions",
            "/projects",
            "/wiki",
            "/pulse",
            "/commits"
        };

        private static readonly string[] TabDescriptions = new string[]
        {
            string.Empty,
            "Issues are not stored in the offline snapshot. Open this repository on GitHub to read, filter and manage them.",
            "Pull requests are not stored in the offline snapshot. Open this repository on GitHub to review, comment on and merge them.",
            "Workflow runs are not stored in the offline snapshot. Open this repository on GitHub to see its Actions and logs.",
            "Project boards live on GitHub and are not part of the offline snapshot. Open this repository there to plan and track work.",
            "The wiki is not stored in the offline snapshot. Open this repository on GitHub to read it.",
            "Traffic and contribution insights are only available on GitHub, not in the offline snapshot. Open this repository there to view its pulse.",
            string.Empty
        };

        private readonly int[] _tabLeft = new int[TabTitles.Length];
        private readonly int[] _tabWidth = new int[TabTitles.Length];
        private int _selectedIndex;
        private int _hoverIndex = -1;
        private int _layoutWidth = -1;
        private int _pad;
        private int _gap;
        private int _iconSize;

        public event EventHandler SelectedIndexChanged;

        public RepoTabStrip()
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
            AccessibleName = "Repository sections";
        }

        public int SelectedIndex
        {
            get { return _selectedIndex; }
            set
            {
                int index = value;
                if (index < 0)
                {
                    index = 0;
                }

                if (index >= TabTitles.Length)
                {
                    index = TabTitles.Length - 1;
                }

                if (index == _selectedIndex)
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

        public static int Count
        {
            get { return TabTitles.Length; }
        }

        public static string GetTitle(int index)
        {
            return index >= 0 && index < TabTitles.Length ? TabTitles[index] : string.Empty;
        }

        public static string GetPath(int index)
        {
            return index >= 0 && index < TabPaths.Length ? TabPaths[index] : string.Empty;
        }

        public static string GetDescription(int index)
        {
            return index >= 0 && index < TabDescriptions.Length ? TabDescriptions[index] : string.Empty;
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            _layoutWidth = -1;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            _layoutWidth = -1;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int hover = HitTest(e.X, e.Y);
            if (hover != _hoverIndex)
            {
                _hoverIndex = hover;
                Cursor = hover >= 0 ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (_hoverIndex != -1)
            {
                _hoverIndex = -1;
                Cursor = Cursors.Default;
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

            int index = HitTest(e.X, e.Y);
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
                SelectedIndex = _selectedIndex - 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Right)
            {
                SelectedIndex = _selectedIndex + 1;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Home)
            {
                SelectedIndex = 0;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.End)
            {
                SelectedIndex = TabTitles.Length - 1;
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

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.Clear(UiTheme.Surface);
            EnsureLayout();

            using (Pen border = new Pen(UiTheme.Border))
            {
                graphics.DrawLine(border, 0, Height - 1, Width, Height - 1);
            }

            for (int i = 0; i < TabTitles.Length; i++)
            {
                Rectangle tab = new Rectangle(_tabLeft[i], 0, _tabWidth[i], Height);
                if (i == _hoverIndex && i != _selectedIndex && tab.Width > 4)
                {
                    using (GraphicsPath hover = RoundedRectangle(
                        new Rectangle(tab.Left + 1, 4, tab.Width - 2, Height - 10), 6))
                    using (SolidBrush fill = new SolidBrush(UiTheme.HoverBackground))
                    {
                        graphics.FillPath(fill, hover);
                    }
                }

                bool active = i == _selectedIndex;
                Color color = active || i == _hoverIndex ? UiTheme.Text : UiTheme.Muted;

                Rectangle iconRect = new Rectangle(
                    tab.Left + _pad,
                    (Height - _iconSize) / 2,
                    _iconSize,
                    _iconSize);
                RepoIcons.Draw(graphics, i, iconRect, color);

                int textLeft = iconRect.Right + _gap;
                // Two spare pixels come from the right padding so GDI rounding can
                // never turn an exactly-fitting label into an ellipsized one.
                int textWidth = tab.Right - _pad - textLeft + 2;
                if (textWidth < 8)
                {
                    textWidth = 8;
                }

                TextRenderer.DrawText(
                    graphics,
                    TabTitles[i],
                    Font,
                    new Rectangle(textLeft, 0, textWidth, Height - 2),
                    color,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

                if (active)
                {
                    using (SolidBrush accent = new SolidBrush(UiTheme.Accent))
                    {
                        graphics.FillRectangle(accent, tab.Left, Height - 2, tab.Width, 2);
                    }
                }
            }

            if (Focused && ShowFocusCues && _selectedIndex >= 0)
            {
                Rectangle tab = new Rectangle(_tabLeft[_selectedIndex], 0, _tabWidth[_selectedIndex], Height);
                ControlPaint.DrawFocusRectangle(
                    graphics,
                    new Rectangle(tab.Left + 3, 5, Math.Max(1, tab.Width - 6), Height - 13));
            }
        }

        private void EnsureLayout()
        {
            if (_layoutWidth == Width)
            {
                return;
            }

            _layoutWidth = Width;

            int left = Math.Min(22, Math.Max(8, Width / 24));
            int right = 8;
            int pad = 12;
            int gap = 6;
            int icon = 16;
            int[] textWidths = new int[TabTitles.Length];
            for (int i = 0; i < TabTitles.Length; i++)
            {
                textWidths[i] = TextRenderer.MeasureText(
                    TabTitles[i],
                    Font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            }

            int total = left + right;
            for (int i = 0; i < TabTitles.Length; i++)
            {
                total += (pad * 2) + icon + gap + textWidths[i];
            }

            // Compress spacing before anything is allowed to overflow. At the 960px
            // minimum window width the strip lands here and every label stays whole.
            while (total > Width && (pad > 5 || gap > 4 || icon > 12))
            {
                if (pad > 5)
                {
                    pad--;
                }
                else if (gap > 4)
                {
                    gap--;
                }
                else if (icon > 12)
                {
                    icon--;
                }

                total = left + right;
                for (int i = 0; i < TabTitles.Length; i++)
                {
                    total += (pad * 2) + icon + gap + textWidths[i];
                }
            }

            _pad = pad;
            _gap = gap;
            _iconSize = icon;

            if (total > Width)
            {
                // Last-resort guard for unusually narrow hosts: equal slots with
                // ellipsized labels, never a control that paints past its edge.
                int slot = Math.Max(32, (Width - left - right) / TabTitles.Length);
                for (int i = 0; i < TabTitles.Length; i++)
                {
                    _tabLeft[i] = left + (slot * i);
                    _tabWidth[i] = slot;
                }

                return;
            }

            int x = left;
            for (int i = 0; i < TabTitles.Length; i++)
            {
                _tabLeft[i] = x;
                _tabWidth[i] = (pad * 2) + icon + gap + textWidths[i];
                x += _tabWidth[i];
            }
        }

        private int HitTest(int x, int y)
        {
            if (y < 0 || y >= Height || x < 0 || x >= Width)
            {
                return -1;
            }

            EnsureLayout();
            for (int i = 0; i < TabTitles.Length; i++)
            {
                if (x >= _tabLeft[i] && x < _tabLeft[i] + _tabWidth[i])
                {
                    return i;
                }
            }

            return -1;
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
