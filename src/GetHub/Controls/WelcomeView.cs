using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GetHub.Utilities;

namespace GetHub.Controls
{
    public class WelcomeView : UserControl
    {
        private readonly Button _addButton;
        private readonly Font _titleFont;
        private readonly Font _subtitleFont;
        private readonly Font _featureFont;

        public event EventHandler AddRequested;

        public WelcomeView()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = UiTheme.Canvas;

            _titleFont = new Font("Segoe UI Semibold", 24F, FontStyle.Regular);
            _subtitleFont = new Font("Segoe UI", 10F, FontStyle.Regular);
            _featureFont = new Font("Segoe UI", 9.25F, FontStyle.Regular);

            _addButton = UiTheme.MakeButton("Add a repository", ButtonStyle.Primary);
            _addButton.Size = new Size(184, 38);
            _addButton.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            _addButton.Click += delegate { OnAddRequested(); };
            Controls.Add(_addButton);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            PositionButton();
        }

        private const int LogoSize = 78;
        private const int TitleHeight = 42;
        private const int SubtitleHeight = 26;
        private const int FeatureHeight = 28;
        private const int FeatureCount = 3;
        private const int ButtonHeight = 38;
        private const int HintHeight = 22;

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(UiTheme.Canvas);

            int logoTop;
            int titleTop;
            int subtitleTop;
            int featureTop;
            int buttonTop;
            int hintTop;
            int contentWidth;
            ComputeLayout(out logoTop, out titleTop, out subtitleTop, out featureTop, out buttonTop, out hintTop, out contentWidth);

            int center = Width / 2;

            Rectangle logoRect = new Rectangle(center - LogoSize / 2, logoTop, LogoSize, LogoSize);
            Bitmap logo = AppAssets.Logo;
            if (logo != null)
            {
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(logo, logoRect);
            }
            else
            {
                DrawLogoPlaceholder(graphics, logoRect);
            }

            Rectangle titleRect = new Rectangle(center - contentWidth / 2, titleTop, contentWidth, TitleHeight);
            TextRenderer.DrawText(
                graphics,
                "GetHub",
                _titleFont,
                titleRect,
                UiTheme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            Rectangle subtitleRect = new Rectangle(center - contentWidth / 2, subtitleTop, contentWidth, SubtitleHeight);
            TextRenderer.DrawText(
                graphics,
                "GetHub \u2022 Browse GitHub repositories offline",
                _subtitleFont,
                subtitleRect,
                UiTheme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);

            string[] features = new string[]
            {
                "Public and private GitHub repositories",
                "Folders, source files, images and rendered READMEs",
                "Sync a fresh copy at any time"
            };

            int featureWidth = Math.Min(420, contentWidth);
            int featureLeft = center - featureWidth / 2;
            int featureY = featureTop;
            foreach (string feature in features)
            {
                Rectangle bullet = new Rectangle(featureLeft, featureY + 5, 8, 8);
                using (SolidBrush dot = new SolidBrush(UiTheme.Green))
                {
                    graphics.FillEllipse(dot, bullet);
                }

                TextRenderer.DrawText(
                    graphics,
                    feature,
                    _featureFont,
                    new Rectangle(featureLeft + 18, featureY, featureWidth - 18, FeatureHeight - 6),
                    UiTheme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                featureY += FeatureHeight;
            }

            Rectangle hintRect = new Rectangle(center - contentWidth / 2, hintTop, contentWidth, HintHeight);
            TextRenderer.DrawText(
                graphics,
                "Everything is stored locally in %LocalAppData%\\GetHub.",
                UiTheme.SmallFont,
                hintRect,
                UiTheme.Faint,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _titleFont.Dispose();
                _subtitleFont.Dispose();
                _featureFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private void PositionButton()
        {
            int logoTop;
            int titleTop;
            int subtitleTop;
            int featureTop;
            int buttonTop;
            int hintTop;
            int contentWidth;
            ComputeLayout(out logoTop, out titleTop, out subtitleTop, out featureTop, out buttonTop, out hintTop, out contentWidth);

            _addButton.Location = new Point((Width - _addButton.Width) / 2, buttonTop);
        }

        // Everything is stacked one after another using measured heights, so the button can
        // never cover the hint text below it.
        private void ComputeLayout(
            out int logoTop,
            out int titleTop,
            out int subtitleTop,
            out int featureTop,
            out int buttonTop,
            out int hintTop,
            out int contentWidth)
        {
            contentWidth = Width - 80;
            if (contentWidth > 620)
            {
                contentWidth = 620;
            }

            if (contentWidth < 260)
            {
                contentWidth = 260;
            }

            int gapTitle = 20;
            int gapSubtitle = 2;
            int gapFeatures = 18;
            int gapButton = 20;
            int gapHint = 14;
            int blockHeight = LogoSize + gapTitle + TitleHeight + gapSubtitle + SubtitleHeight +
                gapFeatures + (FeatureHeight * FeatureCount) + gapButton + ButtonHeight + gapHint + HintHeight;

            int top = (Height - blockHeight) / 2;
            if (top < 28)
            {
                top = 28;
            }

            logoTop = top;
            titleTop = logoTop + LogoSize + gapTitle;
            subtitleTop = titleTop + TitleHeight + gapSubtitle;
            featureTop = subtitleTop + SubtitleHeight + gapFeatures;
            buttonTop = featureTop + (FeatureHeight * FeatureCount) + gapButton;
            hintTop = buttonTop + ButtonHeight + gapHint;
        }

        private void OnAddRequested()
        {
            EventHandler handler = AddRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private static void DrawLogoPlaceholder(Graphics graphics, Rectangle bounds)
        {
            using (GraphicsPath path = RoundedRectangle(bounds, 16))
            using (SolidBrush brush = new SolidBrush(UiTheme.Surface))
            using (Pen pen = new Pen(UiTheme.Border))
            {
                graphics.FillPath(brush, path);
                graphics.DrawPath(pen, path);
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
