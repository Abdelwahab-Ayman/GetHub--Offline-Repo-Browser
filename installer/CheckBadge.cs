using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace GetHub.Installer
{
    // The success tick: a green disc with a hand-drawn check, so setup needs no
    // extra image for it and the mark stays sharp on every display scale.
    internal sealed class CheckBadge : Control
    {
        public CheckBadge()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
            Size = new Size(48, 48);
            TabStop = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            Graphics graphics = e.Graphics;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (SolidBrush fill = new SolidBrush(InstallerTheme.Green))
            {
                graphics.FillEllipse(fill, bounds);
            }

            float size = Math.Min(Width, Height);
            using (Pen pen = new Pen(Color.White, Math.Max(2.4f, size * 0.075f)))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                graphics.DrawLines(
                    pen,
                    new PointF[]
                    {
                        new PointF(size * 0.28f, size * 0.52f),
                        new PointF(size * 0.44f, size * 0.68f),
                        new PointF(size * 0.73f, size * 0.33f)
                    });
            }
        }
    }
}
