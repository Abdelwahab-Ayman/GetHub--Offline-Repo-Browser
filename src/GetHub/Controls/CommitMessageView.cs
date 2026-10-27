using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // The "Commit message" section of the History tab. It shows the full commit
    // message, author/committer details, parents and change totals. Everything is
    // owner-drawn and scrolls vertically, so it follows the theme instantly.
    public class CommitMessageView : ScrollableControl
    {
        private static readonly Color[] AvatarColors = new Color[]
        {
            ColorTranslator.FromHtml("#1f6feb"),
            ColorTranslator.FromHtml("#238636"),
            ColorTranslator.FromHtml("#8957e5"),
            ColorTranslator.FromHtml("#bf3989"),
            ColorTranslator.FromHtml("#9e6a03"),
            ColorTranslator.FromHtml("#cf222e"),
            ColorTranslator.FromHtml("#1a7f37"),
            ColorTranslator.FromHtml("#0969da")
        };

        private readonly Font _monoFont;
        private readonly Font _avatarFont;
        private CommitDetail _detail;
        private string _statusTitle = string.Empty;
        private string _statusMessage = string.Empty;
        private int _contentHeight;

        public CommitMessageView()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = UiTheme.Canvas;
            AutoScroll = true;
            Padding = Padding.Empty;
            AccessibleName = "Commit message";
            AccessibleRole = AccessibleRole.Document;
            _monoFont = new Font("Consolas", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
            _avatarFont = new Font("Segoe UI Semibold", 11F, FontStyle.Regular, GraphicsUnit.Point);
            SetStatus("Commit message", "Select a commit in the graph to read its full message.");
        }

        public CommitDetail Detail
        {
            get { return _detail; }
        }

        public bool HasDetail
        {
            get { return _detail != null; }
        }

        public void SetDetail(CommitDetail detail)
        {
            _detail = detail;
            _statusTitle = string.Empty;
            _statusMessage = string.Empty;
            AutoScrollPosition = Point.Empty;
            Recalculate();
            Invalidate();
        }

        public void SetStatus(string title, string message)
        {
            _detail = null;
            _statusTitle = title == null ? string.Empty : title;
            _statusMessage = message == null ? string.Empty : message;
            _contentHeight = 0;
            AutoScrollMinSize = Size.Empty;
            AutoScrollPosition = Point.Empty;
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Recalculate();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics graphics = e.Graphics;
            graphics.Clear(UiTheme.Canvas);

            if (_detail == null)
            {
                CommitStatusPainter.Paint(graphics, ClientSize, _statusTitle, _statusMessage);
                return;
            }

            int width = ContentWidth;
            int y = 20 + AutoScrollPosition.Y;

            // Subject, wrapped like GitHub's commit title.
            Rectangle subjectBounds = new Rectangle(24, y, width, int.MaxValue);
            Size subjectSize = TextRenderer.MeasureText(
                graphics,
                _detail.Subject,
                UiTheme.HeadingFont,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(
                graphics,
                _detail.Subject,
                UiTheme.HeadingFont,
                subjectBounds,
                UiTheme.Text,
                TextFormatFlags.Top | TextFormatFlags.Left | TextFormatFlags.WordBreak |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            y += subjectSize.Height + 14;

            // Author avatar and byline.
            int avatar = 40;
            Rectangle avatarBounds = new Rectangle(24, y, avatar, avatar);
            DrawAvatar(graphics, avatarBounds, _detail.AuthorDisplay);
            int textLeft = avatarBounds.Right + 12;
            int textWidth = Math.Max(80, width - (textLeft - 24));

            TextRenderer.DrawText(
                graphics,
                _detail.AuthorDisplay,
                UiTheme.BodyBold,
                new Rectangle(textLeft, y, textWidth, UiTheme.BodyBold.Height + 2),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            string relative = Display.RelativeTime(_detail.AuthorDateUtc);
            int nameWidth = TextRenderer.MeasureText(
                graphics,
                _detail.AuthorDisplay,
                UiTheme.BodyBold,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            if (nameWidth < textWidth - 20)
            {
                TextRenderer.DrawText(
                    graphics,
                    "committed " + relative,
                    UiTheme.BodyFont,
                    new Rectangle(textLeft + nameWidth + 6, y, Math.Max(10, textWidth - nameWidth - 6), UiTheme.BodyFont.Height + 2),
                    UiTheme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }

            string email = _detail.AuthorEmail;
            int lineTwo = y + UiTheme.BodyBold.Height + 1;
            TextRenderer.DrawText(
                graphics,
                string.IsNullOrEmpty(email) ? relative : relative + "  \u00b7  " + email,
                UiTheme.SmallFont,
                new Rectangle(textLeft, lineTwo, textWidth, UiTheme.SmallFont.Height + 2),
                UiTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding |
                TextFormatFlags.EndEllipsis);

            y = avatarBounds.Bottom + 16;

            if (_detail.HasDifferentCommitter)
            {
                string committer = "Committed by " + _detail.CommitterName;
                if (!string.IsNullOrEmpty(_detail.CommitterEmail))
                {
                    committer += " <" + _detail.CommitterEmail + ">";
                }

                // The committer date is only relevant when it differs from the author date.
                if (_detail.CommitDateUtc != DateTime.MinValue &&
                    Math.Abs((_detail.CommitDateUtc - _detail.AuthorDateUtc).TotalMinutes) >= 1)
                {
                    committer += " on " + FormatDate(_detail.CommitDateUtc);
                }

                Size committerSize = TextRenderer.MeasureText(
                    graphics,
                    committer,
                    UiTheme.SmallFont,
                    new Size(width, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(
                    graphics,
                    committer,
                    UiTheme.SmallFont,
                    new Rectangle(24, y, width, committerSize.Height + 2),
                    UiTheme.Muted,
                    TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak |
                    TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
                y += committerSize.Height + 12;
            }

            // Short SHA and parents.
            y = DrawShaRow(graphics, 24, y, width);
            y += 14;

            // Change totals.
            if (_detail.Files != null && _detail.Files.Count > 0)
            {
                y = DrawStats(graphics, 24, y, width);
                y += 14;
            }

            // Full message body in a bordered card.
            string body = _detail.Body;
            if (string.IsNullOrEmpty(body))
            {
                body = "(no description)";
            }

            int bodyWidth = Math.Max(60, width - 28);
            Size bodySize = TextRenderer.MeasureText(
                graphics,
                body,
                UiTheme.BodyFont,
                new Size(bodyWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            int cardHeight = Math.Max(56, bodySize.Height + 28);
            Rectangle card = new Rectangle(24, y, width, cardHeight);

            using (SolidBrush fill = new SolidBrush(UiTheme.Surface))
            using (Pen border = new Pen(UiTheme.Border))
            {
                graphics.FillRectangle(fill, card);
                graphics.DrawRectangle(border, card.Left, card.Top, card.Width - 1, card.Height - 1);
            }

            Color bodyColor = string.IsNullOrEmpty(_detail.Body) ? UiTheme.Muted : UiTheme.Text;
            TextRenderer.DrawText(
                graphics,
                body,
                UiTheme.BodyFont,
                new Rectangle(card.Left + 14, card.Top + 14, bodyWidth, bodySize.Height + 2),
                bodyColor,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            // Keep the scroll extent in sync with the measured layout.
            int total = card.Bottom + 24 - AutoScrollPosition.Y;
            if (total != _contentHeight)
            {
                _contentHeight = total;
                AutoScrollMinSize = new Size(0, total);
            }
        }

        private int DrawShaRow(Graphics graphics, int left, int top, int width)
        {
            int x = left;
            string sha = _detail.ShortSha;

            Size shaSize = TextRenderer.MeasureText(
                graphics,
                sha,
                _monoFont,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            Rectangle pill = new Rectangle(x, top, shaSize.Width + 18, 22);
            using (GraphicsPath path = RoundedRectangle(pill, 10))
            using (SolidBrush fill = new SolidBrush(UiTheme.ChipBackground))
            using (Pen border = new Pen(UiTheme.Border))
            {
                graphics.FillPath(fill, path);
                graphics.DrawPath(border, path);
            }

            TextRenderer.DrawText(
                graphics,
                sha,
                _monoFont,
                pill,
                UiTheme.Accent,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            x = pill.Right + 12;

            // Merge commits list every parent; normal commits show their one parent.
            string parentLabel;
            if (_detail.Parents != null && _detail.Parents.Count > 1)
            {
                parentLabel = _detail.Parents.Count + " parents: " + JoinShas(_detail.Parents);
            }
            else if (_detail.Parents != null && _detail.Parents.Count == 1)
            {
                parentLabel = "parent " + ShortSha(_detail.Parents[0]);
            }
            else
            {
                parentLabel = "root commit";
            }

            TextRenderer.DrawText(
                graphics,
                parentLabel,
                UiTheme.SmallFont,
                new Rectangle(x, top + 2, Math.Max(20, width - (x - left)), 20),
                UiTheme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);

            return pill.Bottom;
        }

        private int DrawStats(Graphics graphics, int left, int top, int width)
        {
            int files = _detail.ChangedFiles;
            string fileText = files == 1 ? "1 file changed" : files + " files changed";
            Size fileSize = TextRenderer.MeasureText(
                graphics,
                fileText,
                UiTheme.BodyBold,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(
                graphics,
                fileText,
                UiTheme.BodyBold,
                new Rectangle(left, top, Math.Min(width, fileSize.Width + 4), fileSize.Height + 2),
                UiTheme.Text,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            int x = left + fileSize.Width + 14;
            x = DrawCount(graphics, x, top, "+" + _detail.Additions, UiTheme.Green);
            x = DrawCount(graphics, x + 6, top, "\u2212" + _detail.Deletions, UiTheme.Red);

            if (_detail.Truncated)
            {
                TextRenderer.DrawText(
                    graphics,
                    "truncated",
                    UiTheme.SmallFont,
                    new Rectangle(x + 10, top + 2, Math.Max(20, width - (x - left)), 18),
                    UiTheme.Faint,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }

            return top + fileSize.Height + 2;
        }

        private static int DrawCount(Graphics graphics, int left, int top, string text, Color color)
        {
            Size size = TextRenderer.MeasureText(
                graphics,
                text,
                UiTheme.BodyBold,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            TextRenderer.DrawText(
                graphics,
                text,
                UiTheme.BodyBold,
                new Rectangle(left, top, size.Width + 4, size.Height + 2),
                color,
                TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.SingleLine |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            return left + size.Width;
        }

        private void DrawAvatar(Graphics graphics, Rectangle bounds, string name)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new SolidBrush(AvatarColor(name)))
            {
                graphics.FillEllipse(brush, bounds);
            }

            TextRenderer.DrawText(
                graphics,
                Initials(name),
                _avatarFont,
                bounds,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            graphics.SmoothingMode = SmoothingMode.Default;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _monoFont.Dispose();
                _avatarFont.Dispose();
            }

            base.Dispose(disposing);
        }

        private static Color AvatarColor(string login)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < (login == null ? 0 : login.Length); i++)
                {
                    hash = hash * 31 + login[i];
                }

                return AvatarColors[(hash & 0x7FFFFFFF) % AvatarColors.Length];
            }
        }

        private static string Initials(string login)
        {
            if (string.IsNullOrEmpty(login))
            {
                return "?";
            }

            string[] parts = login.Split(
                new char[] { '-', '_', '.', ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return (parts[0].Substring(0, 1) + parts[parts.Length - 1].Substring(0, 1)).ToUpperInvariant();
            }

            return login.Length == 1
                ? login.ToUpperInvariant()
                : login.Substring(0, 2).ToUpperInvariant();
        }

        private static string JoinShas(System.Collections.Generic.List<string> shas)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < shas.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(ShortSha(shas[i]));
            }

            return builder.ToString();
        }

        private static string ShortSha(string sha)
        {
            if (string.IsNullOrEmpty(sha))
            {
                return string.Empty;
            }

            return sha.Length > 7 ? sha.Substring(0, 7) : sha;
        }

        private static string FormatDate(DateTime utc)
        {
            if (utc == DateTime.MinValue)
            {
                return "unknown";
            }

            DateTime local = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
            return local.ToString("MMM d, yyyy 'at' HH:mm");
        }

        private int ContentWidth
        {
            get
            {
                int width = ClientSize.Width - 48;
                if (AutoScrollMinSize.Height > 0 && VerticalScroll.Visible)
                {
                    width -= SystemInformation.VerticalScrollBarWidth;
                }

                return Math.Max(140, width);
            }
        }

        private void Recalculate()
        {
            if (_detail == null)
            {
                return;
            }

            // Measuring happens on paint because TextRenderer needs a device context
            // that matches the actual font rendering; a rough estimate keeps the
            // scrollbar from jumping until the first paint.
            int width = ContentWidth;
            int height = 20 + UiTheme.HeadingFont.Height + 14 + 40 + 16;
            if (_detail.HasDifferentCommitter)
            {
                height += UiTheme.SmallFont.Height + 12;
            }

            height += 22 + 14;
            if (_detail.Files != null && _detail.Files.Count > 0)
            {
                height += UiTheme.BodyBold.Height + 16;
            }

            string body = string.IsNullOrEmpty(_detail.Body) ? "(no description)" : _detail.Body;
            Size bodySize = TextRenderer.MeasureText(
                body,
                UiTheme.BodyFont,
                new Size(Math.Max(60, width - 28), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            height += Math.Max(56, bodySize.Height + 28) + 24;

            _contentHeight = height;
            AutoScrollMinSize = new Size(0, height);
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
