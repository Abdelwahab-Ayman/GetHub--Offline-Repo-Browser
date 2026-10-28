using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GetHub.Controls;
using GetHub.Models;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Forms
{
    public class MainForm : Form
    {
        private readonly RepoStore _store;
        private Label _countLabel;
        private TextBox _searchBox;
        private FlowLayoutPanel _cardsPanel;
        private Panel _contentHost;
        private WelcomeView _welcome;
        private List<RepoRecord> _records;
        private RepoView _currentView;
        private string _selectedFullName;
        private bool _resizingCards;

        public MainForm(RepoStore store)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            _store = store;
            _records = new List<RepoRecord>();
            _selectedFullName = string.Empty;

            Text = "GetHub";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 820);
            MinimumSize = new Size(960, 640);
            BackColor = UiTheme.Canvas;
            Font = UiTheme.BodyFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            KeyPreview = true;
            DoubleBuffered = true;
            AllowDrop = true;
            DragEnter += MainFormDragEnter;
            DragDrop += MainFormDragDrop;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimizeBox = true;
            MaximizeBox = true;
            ControlBox = true;

            try
            {
                Icon = AppAssets.AppIcon;
            }
            catch
            {
            }

            Shown += MainFormShown;
            BuildUi();
            LoadRecords();
        }

        private Button _accountButton;

        private void BuildUi()
        {
            List<Control> old = new List<Control>();
            foreach (Control control in Controls)
            {
                old.Add(control);
            }

            Controls.Clear();
            foreach (Control control in old)
            {
                control.Dispose();
            }

            BackColor = UiTheme.Canvas;

            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 2;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 304F));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.BackColor = UiTheme.Canvas;
            Controls.Add(root);

            Panel topBar = new Panel();
            topBar.Dock = DockStyle.Fill;
            topBar.Margin = Padding.Empty;
            topBar.BackColor = UiTheme.DarkBar;
            root.Controls.Add(topBar, 0, 0);
            root.SetColumnSpan(topBar, 2);

            LogoView logo = new LogoView();
            logo.Size = new Size(24, 24);
            logo.Location = new Point(18, 18);
            topBar.Controls.Add(logo);

            Label appTitle = new Label();
            appTitle.Text = "GetHub";
            appTitle.Font = new Font("Segoe UI Semibold", 12F, FontStyle.Regular);
            appTitle.ForeColor = UiTheme.DarkBarText;
            appTitle.AutoSize = true;
            appTitle.BackColor = Color.Transparent;
            appTitle.Location = new Point(50, 6);
            topBar.Controls.Add(appTitle);
            UiTheme.FitLabel(appTitle);

            Label repoInfo = new Label();
            repoInfo.Text = "Local offline browser \u2022 " + (UiTheme.IsLight ? "Light mode" : "Dark mode");
            repoInfo.Font = UiTheme.SmallFont;
            repoInfo.ForeColor = UiTheme.DarkBarMuted;
            repoInfo.AutoSize = true;
            repoInfo.BackColor = Color.Transparent;
            repoInfo.Location = new Point(51, appTitle.Bottom + 1);
            topBar.Controls.Add(repoInfo);
            UiTheme.FitLabel(repoInfo);

            FlowLayoutPanel actions = new FlowLayoutPanel();
            actions.Dock = DockStyle.Right;
            actions.AutoSize = true;
            actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actions.Margin = Padding.Empty;
            actions.FlowDirection = FlowDirection.RightToLeft;
            actions.WrapContents = false;
            actions.BackColor = UiTheme.DarkBar;
            actions.Padding = new Padding(0, 14, 20, 0);
            topBar.Controls.Add(actions);
            actions.BringToFront();

            Button addButton = UiTheme.MakeButton("Add repository", ButtonStyle.Primary);
            addButton.Size = new Size(136, 32);
            addButton.Margin = new Padding(8, 0, 0, 0);
            addButton.Click += delegate { AddRepository(); };
            actions.Controls.Add(addButton);

            Button settingsButton = UiTheme.MakeButton("Settings", ButtonStyle.Secondary);
            settingsButton.Size = new Size(84, 32);
            settingsButton.Margin = new Padding(8, 0, 0, 0);
            settingsButton.Click += delegate { ShowSettings(); };
            actions.Controls.Add(settingsButton);

            _accountButton = UiTheme.MakeButton("Sign in", ButtonStyle.Secondary);
            _accountButton.Size = new Size(84, 32);
            _accountButton.Margin = new Padding(8, 0, 0, 0);
            _accountButton.Click += delegate { ShowAccount(); };
            actions.Controls.Add(_accountButton);
            UpdateAccountButton();
            AddWindowControlButtons(topBar);

            AddWindowControlButtons(topBar);

            Panel topBarBorder = new Panel();
            topBarBorder.Dock = DockStyle.Bottom;
            topBarBorder.Height = 1;
            topBarBorder.BackColor = UiTheme.Border;
            topBar.Controls.Add(topBarBorder);
            topBarBorder.BringToFront();

            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Fill;
            sidebar.Margin = Padding.Empty;
            sidebar.BackColor = UiTheme.Border;
            sidebar.Padding = new Padding(0, 0, 1, 0);
            root.Controls.Add(sidebar, 0, 1);

            Panel sidebarInner = new Panel();
            sidebarInner.Dock = DockStyle.Fill;
            sidebarInner.BackColor = UiTheme.Surface;
            sidebar.Controls.Add(sidebarInner);

            TableLayoutPanel sideLayout = new TableLayoutPanel();
            sideLayout.Dock = DockStyle.Fill;
            sideLayout.ColumnCount = 1;
            sideLayout.RowCount = 5;
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            sideLayout.BackColor = UiTheme.Surface;
            sidebarInner.Controls.Add(sideLayout);

            Panel navPanel = new Panel();
            navPanel.Dock = DockStyle.Fill;
            navPanel.BackColor = UiTheme.Surface;
            navPanel.Padding = new Padding(10, 10, 10, 0);
            sideLayout.Controls.Add(navPanel, 0, 0);

            FlowLayoutPanel navFlow = new FlowLayoutPanel();
            navFlow.Dock = DockStyle.Fill;
            navFlow.FlowDirection = FlowDirection.TopDown;
            navFlow.WrapContents = false;
            navFlow.BackColor = UiTheme.Surface;
            navPanel.Controls.Add(navFlow);

            Label navLabel = new Label();
            navLabel.Text = "NAVIGATION";
            navLabel.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            navLabel.ForeColor = UiTheme.Faint;
            navLabel.AutoSize = true;
            navLabel.Margin = new Padding(4, 0, 0, 10);
            navFlow.Controls.Add(navLabel);
            UiTheme.FitLabel(navLabel);

            navFlow.Controls.Add(MakeNavItem("Search", false, delegate { FocusSearch(); }));
            navFlow.Controls.Add(MakeNavItem("Repositories", true, delegate { FocusRepositories(); }));
            navFlow.Controls.Add(MakeNavItem("Settings", false, delegate { ShowSettings(); }));

            Panel sideHeader = new Panel();
            sideHeader.Dock = DockStyle.Fill;
            sideHeader.BackColor = UiTheme.Surface;
            sideLayout.Controls.Add(sideHeader, 0, 1);

            Label reposHeading = new Label();
            reposHeading.Text = "FILE EXPLORER";
            reposHeading.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            reposHeading.ForeColor = UiTheme.Faint;
            reposHeading.AutoSize = true;
            reposHeading.Location = new Point(14, 13);
            sideHeader.Controls.Add(reposHeading);
            UiTheme.FitLabel(reposHeading);

            _countLabel = new Label();
            _countLabel.Text = "0 repos";
            _countLabel.Font = UiTheme.SmallFont;
            _countLabel.ForeColor = UiTheme.Faint;
            _countLabel.AutoSize = false;
            _countLabel.TextAlign = ContentAlignment.MiddleRight;
            _countLabel.Location = new Point(170, 11);
            _countLabel.Size = new Size(120, 22);
            _countLabel.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            sideHeader.Controls.Add(_countLabel);

            Panel searchHost = new Panel();
            searchHost.Dock = DockStyle.Fill;
            searchHost.BackColor = UiTheme.Surface;
            searchHost.Padding = new Padding(12, 4, 12, 4);
            sideLayout.Controls.Add(searchHost, 0, 2);

            _searchBox = new TextBox();
            _searchBox.Dock = DockStyle.Fill;
            _searchBox.BorderStyle = BorderStyle.FixedSingle;
            _searchBox.BackColor = UiTheme.Surface;
            _searchBox.ForeColor = UiTheme.Text;
            _searchBox.Font = UiTheme.BodyFont;
            _searchBox.TextChanged += delegate { RefreshCards(); };
            searchHost.Controls.Add(_searchBox);

            _cardsPanel = new FlowLayoutPanel();
            _cardsPanel.Dock = DockStyle.Fill;
            _cardsPanel.BackColor = UiTheme.Surface;
            _cardsPanel.FlowDirection = FlowDirection.TopDown;
            _cardsPanel.WrapContents = false;
            _cardsPanel.AutoScroll = true;
            _cardsPanel.Padding = new Padding(12);
            _cardsPanel.Resize += delegate { ResizeCards(); };
            sideLayout.Controls.Add(_cardsPanel, 0, 3);

            Panel sideFooter = new Panel();
            sideFooter.Dock = DockStyle.Fill;
            sideFooter.BackColor = UiTheme.Surface;
            sideFooter.Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(UiTheme.Border))
                {
                    e.Graphics.DrawLine(pen, 0, 0, sideFooter.Width, 0);
                }
            };
            sideLayout.Controls.Add(sideFooter, 0, 4);

            LinkLabel dataLink = new LinkLabel();
            dataLink.Text = "Open local data folder";
            dataLink.Font = UiTheme.SmallFont;
            dataLink.LinkColor = UiTheme.Accent;
            dataLink.ActiveLinkColor = UiTheme.Accent;
            dataLink.AutoSize = true;
            dataLink.Location = new Point(14, 17);
            dataLink.LinkClicked += delegate { UiTheme.OpenFolder(_store.RootPath); };
            sideFooter.Controls.Add(dataLink);
            UiTheme.FitLabel(dataLink);

            _contentHost = new Panel();
            _contentHost.Dock = DockStyle.Fill;
            _contentHost.Margin = Padding.Empty;
            _contentHost.BackColor = UiTheme.Canvas;
            root.Controls.Add(_contentHost, 1, 1);

            _welcome = new WelcomeView();
            _welcome.Dock = DockStyle.Fill;
            _welcome.AddRequested += delegate { AddRepository(); };
            _contentHost.Controls.Add(_welcome);
        }

        private void MainFormShown(object sender, EventArgs e)
        {
            UiTheme.SetCueBanner(_searchBox, "Search repositories");
            ResizeCards();
        }

        private static Label MakeNavItem(string text, bool active, EventHandler onClick)
        {
            Label item = new Label();
            item.Text = text;
            item.Font = active ? UiTheme.BodyBold : UiTheme.BodyFont;
            item.ForeColor = active ? UiTheme.Accent : UiTheme.Text;
            item.AutoSize = false;
            item.Size = new Size(260, 26);
            item.TextAlign = ContentAlignment.MiddleLeft;
            item.Padding = new Padding(6, 0, 0, 0);
            item.Margin = new Padding(0, 0, 0, 2);
            item.Cursor = Cursors.Hand;
            item.BackColor = UiTheme.Surface;
            item.Click += onClick;
            return item;
        }

        private void FocusSearch()
        {
            if (_searchBox == null)
            {
                return;
            }

            _searchBox.Focus();
            _searchBox.SelectAll();
        }

        private void FocusRepositories()
        {
            if (_cardsPanel == null)
            {
                return;
            }

            _cardsPanel.Focus();
            _cardsPanel.AutoScrollPosition = new Point(0, 0);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.N)
            {
                AddRepository();
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && e.KeyCode == Keys.F)
            {
                _searchBox.Focus();
                _searchBox.SelectAll();
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.F5 && _currentView != null)
            {
                SyncRecord(_currentView.Record);
                e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private void LoadRecords()
        {
            string keep = _selectedFullName;
            _records = _store.LoadAll();
            RefreshCards();
            _countLabel.Text = _records.Count == 1 ? "1 repo" : _records.Count + " repos";

            if (_records.Count == 0)
            {
                ShowWelcome();
                return;
            }

            RepoRecord selected = FindRecord(keep);
            if (selected == null)
            {
                selected = _records[0];
            }

            OpenRecord(selected);
        }

        private void RefreshCards()
        {
            string filter = _searchBox == null ? string.Empty : _searchBox.Text.Trim();
            _cardsPanel.SuspendLayout();
            try
            {
                List<Control> old = new List<Control>();
                foreach (Control control in _cardsPanel.Controls)
                {
                    old.Add(control);
                }

                _cardsPanel.Controls.Clear();
                foreach (Control control in old)
                {
                    RepoCard oldCard = control as RepoCard;
                    if (oldCard != null)
                    {
                        ContextMenuStrip menu = oldCard.ContextMenuStrip;
                        oldCard.ContextMenuStrip = null;
                        if (menu != null)
                        {
                            menu.Dispose();
                        }
                    }

                    control.Dispose();
                }

                int matches = 0;
                foreach (RepoRecord record in _records)
                {
                    if (!MatchesFilter(record, filter))
                    {
                        continue;
                    }

                    RepoRecord captured = record;
                    RepoCard card = new RepoCard();
                    card.Record = record;
                    card.Width = GetCardWidth();
                    card.Margin = new Padding(0, 0, 0, 10);
                    card.ContextMenuStrip = BuildCardMenu(captured);
                    card.Click += delegate { OpenRecord(captured); };
                    card.DoubleClick += delegate { OpenRecord(captured); };
                    _cardsPanel.Controls.Add(card);
                    matches++;
                }

                if (matches == 0)
                {
                    Label empty = new Label();
                    empty.Text = _records.Count == 0
                        ? "No repositories yet. Add one to get started."
                        : "No repositories match your search.";
                    empty.Font = UiTheme.BodyFont;
                    empty.ForeColor = UiTheme.Faint;
                    empty.AutoSize = false;
                    empty.Size = new Size(250, 44);
                    empty.Margin = new Padding(4, 6, 0, 0);
                    _cardsPanel.Controls.Add(empty);
                }
            }
            finally
            {
                _cardsPanel.ResumeLayout(true);
            }

            UpdateCardSelection();
        }

        private ContextMenuStrip BuildCardMenu(RepoRecord record)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = UiTheme.BodyFont;

            ToolStripMenuItem sync = new ToolStripMenuItem("Sync now");
            sync.Click += delegate { SyncRecord(record); };
            menu.Items.Add(sync);

            ToolStripMenuItem openFolder = new ToolStripMenuItem("Open folder");
            openFolder.Click += delegate { UiTheme.OpenFolder(record.GetSnapshotPath()); };
            menu.Items.Add(openFolder);

            ToolStripMenuItem openGitHub = new ToolStripMenuItem("Open on GitHub");
            openGitHub.Enabled = !string.IsNullOrEmpty(record.HtmlUrl);
            openGitHub.Click += delegate { UiTheme.OpenUrl(record.HtmlUrl); };
            menu.Items.Add(openGitHub);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem remove = new ToolStripMenuItem("Remove repository");
            remove.ForeColor = UiTheme.Red;
            remove.Click += delegate { RemoveRecord(record); };
            menu.Items.Add(remove);

            return menu;
        }

        private bool MatchesFilter(RepoRecord record, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            if (record == null)
            {
                return false;
            }

            if (Contains(record.GetDisplayName(), filter) ||
                Contains(record.Description, filter) ||
                Contains(record.Language, filter))
            {
                return true;
            }

            return false;
        }

        private static bool Contains(string value, string filter)
        {
            return !string.IsNullOrEmpty(value) &&
                value.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private int GetCardWidth()
        {
            int width = _cardsPanel.ClientSize.Width -
                _cardsPanel.Padding.Horizontal -
                SystemInformation.VerticalScrollBarWidth - 2;
            if (width < 140)
            {
                width = 240;
            }

            return width;
        }

        private void ResizeCards()
        {
            if (_resizingCards || _cardsPanel == null)
            {
                return;
            }

            _resizingCards = true;
            try
            {
                int width = GetCardWidth();
                foreach (Control control in _cardsPanel.Controls)
                {
                    RepoCard card = control as RepoCard;
                    if (card != null && card.Width != width)
                    {
                        card.Width = width;
                    }
                }
            }
            finally
            {
                _resizingCards = false;
            }
        }

        private void UpdateCardSelection()
        {
            foreach (Control control in _cardsPanel.Controls)
            {
                RepoCard card = control as RepoCard;
                if (card == null || card.Record == null)
                {
                    continue;
                }

                card.Selected = string.Equals(
                    card.Record.GetDisplayName(),
                    GetSelectedDisplayName(),
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        private string GetSelectedDisplayName()
        {
            if (string.IsNullOrEmpty(_selectedFullName))
            {
                return string.Empty;
            }

            foreach (RepoRecord record in _records)
            {
                if (string.Equals(record.FullName, _selectedFullName, StringComparison.OrdinalIgnoreCase))
                {
                    return record.GetDisplayName();
                }
            }

            return string.Empty;
        }

        private RepoRecord FindRecord(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
            {
                return null;
            }

            foreach (RepoRecord record in _records)
            {
                if (string.Equals(record.FullName, fullName, StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }

            return null;
        }

        private RepoRecord FindRecord(string owner, string name)
        {
            foreach (RepoRecord record in _records)
            {
                if (string.Equals(record.Owner, owner, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }

            return null;
        }

        private void OpenRecord(RepoRecord record)
        {
            if (record == null)
            {
                ShowWelcome();
                return;
            }

            DisposeView();
            if (_contentHost.Controls.Contains(_welcome))
            {
                _contentHost.Controls.Remove(_welcome);
            }

            _selectedFullName = record.FullName;
            RepoView view = new RepoView(_store, record);
            view.Dock = DockStyle.Fill;
            view.SyncRequested += ViewSyncRequested;
            view.SignInRequested += ViewSignInRequested;
            _currentView = view;
            _contentHost.Controls.Add(view);
            view.BringToFront();
            UpdateCardSelection();
        }

        private void ShowWelcome()
        {
            DisposeView();
            _selectedFullName = string.Empty;
            _contentHost.Controls.Clear();
            _contentHost.Controls.Add(_welcome);
            UpdateCardSelection();
        }

        private void DisposeView()
        {
            if (_currentView == null)
            {
                return;
            }

            RepoView view = _currentView;
            _currentView = null;
            view.SyncRequested -= ViewSyncRequested;
            view.SignInRequested -= ViewSignInRequested;
            _contentHost.Controls.Remove(view);
            view.Dispose();
        }

        private void ViewSyncRequested(object sender, EventArgs e)
        {
            RepoView view = sender as RepoView;
            if (view != null)
            {
                SyncRecord(view.Record);
            }
        }

        private void ViewSignInRequested(object sender, EventArgs e)
        {
            ShowSignIn();
        }

        private void AddRepository()
        {
            using (AddRepoForm dialog = new AddRepoForm())
            {
                if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Reference == null)
                {
                    return;
                }

                RepoReference reference = dialog.Reference;
                bool sameAsOpen = _currentView != null && SameRepository(_currentView.Record, reference);
                if (sameAsOpen)
                {
                    DisposeView();
                }

                GitHubDownloader downloader = new GitHubDownloader(_store, _store.Settings.GitHubToken);
                RepoRecord result = null;

                using (DownloadProgressForm progress = new DownloadProgressForm(
                    "Add repository",
                    reference.Owner + "/" + reference.Name))
                {
                    bool succeeded = progress.Run(
                        this,
                        async (IProgress<DownloadProgress> reported, CancellationToken token) =>
                        {
                            result = await downloader.DownloadAsync(reference, reported, token);
                        });

                    if (succeeded && result != null)
                    {
                        _selectedFullName = result.FullName;
                        LoadRecords();
                    }
                    else
                    {
                        if (progress.Error != null)
                        {
                            ShowError("Could not add repository", progress.Error);
                        }

                        if (sameAsOpen)
                        {
                            LoadRecords();
                        }
                    }
                }
            }
        }

        private void SyncRecord(RepoRecord record)
        {
            if (record == null)
            {
                return;
            }

            bool wasOpen = _currentView != null && SameRepository(_currentView.Record, record);
            if (wasOpen)
            {
                DisposeView();
            }

            RepoReference reference = new RepoReference();
            reference.Owner = record.Owner;
            reference.Name = record.Name;
            reference.Branch = record.Branch;

            GitHubDownloader downloader = new GitHubDownloader(_store, _store.Settings.GitHubToken);
            RepoRecord updated = null;

            using (DownloadProgressForm progress = new DownloadProgressForm(
                "Sync repository",
                record.GetDisplayName()))
            {
                bool succeeded = progress.Run(
                    this,
                    async (IProgress<DownloadProgress> reported, CancellationToken token) =>
                    {
                        updated = await downloader.DownloadAsync(reference, reported, token);
                    });

                if (succeeded && updated != null)
                {
                    _selectedFullName = updated.FullName;
                    LoadRecords();
                }
                else
                {
                    if (progress.Error != null)
                    {
                        ShowError("Could not sync repository", progress.Error);
                    }

                    if (wasOpen)
                    {
                        LoadRecords();
                    }
                }
            }
        }

        private void RemoveRecord(RepoRecord record)
        {
            if (record == null)
            {
                return;
            }

            DialogResult answer = MessageBox.Show(
                this,
                "Remove the offline copy of " + record.GetDisplayName() + "?\n\n" +
                "The downloaded files will be deleted from this computer.",
                "Remove repository",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (answer != DialogResult.Yes)
            {
                return;
            }

            bool wasOpen = _currentView != null && SameRepository(_currentView.Record, record);
            if (wasOpen)
            {
                DisposeView();
            }

            try
            {
                _store.DeleteRecord(record);
                if (string.Equals(_selectedFullName, record.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    _selectedFullName = string.Empty;
                }

                LoadRecords();
            }
            catch (Exception ex)
            {
                ShowError("Could not remove repository", ex);
                if (wasOpen)
                {
                    LoadRecords();
                }
            }
        }

        private void ShowSettings()
        {
            string previousTheme = UiTheme.CurrentTheme.ToString();
            bool accountChanged = false;
            using (SettingsForm dialog = new SettingsForm(_store))
            {
                dialog.ShowDialog(this);
                accountChanged = dialog.AccountChanged;
            }

            if (!string.Equals(previousTheme, UiTheme.CurrentTheme.ToString(), StringComparison.Ordinal))
            {
                ApplyTheme();
                return;
            }

            UpdateAccountButton();
            if (accountChanged && _currentView != null)
            {
                _currentView.NotifyAccountChanged();
            }
        }

        private void ShowAccount()
        {
            if (_store.Settings != null && _store.Settings.IsSignedIn)
            {
                ShowSettings();
                return;
            }

            ShowSignIn();
        }

        private void ShowSignIn()
        {
            using (SignInForm dialog = new SignInForm(_store))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }
            }

            UpdateAccountButton();
            if (_currentView != null)
            {
                _currentView.NotifyAccountChanged();
            }
        }

        private void AddWindowControlButtons(Panel topBar)
        {
            FlowLayoutPanel windowButtons = new FlowLayoutPanel();
            windowButtons.Dock = DockStyle.Right;
            windowButtons.AutoSize = true;
            windowButtons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            windowButtons.FlowDirection = FlowDirection.LeftToRight;
            windowButtons.WrapContents = false;
            windowButtons.BackColor = UiTheme.DarkBar;
            windowButtons.Margin = Padding.Empty;
            windowButtons.Padding = new Padding(0, 0, 0, 0);
            topBar.Controls.Add(windowButtons);
            windowButtons.BringToFront();

            Button minimizeButton = new Button();
            minimizeButton.Text = "\u2014";
            minimizeButton.Size = new Size(46, 32);
            minimizeButton.FlatStyle = FlatStyle.Flat;
            minimizeButton.FlatAppearance.BorderSize = 0;
            minimizeButton.BackColor = UiTheme.DarkBar;
            minimizeButton.ForeColor = UiTheme.DarkBarText;
            minimizeButton.Font = new Font("Segoe UI", 9F);
            minimizeButton.Cursor = Cursors.Hand;
            minimizeButton.Margin = new Padding(0);
            minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };
            minimizeButton.MouseEnter += delegate { minimizeButton.BackColor = UiTheme.HoverBackground; };
            minimizeButton.MouseLeave += delegate { minimizeButton.BackColor = UiTheme.DarkBar; };
            windowButtons.Controls.Add(minimizeButton);

            Button maximizeButton = new Button();
            maximizeButton.Text = "\u25A1";
            maximizeButton.Size = new Size(46, 32);
            maximizeButton.FlatStyle = FlatStyle.Flat;
            maximizeButton.FlatAppearance.BorderSize = 0;
            maximizeButton.BackColor = UiTheme.DarkBar;
            maximizeButton.ForeColor = UiTheme.DarkBarText;
            maximizeButton.Font = new Font("Segoe UI", 10F);
            maximizeButton.Cursor = Cursors.Hand;
            maximizeButton.Margin = new Padding(0);
            maximizeButton.Click += delegate {
                if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Normal;
                }
                else
                {
                    WindowState = FormWindowState.Maximized;
                }
            };
            maximizeButton.MouseEnter += delegate { maximizeButton.BackColor = UiTheme.HoverBackground; };
            maximizeButton.MouseLeave += delegate { maximizeButton.BackColor = UiTheme.DarkBar; };
            windowButtons.Controls.Add(maximizeButton);

            Button closeButton = new Button();
            closeButton.Text = "\u2715";
            closeButton.Size = new Size(46, 32);
            closeButton.FlatStyle = FlatStyle.Flat;
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.BackColor = UiTheme.DarkBar;
            closeButton.ForeColor = UiTheme.DarkBarText;
            closeButton.Font = new Font("Segoe UI", 9F);
            closeButton.Cursor = Cursors.Hand;
            closeButton.Margin = new Padding(0);
            closeButton.Click += delegate { Close(); };
            closeButton.MouseEnter += delegate {
                closeButton.BackColor = ColorTranslator.FromHtml("#e81123");
                closeButton.ForeColor = Color.White;
            };
            closeButton.MouseLeave += delegate {
                closeButton.BackColor = UiTheme.DarkBar;
                closeButton.ForeColor = UiTheme.DarkBarText;
            };
            windowButtons.Controls.Add(closeButton);
        }

        private void UpdateAccountButton()
        {
            if (_accountButton == null || _accountButton.IsDisposed)
            {
                return;
            }

            GitHubAccount account = _store.Settings == null ? null : _store.Settings.GetAccount();
            string text;
            if (account != null && account.IsAuthenticated)
            {
                text = "@" + account.Login;
            }
            else if (_store.Settings != null && _store.Settings.IsSignedIn)
            {
                text = "Account";
            }
            else
            {
                text = "Sign in";
            }

            _accountButton.Text = text;
            int width = TextRenderer.MeasureText(text, _accountButton.Font).Width + 28;
            _accountButton.Width = Math.Max(84, width);
        }

        private void ApplyTheme()
        {
            string keep = _selectedFullName;
            SuspendLayout();
            try
            {
                DisposeView();
                BuildUi();
                _selectedFullName = keep;
                LoadRecords();
                UiTheme.SetCueBanner(_searchBox, "Search repositories");
                ResizeCards();
            }
            finally
            {
                ResumeLayout(true);
            }

            Invalidate(true);
        }

        private static bool SameRepository(RepoRecord record, RepoReference reference)
        {
            if (record == null || reference == null)
            {
                return false;
            }

            return string.Equals(record.Owner, reference.Owner, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(record.Name, reference.Name, StringComparison.OrdinalIgnoreCase);
        }

        private static bool SameRepository(RepoRecord left, RepoRecord right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return string.Equals(left.Owner, right.Owner, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
        }

        private void MainFormDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data == null)
            {
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }

            e.Effect = DragDropEffects.None;
        }

        private void MainFormDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length == 0)
            {
                return;
            }

            string path = files[0];
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            string fullPath = Path.GetFullPath(path);
            OpenLocalRepository(fullPath);
        }

        private void OpenLocalRepository(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!Directory.Exists(path))
            {
                ShowError("Cannot open repository", new Exception("Dropped item is not a directory."));
                return;
            }

            string rootDir = Path.GetFullPath(path);
            string gitDir = Path.Combine(rootDir, ".git");
            if (!Directory.Exists(gitDir))
            {
                ShowError("Cannot open repository", new Exception("No .git directory found. Only local Git repositories are supported."));
                return;
            }

            RepoRecord existing = FindLocalRecord(rootDir);
            if (existing != null)
            {
                // Records created before source paths were stored can be linked again so
                // the History tab can read the original .git folder.
                if (string.IsNullOrEmpty(existing.SourcePath))
                {
                    existing.SourcePath = rootDir;
                    try
                    {
                        _store.SaveRecord(existing);
                    }
                    catch
                    {
                    }
                }

                OpenRecord(existing);
                return;
            }

            string name = Path.GetFileName(rootDir);
            string owner = "local";

            RepoRecord record = new RepoRecord();
            record.Owner = owner;
            record.Name = name;
            record.FullName = owner + "/" + name;
            record.HtmlUrl = string.Empty;
            record.Description = "Local repository";
            record.Language = string.Empty;
            record.DefaultBranch = "HEAD";
            record.Branch = "HEAD";
            record.SourcePath = rootDir;

            try
            {
                string snapshotPath = record.GetSnapshotPath();
                Directory.CreateDirectory(snapshotPath);

                CopyDirectory(rootDir, snapshotPath);

                _store.SaveRecord(record);
                _selectedFullName = record.FullName;
                LoadRecords();
            }
            catch (Exception ex)
            {
                ShowError("Could not open local repository", ex);
            }
        }

        private RepoRecord FindLocalRecord(string rootDir)
        {
            if (string.IsNullOrEmpty(rootDir))
            {
                return null;
            }

            string normalized = Path.GetFullPath(rootDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string folderName = Path.GetFileName(normalized);
            foreach (RepoRecord record in _records)
            {
                if (record == null)
                {
                    continue;
                }

                // The original folder is the authoritative link for local repositories.
                if (!string.IsNullOrEmpty(record.SourcePath))
                {
                    try
                    {
                        string source = Path.GetFullPath(record.SourcePath).TrimEnd(
                            Path.DirectorySeparatorChar,
                            Path.AltDirectorySeparatorChar);
                        if (string.Equals(source, normalized, StringComparison.OrdinalIgnoreCase))
                        {
                            return record;
                        }
                    }
                    catch
                    {
                    }
                }

                // Legacy local records have no SourcePath: match the folder name so a
                // second drop re-links the existing copy instead of duplicating it.
                if (string.IsNullOrEmpty(record.SourcePath) &&
                    string.Equals(record.Owner, "local", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(record.Name, folderName, StringComparison.OrdinalIgnoreCase))
                {
                    return record;
                }
            }

            return null;
        }

        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string name = Path.GetFileName(file);
                if (string.Equals(name, ".gitignore", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string destFile = Path.Combine(destinationDir, name);
                File.Copy(file, destFile, true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string name = Path.GetFileName(dir);
                if (string.Equals(name, ".git", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string destDir = Path.Combine(destinationDir, name);
                CopyDirectory(dir, destDir);
            }
        }

        private void ShowError(string title, Exception error)
        {
            string message = error == null ? "An unknown error occurred." : error.Message;
            MessageBox.Show(
                this,
                message,
                title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
