using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Rendering;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // Shared frame for the signed-in GitHub sections (Issues, Projects). It owns
    // the toolbar, the embedded browser and the og:// navigation contract, while
    // the concrete views decide what to load and which page to render.
    public abstract class GitHubSectionView : UserControl
    {
        private readonly RepoStore _store;
        private readonly string _filePrefix;
        private readonly Label _titleLabel;
        private readonly Label _statusLabel;
        private readonly Button _refreshButton;
        private readonly LinkLabel _signInLink;
        private readonly LinkLabel _githubLink;
        private readonly WebBrowser _browser;
        private readonly string _sessionId;
        private int _renderCounter;
        private string _lastRenderFile;
        private string _previousRenderFile;
        private bool _loaded;
        private bool _busy;
        private string _githubUrl;
        private string _lastHtml;

        protected GitHubSectionView(RepoStore store, RepoRecord record, string title, string filePrefix)
        {
            _store = store;
            Record = record;
            _filePrefix = filePrefix;
            _sessionId = Guid.NewGuid().ToString("N");
            BackColor = UiTheme.Canvas;
            Dock = DockStyle.Fill;
            Font = UiTheme.BodyFont;

            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 56;
            toolbar.BackColor = UiTheme.Surface;
            toolbar.Padding = new Padding(18, 10, 16, 8);
            toolbar.Paint += ToolbarPaint;
            Controls.Add(toolbar);

            TableLayoutPanel toolbarLayout = new TableLayoutPanel();
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.ColumnCount = 2;
            toolbarLayout.RowCount = 1;
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            toolbarLayout.Margin = Padding.Empty;
            toolbarLayout.BackColor = UiTheme.Surface;
            toolbar.Controls.Add(toolbarLayout);

            TableLayoutPanel labels = new TableLayoutPanel();
            labels.Dock = DockStyle.Fill;
            labels.ColumnCount = 1;
            labels.RowCount = 2;
            labels.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            labels.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            labels.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            labels.Margin = Padding.Empty;
            labels.BackColor = UiTheme.Surface;
            toolbarLayout.Controls.Add(labels, 0, 0);

            _titleLabel = new Label();
            _titleLabel.Text = title;
            _titleLabel.Dock = DockStyle.Fill;
            _titleLabel.Margin = Padding.Empty;
            _titleLabel.Font = UiTheme.BodyBold;
            _titleLabel.ForeColor = UiTheme.Text;
            _titleLabel.AutoSize = false;
            _titleLabel.AutoEllipsis = true;
            _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            _titleLabel.UseMnemonic = false;
            _titleLabel.BackColor = UiTheme.Surface;
            labels.Controls.Add(_titleLabel, 0, 0);

            _statusLabel = new Label();
            _statusLabel.Text = string.Empty;
            _statusLabel.Dock = DockStyle.Fill;
            _statusLabel.Margin = Padding.Empty;
            _statusLabel.Font = UiTheme.SmallFont;
            _statusLabel.ForeColor = UiTheme.Muted;
            _statusLabel.AutoSize = false;
            _statusLabel.AutoEllipsis = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            _statusLabel.UseMnemonic = false;
            _statusLabel.BackColor = UiTheme.Surface;
            labels.Controls.Add(_statusLabel, 0, 1);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.AutoSize = true;
            actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actions.Anchor = AnchorStyles.Right;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.Margin = Padding.Empty;
            actions.BackColor = UiTheme.Surface;
            toolbarLayout.Controls.Add(actions, 1, 0);

            _refreshButton = UiTheme.MakeButton("Refresh", ButtonStyle.Secondary);
            _refreshButton.Size = new Size(88, 30);
            _refreshButton.Margin = new Padding(12, 4, 0, 0);
            _refreshButton.Click += delegate { RefreshClicked(); };
            actions.Controls.Add(_refreshButton);

            _signInLink = MakeToolbarLink("Sign in");
            _signInLink.Margin = new Padding(12, 8, 0, 0);
            _signInLink.LinkClicked += delegate { OnSignInRequested(); };
            actions.Controls.Add(_signInLink);

            _githubLink = MakeToolbarLink("Open on GitHub");
            _githubLink.Margin = new Padding(12, 8, 0, 0);
            _githubLink.Visible = false;
            _githubLink.LinkClicked += delegate
            {
                if (!string.IsNullOrEmpty(_githubUrl))
                {
                    UiTheme.OpenUrl(_githubUrl);
                }
            };
            actions.Controls.Add(_githubLink);

            _browser = new WebBrowser();
            _browser.Dock = DockStyle.Fill;
            _browser.ScriptErrorsSuppressed = true;
            _browser.AllowWebBrowserDrop = false;
            _browser.IsWebBrowserContextMenuEnabled = false;
            _browser.WebBrowserShortcutsEnabled = false;
            _browser.Margin = Padding.Empty;
            _browser.Navigating += BrowserNavigating;
            Controls.Add(_browser);
            _browser.BringToFront();

            SetStatus("Choose this tab to load from GitHub.");
            UpdateToolbar();
        }

        // Raised when a page link (or the toolbar) asks for the sign-in dialog.
        public event EventHandler SignInRequested;

        // Raised by project cards that point at an issue in this repository.
        public event Action<int> IssueRequested;

        // Raised by Markdown links that point at a file inside the snapshot.
        public event Action<string> BrowseRequested;

        public RepoRecord Record { get; protected set; }

        protected RepoStore Store
        {
            get { return _store; }
        }

        protected bool IsSignedIn
        {
            get { return _store.Settings != null && _store.Settings.IsSignedIn; }
        }

        protected string AccessToken
        {
            get { return _store.Settings == null ? string.Empty : _store.Settings.GitHubToken; }
        }

        protected bool IsBusy
        {
            get { return _busy; }
        }

        // The HTML last sent to the embedded browser. Used by the diagnostics to
        // verify sign-in, offline and navigation states without screen scraping.
        public string LastHtml
        {
            get { return _lastHtml; }
        }

        public void Bind(RepoRecord record)
        {
            Record = record;
            _loaded = false;
            SetBusy(false);
            UpdateToolbar();
            OnBind(record);
        }

        // Called when the hosting tab first becomes visible.
        public void EnsureLoaded()
        {
            if (_loaded)
            {
                return;
            }

            _loaded = true;
            OnEnsureLoaded();
        }

        // Called by the toolbar, an og://refresh link, or after a sign-in.
        public void Reload()
        {
            if (_busy)
            {
                return;
            }

            OnReload();
        }

        public void NotifyAccountChanged()
        {
            _loaded = true;
            UpdateToolbar();
            OnAccountChanged();
        }

        // Re-draws the pages after the light/dark theme changes without requiring
        // the tab to be reopened.
        public void RefreshTheme()
        {
            OnThemeChanged();
        }

        protected void SetTitle(string title)
        {
            if (_titleLabel != null)
            {
                _titleLabel.Text = title ?? string.Empty;
            }
        }

        protected void SetStatus(string text)
        {
            if (_statusLabel != null)
            {
                _statusLabel.Text = text ?? string.Empty;
            }
        }

        protected void SetBusy(bool busy)
        {
            _busy = busy;
            if (_refreshButton != null)
            {
                _refreshButton.Enabled = !busy;
                _refreshButton.Text = busy ? "Loading..." : "Refresh";
            }
        }

        protected void SetGithubUrl(string url)
        {
            _githubUrl = url ?? string.Empty;
            if (_githubLink != null)
            {
                _githubLink.Visible = !string.IsNullOrEmpty(_githubUrl);
            }
        }

        protected void UpdateToolbar()
        {
            if (_signInLink != null)
            {
                _signInLink.Visible = !IsSignedIn;
            }

            if (_refreshButton != null)
            {
                _refreshButton.Visible = true;
            }
        }

        protected void OnSignInRequested()
        {
            EventHandler handler = SignInRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        protected void OnIssueRequested(int number)
        {
            Action<int> handler = IssueRequested;
            if (handler != null)
            {
                handler(number);
            }
        }

        protected void RaiseBrowse(string path)
        {
            Action<string> handler = BrowseRequested;
            if (handler != null)
            {
                handler(path);
            }
        }

        protected void RenderHtml(string html)
        {
            _lastHtml = html;
            try
            {
                _previousRenderFile = _lastRenderFile;
                _renderCounter++;
                string file = Path.Combine(
                    _store.RenderPath,
                    _filePrefix + "-" + _sessionId + "-" + _renderCounter.ToString() + ".html");
                File.WriteAllText(file, html, new UTF8Encoding(false));
                _lastRenderFile = file;
                _browser.Navigate(new Uri(file));
            }
            catch (Exception ex)
            {
                try
                {
                    _browser.DocumentText = SiteBuilder.BuildErrorPage(
                        "Could not render this page",
                        ex.Message);
                }
                catch
                {
                }
            }
        }

        protected abstract void OnEnsureLoaded();

        protected abstract void OnReload();

        protected abstract void OnBind(RepoRecord record);

        // Return true when the og:// host was handled.
        protected abstract bool OnAppUrl(string host, Uri uri);

        protected virtual void OnAccountChanged()
        {
            if (Visible && !_loaded)
            {
                EnsureLoaded();
                return;
            }

            if (Visible)
            {
                OnReload();
            }
        }

        protected virtual void OnThemeChanged()
        {
        }

        // Dispatches one og:// address. Shared hosts (sign-in, refresh and
        // repository files) are handled here; the concrete section handles its
        // own pages through OnAppUrl.
        internal bool HandleAppUrl(Uri uri)
        {
            if (uri == null)
            {
                return false;
            }

            string host = uri.Host.ToLowerInvariant();
            if (host == "signin")
            {
                OnSignInRequested();
                return true;
            }

            if (host == "refresh")
            {
                Reload();
                return true;
            }

            if (host == "browse" || host == "openfile")
            {
                RaiseBrowse(GetQueryValue(uri, "path"));
                return true;
            }

            return OnAppUrl(host, uri);
        }

        protected static string GetQueryValue(Uri uri, string key)
        {
            if (uri == null || string.IsNullOrEmpty(uri.Query))
            {
                return string.Empty;
            }

            string query = uri.Query;
            if (query.StartsWith("?", StringComparison.Ordinal))
            {
                query = query.Substring(1);
            }

            string[] parts = query.Split('&');
            foreach (string part in parts)
            {
                int equals = part.IndexOf('=');
                string name = equals < 0 ? part : part.Substring(0, equals);
                if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string value = equals < 0 ? string.Empty : part.Substring(equals + 1);
                try
                {
                    return Uri.UnescapeDataString(value.Replace("+", " "));
                }
                catch
                {
                    return value;
                }
            }

            return string.Empty;
        }

        protected static bool IsLocal(RepoRecord record)
        {
            return record == null ||
                string.Equals(record.Owner, "local", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(record.HtmlUrl);
        }

        private void RefreshClicked()
        {
            if (!IsSignedIn)
            {
                OnSignInRequested();
                return;
            }

            Reload();
        }

        private static LinkLabel MakeToolbarLink(string text)
        {
            LinkLabel link = new LinkLabel();
            link.Text = text;
            link.Font = UiTheme.BodyFont;
            link.LinkColor = UiTheme.Accent;
            link.ActiveLinkColor = UiTheme.Accent;
            link.VisitedLinkColor = UiTheme.Accent;
            link.LinkBehavior = LinkBehavior.HoverUnderline;
            link.AutoSize = true;
            link.Margin = Padding.Empty;
            link.BackColor = UiTheme.Surface;
            return link;
        }

        private void ToolbarPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(UiTheme.Border))
            {
                e.Graphics.DrawLine(pen, 0, ((Control)sender).Height - 1, ((Control)sender).Width, ((Control)sender).Height - 1);
            }
        }

        private void BrowserNavigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            if (e.Url == null)
            {
                return;
            }

            string scheme = e.Url.Scheme.ToLowerInvariant();
            if (scheme == "og")
            {
                e.Cancel = true;
                HandleAppUrl(e.Url);
                return;
            }

            if (scheme == "file")
            {
                string localPath = e.Url.LocalPath;
                if (PathResolver.IsInside(_store.RenderPath, localPath))
                {
                    if (_previousRenderFile != null &&
                        !string.Equals(_previousRenderFile, _lastRenderFile, StringComparison.OrdinalIgnoreCase))
                    {
                        TryDeleteFile(_previousRenderFile);
                    }

                    return;
                }

                e.Cancel = true;
                return;
            }

            if (scheme == "http" || scheme == "https" || scheme == "mailto")
            {
                e.Cancel = true;
                UiTheme.OpenUrl(e.Url.AbsoluteUri);
                return;
            }

            e.Cancel = true;
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private void DeleteSessionFiles()
        {
            try
            {
                string pattern = _filePrefix + "-" + _sessionId + "-*.html";
                foreach (string file in Directory.GetFiles(_store.RenderPath, pattern))
                {
                    TryDeleteFile(file);
                }
            }
            catch
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _browser.Stop();
                    _browser.Dispose();
                }
                catch
                {
                }

                DeleteSessionFiles();
            }

            base.Dispose(disposing);
        }
    }
}
