using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // GitHub's repository landing page shows a right-hand column with About, Releases,
    // Packages and Contributors. This control reproduces that column for the offline copy.
    public class RepoAboutPanel : UserControl
    {
        private const int CardGap = 12;
        private const int BodyPadding = 14;
        private const int HeaderHeight = 38;
        private const int ChipGap = 6;

        private static readonly HashSet<string> AutoLoadAttempted =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly RepoStore _store;
        private RepoRecord _record;
        private readonly Panel _scroll;
        private readonly TableLayoutPanel _stack;
        private bool _loading;
        private string _error;
        private int _builtWidth = -1;

        public event EventHandler DetailsUpdated;

        public RepoAboutPanel(RepoRecord record, RepoStore store)
        {
            _store = store;
            _record = record;
            BackColor = UiTheme.Canvas;
            Dock = DockStyle.Fill;

            _scroll = new Panel();
            _scroll.Dock = DockStyle.Fill;
            _scroll.AutoScroll = true;
            _scroll.BackColor = UiTheme.Canvas;
            _scroll.Padding = new Padding(12, 12, 10, 12);
            Controls.Add(_scroll);

            _stack = new TableLayoutPanel();
            _stack.Dock = DockStyle.Top;
            _stack.AutoSize = true;
            _stack.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _stack.ColumnCount = 1;
            _stack.RowCount = 0;
            _stack.BackColor = UiTheme.Canvas;
            _stack.Margin = Padding.Empty;
            _stack.Padding = Padding.Empty;
            _stack.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            _stack.SizeChanged += StackSizeChanged;
            _scroll.Controls.Add(_stack);

            Rebuild();
        }

        public void Bind(RepoRecord record)
        {
            _record = record;
            _loading = false;
            _error = null;
            _builtWidth = -1;
            Rebuild();
        }

        // Fills the column automatically the first time a repository is opened in this
        // session, unless the data is already cached. Failures are silent and leave the
        // refresh link visible.
        public void EnsureDetailsLoaded()
        {
            if (!CanFetch || _record.DetailsLoaded || _loading)
            {
                return;
            }

            string key = _record.GetDisplayName();
            if (AutoLoadAttempted.Contains(key))
            {
                return;
            }

            AutoLoadAttempted.Add(key);
            StartDetailsLoad();
        }

        private bool CanFetch
        {
            get
            {
                return _record != null &&
                    !string.IsNullOrEmpty(_record.Owner) &&
                    !string.IsNullOrEmpty(_record.Name) &&
                    !string.Equals(_record.Owner, "local", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void StackSizeChanged(object sender, EventArgs e)
        {
            if (_stack.Width < 80)
            {
                return;
            }

            if (_stack.Width != _builtWidth)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            int width = _stack.Width;
            if (width < 80)
            {
                width = 256;
            }

            _builtWidth = width;

            _stack.SuspendLayout();
            try
            {
                List<Control> old = new List<Control>();
                foreach (Control control in _stack.Controls)
                {
                    old.Add(control);
                }

                _stack.Controls.Clear();
                _stack.RowStyles.Clear();
                _stack.RowCount = 0;
                foreach (Control control in old)
                {
                    control.Dispose();
                }

                AddCard(BuildAboutCard(width));
                AddCard(BuildReleasesCard(width));
                AddCard(BuildPackagesCard(width));
                AddCard(BuildContributorsCard(width));
            }
            finally
            {
                _stack.ResumeLayout(true);
            }
        }

        private void AddCard(CardPanel card)
        {
            _stack.RowCount++;
            _stack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            card.Dock = DockStyle.Top;
            _stack.Controls.Add(card, 0, _stack.RowCount - 1);
        }

        private CardPanel CreateCard(string title, out TableLayoutPanel body)
        {
            CardPanel card = new CardPanel();
            card.Title = title;
            card.Margin = new Padding(0, 0, 0, CardGap);

            body = new TableLayoutPanel();
            body.Dock = DockStyle.Top;
            body.AutoSize = true;
            body.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            body.ColumnCount = 1;
            body.RowCount = 0;
            body.GrowStyle = TableLayoutPanelGrowStyle.AddRows;
            body.BackColor = UiTheme.Surface;
            body.Padding = new Padding(BodyPadding, 10, BodyPadding, BodyPadding);
            body.Margin = Padding.Empty;
            card.Controls.Add(body, 0, 1);
            return card;
        }

        private CardPanel BuildAboutCard(int cardWidth)
        {
            TableLayoutPanel body;
            CardPanel card = CreateCard("About", out body);
            int width = ContentWidth(cardWidth);

            string description = _record == null || string.IsNullOrWhiteSpace(_record.Description)
                ? "No description provided."
                : _record.Description.Replace("\r", " ").Replace("\n", " ").Trim();
            AddWrappedLabel(body, description, UiTheme.BodyFont, UiTheme.Text, width, 8);

            if (_record != null && !string.IsNullOrEmpty(_record.Homepage))
            {
                AddRow(body, BuildHomepageLink(width));
            }

            if (_record != null && _record.Topics != null && _record.Topics.Count > 0)
            {
                AddRow(body, BuildTopics(_record.Topics, width));
                AddSpacer(body, 8);
            }

            AddWrappedLabel(body, BuildStatsText(), UiTheme.SmallFont, UiTheme.Muted, width, 8);

            if (_record != null && !string.IsNullOrEmpty(_record.Language))
            {
                AddRow(body, BuildLanguageRow());
                AddSpacer(body, 6);
            }

            string facts = BuildFactsText();
            if (facts.Length > 0)
            {
                AddWrappedLabel(body, facts, UiTheme.SmallFont, UiTheme.Muted, width, 8);
            }

            AddDetailsStatus(body, width);
            return card;
        }

        private Control BuildHomepageLink(int width)
        {
            LinkLabel homepage = new LinkLabel();
            homepage.Text = _record.Homepage;
            homepage.Font = UiTheme.BodyFont;
            homepage.LinkColor = UiTheme.Accent;
            homepage.ActiveLinkColor = UiTheme.Accent;
            homepage.VisitedLinkColor = UiTheme.Accent;
            homepage.AutoSize = false;
            homepage.AutoEllipsis = true;
            homepage.Height = 20;
            homepage.Width = width;
            homepage.Margin = new Padding(0, 0, 0, 8);
            homepage.Dock = DockStyle.Top;
            homepage.BackColor = UiTheme.Surface;
            homepage.LinkBehavior = LinkBehavior.HoverUnderline;
            string url = _record.Homepage;
            homepage.LinkClicked += delegate { UiTheme.OpenUrl(url); };
            return homepage;
        }

        // Topic chips are laid out manually: a FlowLayoutPanel with AutoSize and wrapping
        // collapses to one line inside a TableLayoutPanel cell.
        private Control BuildTopics(List<string> topics, int width)
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Top;
            host.BackColor = UiTheme.Surface;
            host.Margin = new Padding(0, 0, 0, 2);
            host.Width = width;

            int x = 0;
            int y = 0;
            int rowHeight = 0;
            foreach (string topic in topics)
            {
                if (string.IsNullOrEmpty(topic))
                {
                    continue;
                }

                Label chip = new Label();
                chip.Text = topic;
                chip.Font = UiTheme.SmallFont;
                chip.ForeColor = UiTheme.Accent;
                chip.BackColor = UiTheme.ChipBackground;
                chip.AutoSize = false;
                chip.AutoEllipsis = true;
                chip.UseMnemonic = false;

                Size textSize = TextRenderer.MeasureText(
                    topic,
                    UiTheme.SmallFont,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                int chipWidth = Math.Min(width, textSize.Width + 16);
                int chipHeight = Math.Max(UiTheme.SmallFont.Height + 8, textSize.Height + 8);

                if (x > 0 && x + chipWidth > width)
                {
                    x = 0;
                    y += rowHeight + ChipGap;
                    rowHeight = 0;
                }

                chip.Location = new Point(x, y);
                chip.Size = new Size(chipWidth, chipHeight);
                host.Controls.Add(chip);

                x += chipWidth + ChipGap;
                rowHeight = Math.Max(rowHeight, chipHeight);
            }

            host.Height = y + rowHeight;
            return host;
        }

        private Control BuildLanguageRow()
        {
            Panel row = new Panel();
            row.Dock = DockStyle.Top;
            row.Height = 22;
            row.BackColor = UiTheme.Surface;
            row.Margin = new Padding(0, 0, 0, 0);

            Panel dot = new Panel();
            dot.Size = new Size(10, 10);
            dot.Location = new Point(0, 6);
            dot.BackColor = Display.LanguageColor(_record.Language);
            row.Controls.Add(dot);

            Label label = new Label();
            label.Text = _record.Language;
            label.Font = UiTheme.SmallFont;
            label.ForeColor = UiTheme.Muted;
            label.AutoSize = false;
            label.UseMnemonic = false;
            label.BackColor = UiTheme.Surface;
            Size textSize = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            label.Size = new Size(textSize.Width + 2, 20);
            label.Location = new Point(16, 1);
            row.Controls.Add(label);
            return row;
        }

        private string BuildStatsText()
        {
            if (_record == null)
            {
                return string.Empty;
            }

            return Display.Count(_record.Stars) + " stars   \u00b7   " +
                Display.Count(_record.Forks) + " forks   \u00b7   " +
                Display.Count(_record.Watchers) + " watching   \u00b7   " +
                Display.Count(_record.OpenIssues) + " issues";
        }

        private string BuildFactsText()
        {
            string facts = string.Empty;
            if (_record == null)
            {
                return facts;
            }

            if (!string.IsNullOrEmpty(_record.LicenseName))
            {
                facts = _record.LicenseName;
            }

            string branch = string.IsNullOrEmpty(_record.Branch) ? _record.DefaultBranch : _record.Branch;
            if (!string.IsNullOrEmpty(branch) && !string.Equals(branch, "default", StringComparison.OrdinalIgnoreCase))
            {
                if (facts.Length > 0)
                {
                    facts += "   \u00b7   ";
                }

                facts += "branch " + branch;
            }

            return facts;
        }

        private void AddDetailsStatus(TableLayoutPanel body, int width)
        {
            string status;
            Color statusColor = UiTheme.Faint;
            if (_loading)
            {
                status = "Loading details...";
            }
            else if (!string.IsNullOrEmpty(_error))
            {
                status = "Could not load details.";
                statusColor = UiTheme.Muted;
            }
            else if (_record != null && _record.DetailsLoaded)
            {
                status = "Updated " + Display.RelativeTime(_record.DetailsUpdatedAtUtc) + ".";
            }
            else if (CanFetch)
            {
                status = "Releases and contributors are not cached yet.";
            }
            else
            {
                status = "This offline repository has no GitHub details.";
            }

            AddWrappedLabel(body, status, UiTheme.SmallFont, statusColor, width, 4);

            if (!CanFetch)
            {
                return;
            }

            LinkLabel refresh = new LinkLabel();
            refresh.Text = _loading ? "Refreshing..." : "Refresh details";
            refresh.Font = UiTheme.SmallFont;
            refresh.LinkColor = UiTheme.Accent;
            refresh.ActiveLinkColor = UiTheme.Accent;
            refresh.VisitedLinkColor = UiTheme.Accent;
            refresh.LinkBehavior = LinkBehavior.HoverUnderline;
            refresh.AutoSize = true;
            refresh.Margin = Padding.Empty;
            refresh.BackColor = UiTheme.Surface;
            refresh.Enabled = !_loading;
            refresh.LinkClicked += delegate { StartDetailsLoad(); };
            UiTheme.FitLabel(refresh);
            AddRow(body, refresh);
        }

        private CardPanel BuildReleasesCard(int cardWidth)
        {
            TableLayoutPanel body;
            CardPanel card = CreateCard("Releases", out body);
            int width = ContentWidth(cardWidth);

            if (_record == null || _record.Releases == null || _record.Releases.Count == 0)
            {
                AddWrappedLabel(body, ReleasesPlaceholder(), UiTheme.SmallFont, UiTheme.Muted, width, 8);
            }
            else
            {
                ReleaseList list = new ReleaseList();
                list.Releases = _record.Releases;
                list.Width = width;
                list.Height = ReleaseList.RowHeight * _record.Releases.Count;
                list.Margin = new Padding(0, 0, 0, 6);
                list.Dock = DockStyle.Top;
                AddRow(body, list);
            }

            if (CanFetch)
            {
                LinkLabel all = new LinkLabel();
                all.Text = "View all releases on GitHub";
                all.Font = UiTheme.SmallFont;
                all.LinkColor = UiTheme.Accent;
                all.ActiveLinkColor = UiTheme.Accent;
                all.VisitedLinkColor = UiTheme.Accent;
                all.LinkBehavior = LinkBehavior.HoverUnderline;
                all.AutoSize = true;
                all.Margin = Padding.Empty;
                all.BackColor = UiTheme.Surface;
                string url = _record.HtmlUrl + "/releases";
                all.LinkClicked += delegate { UiTheme.OpenUrl(url); };
                UiTheme.FitLabel(all);
                AddRow(body, all);
            }

            return card;
        }

        private CardPanel BuildPackagesCard(int cardWidth)
        {
            TableLayoutPanel body;
            CardPanel card = CreateCard("Packages", out body);
            int width = ContentWidth(cardWidth);

            if (_record != null && _record.Packages != null && _record.Packages.Count > 0)
            {
                foreach (string package in _record.Packages)
                {
                    AddWrappedLabel(body, package, UiTheme.SmallFont, UiTheme.Muted, width, 6);
                }
            }
            else
            {
                AddWrappedLabel(body, PackagesPlaceholder(), UiTheme.SmallFont, UiTheme.Muted, width, 0);
            }

            return card;
        }

        private string ReleasesPlaceholder()
        {
            if (_loading)
            {
                return "Loading releases...";
            }

            if (CanFetch && _record != null && !_record.DetailsLoaded)
            {
                return "Releases are not cached yet.";
            }

            return "No releases published.";
        }

        private string PackagesPlaceholder()
        {
            if (_loading)
            {
                return "Loading packages...";
            }

            if (CanFetch && _record != null && !_record.DetailsLoaded)
            {
                return "Packages are not cached yet.";
            }

            return "No packages published.";
        }

        private CardPanel BuildContributorsCard(int cardWidth)
        {
            TableLayoutPanel body;
            CardPanel card = CreateCard("Contributors", out body);
            int width = ContentWidth(cardWidth);

            if (_record == null || _record.Contributors == null || _record.Contributors.Count == 0)
            {
                string message;
                if (_loading)
                {
                    message = "Loading contributors...";
                }
                else if (_record != null && _record.DetailsLoaded)
                {
                    message = "No contributors listed.";
                }
                else
                {
                    message = "Contributors are not cached yet.";
                }

                AddWrappedLabel(body, message, UiTheme.SmallFont, UiTheme.Muted, width, 0);
            }
            else
            {
                ContributorList list = new ContributorList();
                list.Contributors = _record.Contributors;
                list.Width = width;
                list.Height = ContributorList.RowHeight * _record.Contributors.Count;
                list.Margin = Padding.Empty;
                list.Dock = DockStyle.Top;
                AddRow(body, list);
            }

            return card;
        }

        private void StartDetailsLoad()
        {
            if (_loading)
            {
                return;
            }

            RepoRecord target = _record;
            if (target == null || string.IsNullOrEmpty(target.Owner) || string.IsNullOrEmpty(target.Name))
            {
                _error = "This repository has no GitHub details.";
                Rebuild();
                return;
            }

            _loading = true;
            _error = null;
            Rebuild();

            string token = _store.Settings == null ? string.Empty : _store.Settings.GitHubToken;
            GitHubDownloader downloader = new GitHubDownloader(_store, token);

            ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null;
                try
                {
                    downloader.FetchDetailsAsync(target, CancellationToken.None).Wait();
                }
                catch (AggregateException aggregate)
                {
                    Exception inner = aggregate.InnerException != null ? aggregate.InnerException : aggregate;
                    error = inner.Message;
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                try
                {
                    if (IsDisposed || !IsHandleCreated)
                    {
                        return;
                    }

                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed)
                        {
                            return;
                        }

                        _loading = false;
                        _error = error;
                        Rebuild();

                        if (!ReferenceEquals(_record, target))
                        {
                            return;
                        }

                        EventHandler handler = DetailsUpdated;
                        if (handler != null)
                        {
                            handler(this, EventArgs.Empty);
                        }
                    });
                }
                catch
                {
                    // The view was closed while the request was in flight.
                }
            });
        }

        private static int ContentWidth(int cardWidth)
        {
            // Two extra pixels of slack stop the card's preferred width from forcing a
            // horizontal scrollbar when the vertical scrollbar is showing.
            int width = cardWidth - (BodyPadding * 2) - 4;
            return width < 60 ? 60 : width;
        }

        private void AddWrappedLabel(
            TableLayoutPanel body,
            string text,
            Font font,
            Color color,
            int width,
            int bottomMargin)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = UiTheme.Surface;
            label.AutoSize = false;
            label.Dock = DockStyle.Top;
            label.Width = width;
            label.Height = Math.Max(font.Height + 2, TextRenderer.MeasureText(
                text,
                font,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding).Height + 2);
            label.Margin = new Padding(0, 0, 0, bottomMargin);
            label.UseMnemonic = false;
            AddRow(body, label);
        }

        private static void AddSpacer(TableLayoutPanel body, int height)
        {
            Panel spacer = new Panel();
            spacer.Dock = DockStyle.Top;
            spacer.Height = height;
            spacer.Margin = Padding.Empty;
            spacer.BackColor = UiTheme.Surface;
            AddRow(body, spacer);
        }

        private static void AddRow(TableLayoutPanel body, Control control)
        {
            body.RowCount++;
            body.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            body.Controls.Add(control, 0, body.RowCount - 1);
        }

        private sealed class CardPanel : TableLayoutPanel
        {
            private string _title = string.Empty;

            public string Title
            {
                get { return _title; }
                set
                {
                    _title = value ?? string.Empty;
                    Invalidate();
                }
            }

            public CardPanel()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.UserPaint |
                    ControlStyles.ResizeRedraw,
                    true);
                BackColor = UiTheme.Surface;
                ColumnCount = 1;
                RowCount = 2;
                ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                RowStyles.Add(new RowStyle(SizeType.Absolute, HeaderHeight));
                RowStyles.Add(new RowStyle(SizeType.AutoSize));
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                Margin = new Padding(0, 0, 0, CardGap);
                Padding = new Padding(1);
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                UpdateRegion();
            }

            protected override void OnHandleCreated(EventArgs e)
            {
                base.OnHandleCreated(e);
                UpdateRegion();
            }

            private void UpdateRegion()
            {
                if (Width <= 0 || Height <= 0)
                {
                    return;
                }

                using (GraphicsPath path = RoundedRectangle(new Rectangle(0, 0, Width, Height), 8))
                {
                    Region old = Region;
                    Region = new Region(path);
                    if (old != null)
                    {
                        old.Dispose();
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics graphics = e.Graphics;
                graphics.Clear(UiTheme.Canvas);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                using (GraphicsPath path = RoundedRectangle(bounds, 8))
                using (SolidBrush fill = new SolidBrush(UiTheme.Surface))
                using (Pen border = new Pen(UiTheme.Border))
                {
                    graphics.FillPath(fill, path);
                    graphics.DrawPath(border, path);
                }

                using (Pen separator = new Pen(UiTheme.Border))
                {
                    graphics.DrawLine(separator, 1, HeaderHeight + 1, Width - 2, HeaderHeight + 1);
                }

                TextRenderer.DrawText(
                    graphics,
                    _title,
                    UiTheme.CardTitleFont,
                    new Rectangle(BodyPadding + 1, 1, Width - (BodyPadding * 2) - 2, HeaderHeight),
                    UiTheme.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
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

        // Releases rendered like GitHub's release list: tag pill, title and publish date.
        private sealed class ReleaseList : Control
        {
            public const int RowHeight = 48;

            private List<ReleaseInfo> _releases = new List<ReleaseInfo>();

            public List<ReleaseInfo> Releases
            {
                get { return _releases; }
                set
                {
                    _releases = value ?? new List<ReleaseInfo>();
                    Invalidate();
                }
            }

            public ReleaseList()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.UserPaint |
                    ControlStyles.ResizeRedraw,
                    true);
                BackColor = UiTheme.Surface;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                int index = e.Y / RowHeight;
                if (index >= 0 && index < _releases.Count)
                {
                    ReleaseInfo release = _releases[index];
                    if (!string.IsNullOrEmpty(release.HtmlUrl))
                    {
                        UiTheme.OpenUrl(release.HtmlUrl);
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics graphics = e.Graphics;
                graphics.Clear(UiTheme.Surface);

                int y = 0;
                foreach (ReleaseInfo release in _releases)
                {
                    string tag = string.IsNullOrEmpty(release.TagName) ? "release" : release.TagName;
                    Font tagFont = UiTheme.SmallFont;
                    Size tagSize = TextRenderer.MeasureText(
                        tag,
                        tagFont,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                    int tagWidth = Math.Min(Width, tagSize.Width + 14);
                    Rectangle pill = new Rectangle(0, y + 2, tagWidth, 20);

                    using (GraphicsPath path = RoundedRectangle(pill, 9))
                    using (Pen pen = new Pen(UiTheme.Green))
                    {
                        graphics.SmoothingMode = SmoothingMode.AntiAlias;
                        graphics.DrawPath(pen, path);
                    }

                    TextRenderer.DrawText(
                        graphics,
                        tag,
                        tagFont,
                        pill,
                        UiTheme.Green,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    string title = release.GetTitle();
                    int titleLeft = pill.Right + 8;
                    Rectangle titleRect = new Rectangle(
                        titleLeft,
                        y + 1,
                        Math.Max(20, Width - titleLeft),
                        20);
                    TextRenderer.DrawText(
                        graphics,
                        title,
                        UiTheme.SmallFont,
                        titleRect,
                        UiTheme.Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    string when = Display.RelativeTime(release.PublishedAtUtc);
                    if (release.IsPrerelease)
                    {
                        when += "   \u00b7   pre-release";
                    }

                    TextRenderer.DrawText(
                        graphics,
                        when,
                        UiTheme.SmallFont,
                        new Rectangle(0, y + 24, Width, 18),
                        UiTheme.Faint,
                        TextFormatFlags.Left | TextFormatFlags.Top |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    y += RowHeight;
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

        // Contributors rendered as initials avatars with their contribution count.
        private sealed class ContributorList : Control
        {
            public const int RowHeight = 34;

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

            private List<ContributorInfo> _contributors = new List<ContributorInfo>();

            public List<ContributorInfo> Contributors
            {
                get { return _contributors; }
                set
                {
                    _contributors = value ?? new List<ContributorInfo>();
                    Invalidate();
                }
            }

            public ContributorList()
            {
                SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.UserPaint |
                    ControlStyles.ResizeRedraw,
                    true);
                BackColor = UiTheme.Surface;
                Cursor = Cursors.Hand;
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button != MouseButtons.Left)
                {
                    return;
                }

                int index = e.Y / RowHeight;
                if (index >= 0 && index < _contributors.Count)
                {
                    ContributorInfo contributor = _contributors[index];
                    if (!string.IsNullOrEmpty(contributor.HtmlUrl))
                    {
                        UiTheme.OpenUrl(contributor.HtmlUrl);
                    }
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics graphics = e.Graphics;
                graphics.Clear(UiTheme.Surface);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;

                int y = 0;
                foreach (ContributorInfo contributor in _contributors)
                {
                    Rectangle circle = new Rectangle(0, y + 5, 24, 24);
                    using (SolidBrush brush = new SolidBrush(AvatarColor(contributor.Login)))
                    {
                        graphics.FillEllipse(brush, circle);
                    }

                    TextRenderer.DrawText(
                        graphics,
                        Initials(contributor.Login),
                        UiTheme.SmallFont,
                        circle,
                        Color.White,
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPrefix);

                    Rectangle nameRect = new Rectangle(32, y + 4, Math.Max(20, Width - 32 - 44), 26);
                    TextRenderer.DrawText(
                        graphics,
                        contributor.Login,
                        UiTheme.BodyFont,
                        nameRect,
                        UiTheme.Text,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                    TextRenderer.DrawText(
                        graphics,
                        Display.Count(contributor.Contributions),
                        UiTheme.SmallFont,
                        new Rectangle(Width - 44, y + 4, 44, 26),
                        UiTheme.Faint,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter |
                        TextFormatFlags.NoPrefix);

                    y += RowHeight;
                }
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

                    int index = (hash & 0x7FFFFFFF) % AvatarColors.Length;
                    return AvatarColors[index];
                }
            }

            private static string Initials(string login)
            {
                if (string.IsNullOrEmpty(login))
                {
                    return "?";
                }

                string[] parts = login.Split(new char[] { '-', '_', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    return (parts[0].Substring(0, 1) + parts[1].Substring(0, 1)).ToUpperInvariant();
                }

                if (login.Length >= 2)
                {
                    return login.Substring(0, 2).ToUpperInvariant();
                }

                return login.ToUpperInvariant();
            }
        }
    }
}
