using System;
using System.Drawing;
using System.Windows.Forms;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // Shared empty/loading/error state for the commit detail tabs (Message, Files,
    // Diff). Kept in one place so all three look identical to the graph's state.
    internal static class CommitStatusPainter
    {
        public static void Paint(Graphics graphics, Size clientSize, string title, string message)
        {
            if (graphics == null || clientSize.Width <= 0 || clientSize.Height <= 0)
            {
                return;
            }

            int width = clientSize.Width;
            int textWidth = Math.Min(540, Math.Max(180, width - 80));

            Size titleSize = TextRenderer.MeasureText(
                title == null ? string.Empty : title,
                UiTheme.BodyBold,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            Size messageSize = TextRenderer.MeasureText(
                message == null ? string.Empty : message,
                UiTheme.SmallFont,
                new Size(textWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            int total = 64 + 16 + titleSize.Height + 8 + messageSize.Height;
            int top = (clientSize.Height - total) / 2;
            if (top < 20)
            {
                top = 20;
            }

            int center = width / 2;
            RepoIcons.Draw(
                graphics,
                RepoTabStrip.HistoryIndex,
                new Rectangle(center - 32, top, 64, 64),
                UiTheme.Muted,
                1.15F);

            int titleTop = top + 80;
            TextRenderer.DrawText(
                graphics,
                title == null ? string.Empty : title,
                UiTheme.BodyBold,
                new Rectangle(center - (textWidth / 2), titleTop, textWidth, titleSize.Height + 2),
                UiTheme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.Top |
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding);

            TextRenderer.DrawText(
                graphics,
                message == null ? string.Empty : message,
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
    }
}
