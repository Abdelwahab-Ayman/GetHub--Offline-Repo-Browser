using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Rendering;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Controls
{
    public class RepoView : UserControl
    {
        private readonly RepoStore _store;
        private RepoRecord _record;
        private readonly Label _nameLabel;
        private readonly Label _descriptionLabel;
        private readonly Label _metaLabel;
        private readonly Button _syncButton;
        private readonly Button _folderButton;
        private readonly Button _githubButton;
        private readonly TreeView _tree;
        private readonly WebBrowser _browser;
        private readonly RepoAboutPanel _aboutPanel;
        private readonly TableLayoutPanel _contentLayout;
        private readonly RepoTabStrip _tabStrip;
        private readonly RepoPlaceholderPanel _placeholder;
        private readonly RepoHistoryView _historyView;
        private readonly RepoIssuesView _issuesView;
        private readonly RepoProjectsView _projectsView;
        private readonly SplitContainer _split;
        private readonly ImageList _icons;
        private readonly string _sessionId;
        private int _renderCounter;
        private string _lastRenderFile;
        private string _previousRenderFile;
        private bool _syncingTree;
        private string _currentRelative;
        private bool _currentRaw;
        private string _fullName;
        private string _fullDescription;
        private float _nameSize;
        private bool _aboutVisible = true;

        private const int AboutColumnWidth = 268;
        private const int AboutMinContentWidth = 640;

        public event EventHandler SyncRequested;

        // Raised when the Issues or Projects page asks for the account dialog.
        public event EventHandler SignInRequested;

        public RepoRecord Record
        {
            get { return _record; }
        }

        public RepoView(RepoStore store, RepoRecord record)
        {
            _store = store;
            _record = record;
            _sessionId = Guid.NewGuid().ToString("N");
            _currentRelative = string.Empty;
            BackColor = UiTheme.Canvas;
            Dock = DockStyle.Fill;
            Font = UiTheme.BodyFont;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 124F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, RepoTabStrip.PreferredHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.BackColor = UiTheme.Canvas;
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.Margin = Padding.Empty;
            header.BackColor = UiTheme.Surface;
            header.Padding = new Padding(22, 12, 22, 8);
            root.Controls.Add(header, 0, 0);

            TableLayoutPanel headerLayout = new TableLayoutPanel();
            headerLayout.Dock = DockStyle.Fill;
            headerLayout.ColumnCount = 2;
            headerLayout.RowCount = 1;
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            headerLayout.BackColor = UiTheme.Surface;
            header.Controls.Add(headerLayout);

            TableLayoutPanel labels = new TableLayoutPanel();
            labels.Dock = DockStyle.Fill;
            labels.Margin = Padding.Empty;
            labels.ColumnCount = 1;
            labels.RowCount = 3;
            labels.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            labels.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            labels.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            labels.BackColor = UiTheme.Surface;
            headerLayout.Controls.Add(labels, 0, 0);

            _nameLabel = new Label();
            _nameLabel.Dock = DockStyle.Fill;
            _nameLabel.Margin = Padding.Empty;
            _nameLabel.Font = UiTheme.HeadingFont;
            _nameLabel.ForeColor = UiTheme.Text;
            _nameLabel.AutoSize = false;
            _nameLabel.AutoEllipsis = false;
            _nameLabel.TextAlign = ContentAlignment.MiddleLeft;
            _nameLabel.UseMnemonic = false;
            labels.Controls.Add(_nameLabel, 0, 0);

            _descriptionLabel = new Label();
            _descriptionLabel.Dock = DockStyle.Fill;
            _descriptionLabel.Margin = Padding.Empty;
            _descriptionLabel.Font = UiTheme.BodyFont;
            _descriptionLabel.ForeColor = UiTheme.Muted;
            _descriptionLabel.AutoSize = false;
            _descriptionLabel.AutoEllipsis = false;
            _descriptionLabel.TextAlign = ContentAlignment.TopLeft;
            _descriptionLabel.UseMnemonic = false;
            labels.Controls.Add(_descriptionLabel, 0, 1);

            _metaLabel = new Label();
            _metaLabel.Dock = DockStyle.Fill;
            _metaLabel.Margin = Padding.Empty;
            _metaLabel.Font = UiTheme.SmallFont;
            _metaLabel.ForeColor = UiTheme.Faint;
            _metaLabel.AutoSize = false;
            _metaLabel.AutoEllipsis = true;
            _metaLabel.TextAlign = ContentAlignment.MiddleLeft;
            _metaLabel.UseMnemonic = false;
            labels.Controls.Add(_metaLabel, 0, 2);

            labels.Resize += delegate { FitHeaderText(); };

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.AutoSize = true;
            actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            actions.Margin = new Padding(12, 2, 0, 0);
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.BackColor = UiTheme.Surface;
            headerLayout.Controls.Add(actions, 1, 0);

            _syncButton = UiTheme.MakeButton("Sync", ButtonStyle.Primary);
            _syncButton.Size = new Size(80, 32);
            _syncButton.Margin = new Padding(6, 0, 0, 0);
            _syncButton.Click += delegate { OnSyncRequested(); };
            actions.Controls.Add(_syncButton);

            _folderButton = UiTheme.MakeButton("Open folder", ButtonStyle.Secondary);
            _folderButton.Size = new Size(106, 32);
            _folderButton.Margin = new Padding(6, 0, 0, 0);
            _folderButton.Click += delegate { UiTheme.OpenFolder(_record.GetSnapshotPath()); };
            actions.Controls.Add(_folderButton);

            _githubButton = UiTheme.MakeButton("GitHub", ButtonStyle.Secondary);
            _githubButton.Size = new Size(82, 32);
            _githubButton.Margin = new Padding(6, 0, 0, 0);
            _githubButton.Click += delegate
            {
                if (!string.IsNullOrEmpty(_record.HtmlUrl))
                {
                    UiTheme.OpenUrl(_record.HtmlUrl);
                }
            };
            actions.Controls.Add(_githubButton);

            _tabStrip = new RepoTabStrip();
            _tabStrip.Dock = DockStyle.Fill;
            _tabStrip.Margin = Padding.Empty;
            _tabStrip.SelectedIndexChanged += delegate { ApplyTab(); };
            root.Controls.Add(_tabStrip, 0, 1);

            _split = new SplitContainer();
            SplitContainer split = _split;
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.SplitterWidth = 6;
            split.IsSplitterFixed = true;
            split.BorderStyle = BorderStyle.None;
            split.BackColor = UiTheme.Canvas;
            split.Panel1.BackColor = UiTheme.Surface;
            split.Panel2.BackColor = UiTheme.Surface;

            Panel tabContent = new Panel();
            tabContent.Dock = DockStyle.Fill;
            tabContent.Margin = Padding.Empty;
            tabContent.BackColor = UiTheme.Canvas;
            tabContent.Controls.Add(split);
            root.Controls.Add(tabContent, 0, 2);

            EventHandler splitHandler = null;
            splitHandler = delegate
            {
                try
                {
                    if (split.IsDisposed)
                    {
                        split.SizeChanged -= splitHandler;
                        split.HandleCreated -= splitHandler;
                        return;
                    }

                    if (split.Width <= 0)
                    {
                        return;
                    }

                    int width = split.Width - split.SplitterWidth;
                    if (width < 400)
                    {
                        return;
                    }

                    // Keep the file tree compact so the GitHub-style About column fits next to
                    // the markdown without making the content pane too narrow.
                    int desired = (int)(width * 0.24);
                    if (desired < 200)
                    {
                        desired = 200;
                    }

                    if (desired > 300)
                    {
                        desired = 300;
                    }

                    if (desired > width - 120)
                    {
                        desired = Math.Max(80, width - 120);
                    }

                    if (split.SplitterDistance != desired)
                    {
                        split.SplitterDistance = desired;
                    }
                }
                catch
                {
                }
            };

            split.HandleCreated += splitHandler;
            split.SizeChanged += splitHandler;

            TableLayoutPanel treeLayout = new TableLayoutPanel();
            treeLayout.Dock = DockStyle.Fill;
            treeLayout.ColumnCount = 1;
            treeLayout.RowCount = 2;
            treeLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            treeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            treeLayout.BackColor = UiTheme.Surface;
            split.Panel1.Controls.Add(treeLayout);

            Label filesLabel = new Label();
            filesLabel.Dock = DockStyle.Fill;
            filesLabel.Text = "FILES";
            filesLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            filesLabel.ForeColor = UiTheme.Faint;
            filesLabel.TextAlign = ContentAlignment.MiddleLeft;
            filesLabel.Padding = new Padding(14, 0, 0, 0);
            filesLabel.BackColor = UiTheme.Surface;
            treeLayout.Controls.Add(filesLabel, 0, 0);

            _icons = CreateIconList();
            _tree = new TreeView();
            _tree.Dock = DockStyle.Fill;
            _tree.BorderStyle = BorderStyle.None;
            _tree.BackColor = UiTheme.Surface;
            _tree.Font = UiTheme.BodyFont;
            _tree.ForeColor = UiTheme.Text;
            _tree.HideSelection = false;
            _tree.FullRowSelect = true;
            _tree.ShowLines = false;
            _tree.ShowRootLines = false;
            _tree.ShowPlusMinus = true;
            _tree.ItemHeight = 24;
            _tree.Indent = 16;
            _tree.ImageList = _icons;
            _tree.BeforeExpand += TreeBeforeExpand;
            _tree.AfterSelect += TreeAfterSelect;
            treeLayout.Controls.Add(_tree, 0, 1);

            _browser = new WebBrowser();
            _browser.Dock = DockStyle.Fill;
            _browser.ScriptErrorsSuppressed = true;
            _browser.AllowWebBrowserDrop = false;
            _browser.IsWebBrowserContextMenuEnabled = false;
            _browser.WebBrowserShortcutsEnabled = false;
            _browser.Margin = Padding.Empty;
            _browser.Navigating += BrowserNavigating;

            _contentLayout = new TableLayoutPanel();
            _contentLayout.Dock = DockStyle.Fill;
            _contentLayout.ColumnCount = 2;
            _contentLayout.RowCount = 1;
            _contentLayout.Margin = Padding.Empty;
            _contentLayout.Padding = Padding.Empty;
            _contentLayout.BackColor = UiTheme.Canvas;
            _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _contentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AboutColumnWidth));
            _contentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            _contentLayout.Controls.Add(_browser, 0, 0);

            _aboutPanel = new RepoAboutPanel(_record, _store);
            _aboutPanel.Margin = Padding.Empty;
            _aboutPanel.DetailsUpdated += delegate { UpdateHeader(); };
            _contentLayout.Controls.Add(_aboutPanel, 1, 0);

            split.Panel2.Controls.Add(_contentLayout);

            _placeholder = new RepoPlaceholderPanel();
            _placeholder.Dock = DockStyle.Fill;
            _placeholder.Margin = Padding.Empty;
            tabContent.Controls.Add(_placeholder);

            _historyView = new RepoHistoryView(_store, _record);
            _historyView.Dock = DockStyle.Fill;
            _historyView.Margin = Padding.Empty;
            _historyView.Visible = false;
            tabContent.Controls.Add(_historyView);

            _issuesView = new RepoIssuesView(_store, _record);
            _issuesView.Dock = DockStyle.Fill;
            _issuesView.Margin = Padding.Empty;
            _issuesView.Visible = false;
            _issuesView.SignInRequested += delegate { RaiseSignInRequested(); };
            _issuesView.BrowseRequested += delegate(string path) { NavigateToPath(path, false); };
            tabContent.Controls.Add(_issuesView);

            _projectsView = new RepoProjectsView(_store, _record);
            _projectsView.Dock = DockStyle.Fill;
            _projectsView.Margin = Padding.Empty;
            _projectsView.Visible = false;
            _projectsView.SignInRequested += delegate { RaiseSignInRequested(); };
            _projectsView.BrowseRequested += delegate(string path) { NavigateToPath(path, false); };
            _projectsView.IssueRequested += delegate(int number) { OpenIssue(number); };
            tabContent.Controls.Add(_projectsView);

            split.Panel2.SizeChanged += delegate { UpdateAboutVisibility(); };

            UpdateAboutVisibility();
            Load += delegate
            {
                UpdateAboutVisibility();
                _aboutPanel.EnsureDetailsLoaded();
            };

            UpdateHeader();
            ApplyTab();
            BuildTree();
            NavigateToPath(string.Empty, false);
        }

        public void UpdateRecord(RepoRecord record)
        {
            _record = record;
            _tabStrip.SelectedIndex = 0;
            _aboutPanel.Bind(record);
            _historyView.Bind(record);
            _issuesView.Bind(record);
            _projectsView.Bind(record);
            UpdateHeader();
            BuildTree();
            string target = _currentRelative;
            NavigateToPath(target, _currentRaw);
        }

        // Called after the account dialog or Settings changes the signed-in user.
        public void NotifyAccountChanged()
        {
            if (_issuesView != null)
            {
                _issuesView.NotifyAccountChanged();
            }

            if (_projectsView != null)
            {
                _projectsView.NotifyAccountChanged();
            }
        }

        public RepoIssuesView IssuesView
        {
            get { return _issuesView; }
        }

        public RepoProjectsView ProjectsView
        {
            get { return _projectsView; }
        }

        private void OpenIssue(int number)
        {
            if (_issuesView == null || number <= 0)
            {
                return;
            }

            if (_tabStrip.SelectedIndex != 1)
            {
                _tabStrip.SelectedIndex = 1;
            }

            _issuesView.OpenIssue(number);
        }

        private void RaiseSignInRequested()
        {
            EventHandler handler = SignInRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        // Switches between the offline file browser (Code) and the GitHub-only
        // sections. Issues and Projects render their own pages; the remaining
        // sections show a centered empty state with a link to github.com.
        private void ApplyTab()
        {
            if (_tabStrip == null || _split == null || _placeholder == null || _historyView == null)
            {
                return;
            }

            int index = _tabStrip.SelectedIndex;
            if (_issuesView != null)
            {
                _issuesView.Visible = false;
            }

            if (_projectsView != null)
            {
                _projectsView.Visible = false;
            }

            if (index == RepoTabStrip.HistoryIndex)
            {
                _placeholder.Visible = false;
                _split.Visible = false;
                _historyView.Visible = true;
                _historyView.BringToFront();
                _historyView.EnsureLoaded();
                return;
            }

            _historyView.Visible = false;
            if (index <= 0)
            {
                _placeholder.Visible = false;
                _split.Visible = true;
                _split.BringToFront();
                return;
            }

            if (index == 1)
            {
                _placeholder.Visible = false;
                _split.Visible = false;
                _issuesView.Visible = true;
                _issuesView.BringToFront();
                _issuesView.EnsureLoaded();
                return;
            }

            if (index == 4)
            {
                _placeholder.Visible = false;
                _split.Visible = false;
                _projectsView.Visible = true;
                _projectsView.BringToFront();
                _projectsView.EnsureLoaded();
                return;
            }

            _split.Visible = false;
            string url = GetTabUrl(index);
            string message = url.Length > 0
                ? RepoTabStrip.GetDescription(index)
                : "This repository was opened from a local folder and has no GitHub address, so its " +
                  RepoTabStrip.GetTitle(index).ToLowerInvariant() + " page is not available offline.";

            _placeholder.Bind(index, RepoTabStrip.GetTitle(index), message, url);
            _placeholder.Visible = true;
            _placeholder.BringToFront();
        }

        private string GetTabUrl(int index)
        {
            if (_record == null || string.IsNullOrEmpty(_record.HtmlUrl))
            {
                return string.Empty;
            }

            return _record.HtmlUrl.TrimEnd('/') + RepoTabStrip.GetPath(index);
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

                try
                {
                    _icons.Dispose();
                }
                catch
                {
                }

                DeleteSessionFiles();
            }

            base.Dispose(disposing);
        }

        private void UpdateAboutVisibility()
        {
            if (_contentLayout == null || _contentLayout.IsDisposed || _aboutPanel == null)
            {
                return;
            }

            int available = _contentLayout.ClientSize.Width;
            if (available <= 0)
            {
                return;
            }

            bool show = available >= AboutMinContentWidth;
            float columnWidth = _contentLayout.ColumnStyles[1].Width;
            if (show == _aboutVisible &&
                Math.Abs(columnWidth - (show ? AboutColumnWidth : 0F)) < 0.5F)
            {
                return;
            }

            _aboutVisible = show;
            _aboutPanel.Visible = show;
            _contentLayout.ColumnStyles[1].Width = show ? AboutColumnWidth : 0F;
        }

        private void UpdateHeader()
        {            string name = _record == null ? string.Empty : _record.GetDisplayName();
            _fullName = name;
            _fullDescription = _record == null || string.IsNullOrWhiteSpace(_record.Description)
                ? "No description provided."
                : _record.Description.Replace("\r", " ").Replace("\n", " ");

            if (_record == null)
            {
                _metaLabel.Text = string.Empty;
                FitHeaderText();
                return;
            }

            StringBuilder meta = new StringBuilder();
            meta.Append(Display.Count(_record.Stars));
            meta.Append(" stars  ·  ");
            meta.Append(Display.Count(_record.Forks));
            meta.Append(" forks  ·  ");
            meta.Append(string.IsNullOrEmpty(_record.Language) ? "Unknown language" : _record.Language);
            if (!string.IsNullOrEmpty(_record.LicenseName))
            {
                meta.Append("  ·  ");
                meta.Append(_record.LicenseName);
            }

            meta.Append("  ·  branch ");
            meta.Append(string.IsNullOrEmpty(_record.Branch) ? "default" : _record.Branch);
            meta.Append("  ·  synced ");
            meta.Append(Display.RelativeTime(_record.DownloadedAtUtc));
            _metaLabel.Text = meta.ToString();

            _githubButton.Enabled = !string.IsNullOrEmpty(_record.HtmlUrl);
            _syncButton.Enabled = true;
            _folderButton.Enabled = _record.HasSnapshot();
            FitHeaderText();
        }

        // Shrinks the repository name until it fits on one line and wraps the description
        // to at most two lines, so nothing in the header is ever clipped or hidden.
        private void FitHeaderText()
        {
            if (_fullName == null && _fullDescription == null)
            {
                return;
            }

            int width = _nameLabel.ClientSize.Width;
            if (width <= 0)
            {
                width = _nameLabel.Parent == null ? 400 : _nameLabel.Parent.Width;
            }

            if (width > 0 && _fullName != null)
            {
                float[] sizes = new float[] { 17F, 16F, 15F, 14F, 13F, 12F, 11F, 10F };
                float chosen = sizes[sizes.Length - 1];
                foreach (float size in sizes)
                {
                    using (Font candidate = new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Point))
                    {
                        int measured = TextRenderer.MeasureText(
                            _fullName,
                            candidate,
                            new Size(int.MaxValue, int.MaxValue),
                            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                        if (measured <= width)
                        {
                            chosen = size;
                            break;
                        }
                    }
                }

                if (Math.Abs(_nameSize - chosen) > 0.01F)
                {
                    _nameSize = chosen;
                    Font old = _nameLabel.Font;
                    _nameLabel.Font = chosen >= 17F
                        ? UiTheme.HeadingFont
                        : new Font("Segoe UI Semibold", chosen, FontStyle.Regular, GraphicsUnit.Point);
                    if (old != null && old != UiTheme.HeadingFont)
                    {
                        old.Dispose();
                    }
                }

                _nameLabel.Text = EllipsizeText(_fullName, _nameLabel.Font, width);
            }
            else if (_fullName != null)
            {
                _nameLabel.Text = _fullName;
            }

            int descriptionWidth = _descriptionLabel.ClientSize.Width;
            if (descriptionWidth <= 0)
            {
                descriptionWidth = width;
            }

            _descriptionLabel.Text = WrapDescription(_fullDescription, descriptionWidth, 2);
        }

        private static string EllipsizeText(string text, Font font, int width)
        {
            if (string.IsNullOrEmpty(text) || width <= 0)
            {
                return text;
            }

            int measured = TextRenderer.MeasureText(
                text,
                font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            if (measured <= width)
            {
                return text;
            }

            string value = text;
            while (value.Length > 1)
            {
                value = value.Substring(0, value.Length - 1);
                int candidate = TextRenderer.MeasureText(
                    value + "...",
                    font,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
                if (candidate <= width)
                {
                    return value + "...";
                }
            }

            return "...";
        }

        private static string WrapDescription(string text, int width, int maxLines)
        {
            if (string.IsNullOrEmpty(text) || width <= 8)
            {
                return text;
            }

            Font font = UiTheme.BodyFont;
            int lineHeight = TextRenderer.MeasureText("Ag", font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Height;
            int maxHeight = lineHeight * maxLines + 2;
            Size full = TextRenderer.MeasureText(
                text,
                font,
                new Size(width, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            if (full.Height <= maxHeight)
            {
                return text;
            }

            string[] words = text.Split(' ');
            int low = 0;
            int high = words.Length;
            string best = string.Empty;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                StringBuilder candidate = new StringBuilder();
                for (int i = 0; i < mid; i++)
                {
                    if (i > 0)
                    {
                        candidate.Append(' ');
                    }

                    candidate.Append(words[i]);
                }

                candidate.Append("...");
                Size size = TextRenderer.MeasureText(
                    candidate.ToString(),
                    font,
                    new Size(width, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                if (size.Height <= maxHeight)
                {
                    best = candidate.ToString();
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return best.Length == 0 ? text : best;
        }

        private void BuildTree()
        {
            if (_record == null)
            {
                return;
            }

            _tree.BeginUpdate();
            try
            {
                _tree.Nodes.Clear();
                TreeNode root = new TreeNode(_record.Name, 0, 0);
                root.Tag = string.Empty;
                _tree.Nodes.Add(root);
                PopulateNode(root, string.Empty);
                root.Expand();
                SelectTreePath(string.Empty);
            }
            finally
            {
                _tree.EndUpdate();
            }
        }

        private void PopulateNode(TreeNode node, string relativeDirectory)
        {
            node.Nodes.Clear();
            if (_record == null)
            {
                return;
            }

            string fullDirectory = PathResolver.GetFullPath(_record.GetSnapshotPath(), relativeDirectory);
            if (fullDirectory == null || !Directory.Exists(fullDirectory))
            {
                return;
            }

            List<FileSystemInfo> entries = new List<FileSystemInfo>();
            try
            {
                foreach (FileSystemInfo entry in new DirectoryInfo(fullDirectory).GetFileSystemInfos())
                {
                    if (string.Equals(entry.Name, ".git", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    entries.Add(entry);
                }
            }
            catch
            {
                return;
            }

            entries.Sort(delegate(FileSystemInfo a, FileSystemInfo b)
            {
                bool aDirectory = (a.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                bool bDirectory = (b.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                if (aDirectory != bDirectory)
                {
                    return aDirectory ? -1 : 1;
                }

                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            foreach (FileSystemInfo entry in entries)
            {
                bool isDirectory = (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                string childRelative = relativeDirectory.Length == 0
                    ? entry.Name
                    : relativeDirectory + "/" + entry.Name;

                int imageIndex = isDirectory ? 0 : GetFileImageIndex(entry.FullName);
                TreeNode child = new TreeNode(entry.Name, imageIndex, imageIndex);
                child.Tag = childRelative;
                if (isDirectory)
                {
                    try
                    {
                        if (Directory.GetFileSystemEntries(entry.FullName).Length > 0)
                        {
                            child.Nodes.Add(new TreeNode("Loading..."));
                        }
                    }
                    catch
                    {
                    }
                }

                node.Nodes.Add(child);
            }
        }

        private int GetFileImageIndex(string path)
        {
            if (FileUtil.IsMarkdown(path))
            {
                return 2;
            }

            return 1;
        }

        private void TreeBeforeExpand(object sender, TreeViewCancelEventArgs e)
        {
            if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Tag == null)
            {
                PopulateNode(e.Node, (string)e.Node.Tag);
            }
        }

        private void TreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_syncingTree || e.Node == null)
            {
                return;
            }

            string relative = e.Node.Tag as string;
            if (relative == null)
            {
                return;
            }

            string full = PathResolver.GetFullPath(_record.GetSnapshotPath(), relative);
            if (full != null && Directory.Exists(full))
            {
                if (e.Node.Nodes.Count == 1 && e.Node.Nodes[0].Tag == null)
                {
                    PopulateNode(e.Node, relative);
                }

                if (!e.Node.IsExpanded)
                {
                    e.Node.Expand();
                }
            }

            NavigateToPath(relative, false);
        }

        private void SelectTreePath(string relative)
        {
            if (_tree.Nodes.Count == 0)
            {
                return;
            }

            _syncingTree = true;
            try
            {
                string normalized = PathResolver.NormalizeRelative(relative);
                TreeNode node = _tree.Nodes[0];
                if (normalized.Length == 0)
                {
                    _tree.SelectedNode = node;
                    node.EnsureVisible();
                    return;
                }

                string[] pieces = normalized.Split('/');
                string accumulated = string.Empty;
                foreach (string piece in pieces)
                {
                    if (node.Nodes.Count == 1 && node.Nodes[0].Tag == null)
                    {
                        PopulateNode(node, (string)node.Tag);
                    }

                    if (!node.IsExpanded)
                    {
                        node.Expand();
                    }

                    if (accumulated.Length > 0)
                    {
                        accumulated += "/";
                    }

                    accumulated += piece;
                    TreeNode child = null;
                    foreach (TreeNode candidate in node.Nodes)
                    {
                        if (string.Equals(candidate.Tag as string, accumulated, StringComparison.Ordinal))
                        {
                            child = candidate;
                            break;
                        }
                    }

                    if (child == null)
                    {
                        break;
                    }

                    node = child;
                }

                _tree.SelectedNode = node;
                node.EnsureVisible();
            }
            finally
            {
                _syncingTree = false;
            }
        }

        private void NavigateToPath(string relative, bool raw)
        {
            if (_record == null)
            {
                return;
            }

            // Selecting a file or folder always returns the user to the Code tab.
            if (_tabStrip.SelectedIndex != 0)
            {
                _tabStrip.SelectedIndex = 0;
            }

            string normalized = PathResolver.NormalizeRelative(relative);
            string full = PathResolver.GetFullPath(_record.GetSnapshotPath(), normalized);
            _currentRelative = normalized;
            _currentRaw = raw;

            string html;
            if (full != null && Directory.Exists(full))
            {
                html = SiteBuilder.BuildDirectoryPage(_record, normalized);
            }
            else if (full != null && File.Exists(full))
            {
                html = SiteBuilder.BuildFilePage(_record, normalized, raw);
            }
            else
            {
                html = SiteBuilder.BuildNotFoundPage(_record, normalized);
            }

            RenderHtml(html);
            SelectTreePath(normalized);
        }

        private void RenderHtml(string html)
        {
            try
            {
                _previousRenderFile = _lastRenderFile;
                _renderCounter++;
                string file = Path.Combine(
                    _store.RenderPath,
                    "view-" + _sessionId + "-" + _renderCounter.ToString() + ".html");
                File.WriteAllText(file, html, new UTF8Encoding(false));
                _lastRenderFile = file;
                _browser.Navigate(new Uri(file));
            }
            catch (Exception ex)
            {
                _browser.DocumentText = SiteBuilder.BuildErrorPage(
                    "Could not render this page",
                    ex.Message);
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
                if (_record != null && PathResolver.IsInside(_record.GetSnapshotPath(), localPath))
                {
                    NavigateToPath(PathResolver.GetRelativePath(_record.GetSnapshotPath(), localPath), false);
                }

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

        private void HandleAppUrl(Uri uri)
        {
            string host = uri.Host.ToLowerInvariant();
            if (host == "browse")
            {
                string path = GetQueryValue(uri, "path");
                string mode = GetQueryValue(uri, "mode");
                NavigateToPath(path, string.Equals(mode, "raw", StringComparison.OrdinalIgnoreCase));
                return;
            }

            if (host == "openfile")
            {
                string path = GetQueryValue(uri, "path");
                string normalized = PathResolver.NormalizeRelative(path);
                string full = PathResolver.GetFullPath(_record.GetSnapshotPath(), normalized);
                if (full != null && File.Exists(full))
                {
                    UiTheme.OpenFile(full);
                }

                return;
            }
        }

        private static string GetQueryValue(Uri uri, string key)
        {
            string query = uri.Query;
            if (string.IsNullOrEmpty(query))
            {
                return string.Empty;
            }

            if (query.StartsWith("?", StringComparison.Ordinal))
            {
                query = query.Substring(1);
            }

            string[] parts = query.Split('&');
            foreach (string part in parts)
            {
                int equals = part.IndexOf('=');
                string name = equals < 0 ? part : part.Substring(0, equals);
                name = Uri.UnescapeDataString(name.Replace("+", " "));
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                {
                    string value = equals < 0 ? string.Empty : part.Substring(equals + 1);
                    return Uri.UnescapeDataString(value.Replace("+", " "));
                }
            }

            return string.Empty;
        }

        private void OnSyncRequested()
        {
            EventHandler handler = SyncRequested;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void DeleteSessionFiles()
        {
            try
            {
                string pattern = "view-" + _sessionId + "-*.html";
                foreach (string file in Directory.GetFiles(_store.RenderPath, pattern))
                {
                    TryDeleteFile(file);
                }
            }
            catch
            {
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetAttributes(path, FileAttributes.Normal);
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static ImageList CreateIconList()
        {
            ImageList list = new ImageList();
            list.ImageSize = new Size(16, 16);
            list.ColorDepth = ColorDepth.Depth32Bit;
            list.TransparentColor = Color.Transparent;

            list.Images.Add(CreateFolderIcon());
            list.Images.Add(CreateFileIcon(false));
            list.Images.Add(CreateFileIcon(true));
            return list;
        }

        private static Image CreateFolderIcon()
        {
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddArc(1, 4, 5, 5, 180, 90);
                    path.AddLine(5, 4, 7, 4);
                    path.AddLine(7, 4, 9, 6);
                    path.AddLine(9, 6, 14, 6);
                    path.AddArc(14, 6, 2, 2, 270, 90);
                    path.AddLine(15, 8, 15, 12);
                    path.AddArc(13, 10, 2, 2, 0, 90);
                    path.AddLine(14, 12, 2, 12);
                    path.AddArc(1, 10, 2, 2, 90, 90);
                    path.CloseFigure();
                    using (SolidBrush fill = new SolidBrush(ColorTranslator.FromHtml("#54AEFF")))
                    {
                        graphics.FillPath(fill, path);
                    }

                    using (Pen outline = new Pen(ColorTranslator.FromHtml("#218BFF")))
                    {
                        graphics.DrawPath(outline, path);
                    }
                }
            }

            return bitmap;
        }

        private static Image CreateFileIcon(bool markdown)
        {
            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddLine(4, 1, 9, 1);
                    path.AddLine(9, 1, 13, 5);
                    path.AddLine(13, 5, 13, 14);
                    path.AddLine(13, 14, 4, 14);
                    path.AddLine(4, 14, 4, 1);
                    path.CloseFigure();
                    using (SolidBrush fill = new SolidBrush(Color.White))
                    {
                        graphics.FillPath(fill, path);
                    }

                    using (Pen outline = new Pen(ColorTranslator.FromHtml("#8C959F")))
                    {
                        graphics.DrawPath(outline, path);
                    }

                    graphics.DrawLine(Pens.Gray, 5, 3, 8, 3);
                    graphics.DrawLine(Pens.Gray, 6, 5, 12, 5);
                    graphics.DrawLine(Pens.Gray, 6, 7, 12, 7);
                    graphics.DrawLine(Pens.Gray, 6, 9, 11, 9);
                    if (markdown)
                    {
                        using (SolidBrush accent = new SolidBrush(ColorTranslator.FromHtml("#0969DA")))
                        {
                            graphics.FillRectangle(accent, 5, 10, 7, 3);
                        }
                    }
                    else
                    {
                        graphics.DrawLine(Pens.Gray, 6, 11, 10, 11);
                    }
                }
            }

            return bitmap;
        }
    }
}
