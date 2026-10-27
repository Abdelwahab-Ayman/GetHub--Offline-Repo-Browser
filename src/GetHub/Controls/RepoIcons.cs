using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace GetHub.Controls
{
    // Draws small GitHub-style octicon outlines for the repository tabs and the
    // matching empty-state artwork. Everything is painted so the icons follow the
    // active theme without shipping image assets.
    internal static class RepoIcons
    {
        public const float DefaultStroke = 1.4F;

        public static void Draw(Graphics graphics, int index, Rectangle bounds, Color color)
        {
            Draw(graphics, index, bounds, color, DefaultStroke);
        }

        public static void Draw(Graphics graphics, int index, Rectangle bounds, Color color, float stroke)
        {
            if (graphics == null || bounds.Width < 4 || bounds.Height < 4)
            {
                return;
            }

            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                float scale = Math.Min(bounds.Width / 16F, bounds.Height / 16F);
                graphics.TranslateTransform(
                    bounds.X + (bounds.Width - (16F * scale)) / 2F,
                    bounds.Y + (bounds.Height - (16F * scale)) / 2F);
                graphics.ScaleTransform(scale, scale);

                using (Pen pen = new Pen(color, stroke))
                using (SolidBrush brush = new SolidBrush(color))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;

                    switch (index)
                    {
                        case 0:
                            DrawCode(graphics, pen);
                            break;
                        case 1:
                            DrawIssues(graphics, pen, brush);
                            break;
                        case 2:
                            DrawPullRequest(graphics, pen);
                            break;
                        case 3:
                            DrawActions(graphics, pen, brush);
                            break;
                        case 4:
                            DrawProjects(graphics, pen);
                            break;
                        case 5:
                            DrawWiki(graphics, pen);
                            break;
                        case 6:
                            DrawInsights(graphics, pen);
                            break;
                        default:
                            DrawHistory(graphics, pen);
                            break;
                    }
                }
            }
            finally
            {
                graphics.Restore(state);
            }
        }

        private static void DrawCode(Graphics graphics, Pen pen)
        {
            graphics.DrawLines(pen, new PointF[]
            {
                new PointF(6.2F, 4.4F),
                new PointF(2.6F, 8F),
                new PointF(6.2F, 11.6F)
            });
            graphics.DrawLine(pen, 9.7F, 3.4F, 6.9F, 12.6F);
            graphics.DrawLines(pen, new PointF[]
            {
                new PointF(9.8F, 4.4F),
                new PointF(13.4F, 8F),
                new PointF(9.8F, 11.6F)
            });
        }

        private static void DrawIssues(Graphics graphics, Pen pen, SolidBrush brush)
        {
            graphics.DrawEllipse(pen, 2.5F, 2.5F, 11F, 11F);
            graphics.FillEllipse(brush, 6.8F, 6.8F, 2.4F, 2.4F);
        }

        private static void DrawPullRequest(Graphics graphics, Pen pen)
        {
            graphics.DrawEllipse(pen, 2.3F, 1.6F, 3.4F, 3.4F);
            graphics.DrawLine(pen, 4F, 5F, 4F, 13F);
            graphics.DrawLine(pen, 4F, 8.2F, 10.6F, 8.2F);
            graphics.DrawLine(pen, 10.6F, 8.2F, 10.6F, 6F);
            graphics.DrawLine(pen, 9.4F, 7.2F, 10.6F, 6F);
            graphics.DrawLine(pen, 11.8F, 7.2F, 10.6F, 6F);
        }

        private static void DrawActions(Graphics graphics, Pen pen, SolidBrush brush)
        {
            graphics.DrawEllipse(pen, 2.4F, 2.4F, 11.2F, 11.2F);
            using (GraphicsPath play = new GraphicsPath())
            {
                play.AddPolygon(new PointF[]
                {
                    new PointF(6.6F, 5.2F),
                    new PointF(11F, 8F),
                    new PointF(6.6F, 10.8F)
                });
                graphics.FillPath(brush, play);
            }
        }

        private static void DrawProjects(Graphics graphics, Pen pen)
        {
            using (GraphicsPath board = RoundedRectangle(new RectangleF(2.2F, 2.2F, 11.6F, 11.6F), 1.8F))
            {
                graphics.DrawPath(pen, board);
            }

            graphics.DrawLine(pen, 6.8F, 2.6F, 6.8F, 13.4F);
            graphics.DrawLine(pen, 6.8F, 6.4F, 13.4F, 6.4F);
        }

        private static void DrawWiki(Graphics graphics, Pen pen)
        {
            PointF[] left = new PointF[]
            {
                new PointF(7.9F, 4.5F),
                new PointF(2.7F, 3.2F),
                new PointF(2.7F, 11.7F),
                new PointF(7.9F, 13F)
            };
            PointF[] right = new PointF[]
            {
                new PointF(8.1F, 4.5F),
                new PointF(13.3F, 3.2F),
                new PointF(13.3F, 11.7F),
                new PointF(8.1F, 13F)
            };
            graphics.DrawPolygon(pen, left);
            graphics.DrawPolygon(pen, right);
            graphics.DrawLine(pen, 7.9F, 4.5F, 7.9F, 13F);
            graphics.DrawLine(pen, 8.1F, 4.5F, 8.1F, 13F);
        }

        private static void DrawInsights(Graphics graphics, Pen pen)
        {
            graphics.DrawLines(pen, new PointF[]
            {
                new PointF(2.3F, 12.1F),
                new PointF(5.6F, 8.1F),
                new PointF(8.6F, 10.1F),
                new PointF(13.7F, 3.7F)
            });
            graphics.DrawLine(pen, 2.3F, 13.6F, 13.7F, 13.6F);
        }

        // Commit graph: a trunk with two commits and a branch curving off it.
        private static void DrawHistory(Graphics graphics, Pen pen)
        {
            graphics.DrawLine(pen, 4.6F, 4.4F, 4.6F, 12F);
            graphics.DrawBezier(pen, 4.6F, 8.2F, 8.4F, 8.2F, 8.4F, 7.2F, 11.4F, 7.2F);

            using (SolidBrush brush = new SolidBrush(pen.Color))
            {
                graphics.FillEllipse(brush, 3F, 1.6F, 3.2F, 3.2F);
                graphics.FillEllipse(brush, 3F, 10.4F, 3.2F, 3.2F);
                graphics.FillEllipse(brush, 9.8F, 5.6F, 3.2F, 3.2F);
            }
        }

        private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            float diameter = radius * 2F;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270F, 90F);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0F, 90F);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90F, 90F);
            path.CloseFigure();
            return path;
        }
    }
}
