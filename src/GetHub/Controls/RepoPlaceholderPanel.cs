using System;
using System.Drawing;
using System.Windows.Forms;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // Empty state shown by repository tabs that only exist on GitHub (Issues,
    // Pull requests, Actions, ...). It mirrors GitHub's centered illustration,
    // heading and link while the snapshot itself stays offline.
    public class RepoPlaceholderPanel : UserControl
    {
        private readonly Button _link;
        private readonly Font _titleFont;
        private string _title = string.Empty;
        private string _message = string.Empty;
        private string _url = string.Empty;
        private int _iconIndex;
        private int _messageHeight;
        private int _messageWidth;

        public RepoPlaceholderPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = UiTheme.Canvas;
            Dock = DockStyle.Fill;
            _titleFont = new Font("Segoe UI Semibold", 15F, FontStyle.Regular, GraphicsUnit.Point);

            _link = UiTheme.MakeButton("Open on GitHub", ButtonStyle.Secondary);
            _link.Height = 34;
            _link.Visible = false;
            _link.Click += delegate
            {
                if (!string.IsNullOrEmpty(_url))
                {
                    UiTheme.OpenUrl(_url);
                }
            };
            Controls.Add(_link);
            Visible = false;
        }

        public void Bind(int iconIndex, string title, string message, string url)
        {
            _iconIndex = iconIndex;
            _title = title == null ? string.Empty : title;
            _message = message == null ? string.Empty : message;
            _url = url == null ? string.Empty : url;
            _link.Visible = _url.Length > 0;
            _link.AccessibleName = "Open " + _title + " on GitHub";
            AccessibleName = _title;
            AccessibleDescription = _message;

            LayoutContent();
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutContent();
            Invalidate();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            LayoutContent();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            graphics.Clear(UiTheme.Canvas);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int center = ClientSize.Width / 2;
            Rectangle iconRect = IconBounds();
            RepoIcons.Draw(graphics, _iconIndex, iconRect, UiTheme.Muted, 1.15F);

            TextRenderer.DrawText(
                graphics,
                _title,
                _titleFont,
                TitleBounds(),
                UiTheme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);

            if (_message.Length > 0)
            {
                TextRenderer.DrawText(
                    graphics,
                    _message,
                    UiTheme.BodyFont,
                    MessageBounds(),
                    UiTheme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private const int IconSize = 64;
        private const int IconGap = 16;
        private const int TitleGap = 6;
        private const int ButtonGap = 22;
        private const int EdgeMargin = 8;

        private void LayoutContent()
        {
            int width = ClientSize.Width;
            if (width <= 0)
            {
                return;
            }

            int textWidth = Math.Min(500, Math.Max(160, width - 72));
            _messageWidth = textWidth;
            Size messageSize = TextRenderer.MeasureText(
                _message,
                UiTheme.BodyFont,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
            _messageHeight = messageSize.Height;

            Rectangle icon = IconBounds();
            Rectangle titleRect = TitleBounds();
            Rectangle messageRect = MessageBounds();

            if (_url.Length > 0)
            {
                int linkWidth = TextRenderer.MeasureText(
                    _link.Text,
                    _link.Font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width + 46;
                if (linkWidth < 150)
                {
                    linkWidth = 150;
                }

                int linkTop = messageRect.Bottom + ButtonGap;
                _link.Bounds = new Rectangle(
                    Math.Max(8, (width - linkWidth) / 2),
                    linkTop,
                    linkWidth,
                    _link.Height);
            }
        }

        private Rectangle IconBounds()
        {
            int width = ClientSize.Width;
            int textWidth = Math.Min(500, Math.Max(160, width - 72));
            Size messageSize = TextRenderer.MeasureText(
                _message,
                UiTheme.BodyFont,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);
            int titleHeight = _titleFont.Height + 8;
            int total = IconSize + IconGap + titleHeight + TitleGap + messageSize.Height;
            if (_url.Length > 0)
            {
                total += ButtonGap + _link.Height;
            }

            int top = (ClientSize.Height - total) / 2;
            int lowest = ClientSize.Height - total - EdgeMargin;
            if (top > lowest)
            {
                top = lowest;
            }

            if (top < EdgeMargin)
            {
                top = EdgeMargin;
            }

            return new Rectangle(Math.Max(0, (width - IconSize) / 2), top, IconSize, IconSize);
        }

        private Rectangle TitleBounds()
        {
            int width = ClientSize.Width;
            int textWidth = Math.Min(500, Math.Max(160, width - 72));
            Rectangle icon = IconBounds();
            return new Rectangle(
                Math.Max(0, (width - textWidth) / 2),
                icon.Bottom + IconGap,
                textWidth,
                _titleFont.Height + 8);
        }

        private Rectangle MessageBounds()
        {
            int width = ClientSize.Width;
            Rectangle title = TitleBounds();
            int height = _messageHeight > 0
                ? _messageHeight
                : TextRenderer.MeasureText(
                    _message,
                    UiTheme.BodyFont,
                    new Size(_messageWidth > 0 ? _messageWidth : 400, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding).Height;
            return new Rectangle(
                Math.Max(0, (width - _messageWidth) / 2),
                title.Bottom + TitleGap,
                _messageWidth,
                height + 2);
        }
    }
}
