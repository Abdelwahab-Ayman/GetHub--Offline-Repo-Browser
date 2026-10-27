using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Controls
{
    public class RepoCard : Control
    {
        private RepoRecord _record;
        private bool _selected;
        private bool _hover;

        public RepoRecord Record
        {
            get { return _record; }
            set
            {
                _record = value;
                Invalidate();
            }
        }

        public bool Selected
        {
            get { return _selected; }
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    Invalidate();
                }
            }
        }

        public RepoCard()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);
            Height = 96;
            Cursor = Cursors.Hand;
            TabStop = true;
            BackColor = UiTheme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color parentBack = Parent == null ? UiTheme.Surface : Parent.BackColor;
            graphics.Clear(parentBack);

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill = UiTheme.Surface;
            if (_selected)
            {
                fill = UiTheme.Selection;
            }
            else if (_hover)
            {
                fill = ColorTranslator.FromHtml("#1c2128");
            }

            using (GraphicsPath path = RoundedRectangle(bounds, 6))
            {
                using (SolidBrush brush = new SolidBrush(fill))
                {
                    graphics.FillPath(brush, path);
                }

                Color borderColor = _selected ? UiTheme.Accent : UiTheme.Border;
                using (Pen pen = new Pen(borderColor))
                {
                    graphics.DrawPath(pen, path);
                }
            }

            if (_selected)
            {
                using (SolidBrush accent = new SolidBrush(UiTheme.Accent))
                {
                    graphics.FillRectangle(accent, 0, 12, 2, Height - 24);
                }
            }

            if (_record == null)
            {
                return;
            }

            int left = 16;
            int textWidth = Width - 32;
            TextRenderer.DrawText(
                graphics,
                _record.GetDisplayName(),
                UiTheme.CardTitleFont,
                new Rectangle(left, 11, textWidth, 21),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            string description = string.IsNullOrWhiteSpace(_record.Description)
                ? "No description"
                : _record.Description.Replace("\r", " ").Replace("\n", " ");
            TextRenderer.DrawText(
                graphics,
                description,
                UiTheme.BodyFont,
                new Rectangle(left, 34, textWidth, 19),
                UiTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            Color languageColor = Display.LanguageColor(_record.Language);
            using (SolidBrush dot = new SolidBrush(languageColor))
            {
                graphics.FillEllipse(dot, left, 66, 8, 8);
            }

            string language = string.IsNullOrEmpty(_record.Language) ? "Unknown" : _record.Language;
            string meta = Display.Count(_record.Stars) + " stars  ·  " + language;
            TextRenderer.DrawText(
                graphics,
                meta,
                UiTheme.SmallFont,
                new Rectangle(left + 14, 62, textWidth - 14, 17),
                UiTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            string synced = "synced " + Display.RelativeTime(_record.DownloadedAtUtc);
            TextRenderer.DrawText(
                graphics,
                synced,
                UiTheme.SmallFont,
                new Rectangle(left, 78, textWidth, 17),
                UiTheme.Faint,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            if (Focused)
            {
                Rectangle focus = new Rectangle(3, 3, Width - 7, Height - 7);
                ControlPaint.DrawFocusRectangle(graphics, focus);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnGotFocus(EventArgs e)
        {
            Invalidate();
            base.OnGotFocus(e);
        }

        protected override void OnLostFocus(EventArgs e)
        {
            Invalidate();
            base.OnLostFocus(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
                return;
            }

            base.OnKeyDown(e);
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
