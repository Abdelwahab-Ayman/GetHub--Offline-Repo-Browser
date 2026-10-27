using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // Paints the bundled GetHub badge directly instead of using a PictureBox: the artwork
    // keeps its rounded transparent corners on both themes and stays crisp at small sizes.
    public class LogoView : Control
    {
        public LogoView()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            Size = new Size(24, 24);
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            if (Parent != null)
            {
                using (SolidBrush background = new SolidBrush(Parent.BackColor))
                {
                    graphics.FillRectangle(background, ClientRectangle);
                }
            }

            Bitmap logo = AppAssets.Logo;
            if (logo == null || Width < 4 || Height < 4)
            {
                return;
            }

            graphics.SmoothingMode = SmoothingMode.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Size fit = Fit(logo.Width, logo.Height, Width, Height);
            graphics.DrawImage(
                logo,
                new Rectangle((Width - fit.Width) / 2, (Height - fit.Height) / 2, fit.Width, fit.Height));
        }

        private static Size Fit(int width, int height, int maxWidth, int maxHeight)
        {
            double scale = Math.Min(maxWidth / (double)width, maxHeight / (double)height);
            int fittedWidth = Math.Max(1, (int)Math.Round(width * scale));
            int fittedHeight = Math.Max(1, (int)Math.Round(height * scale));
            return new Size(fittedWidth, fittedHeight);
        }
    }
}
