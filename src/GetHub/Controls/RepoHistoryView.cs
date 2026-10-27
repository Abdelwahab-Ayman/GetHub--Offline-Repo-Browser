using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // The History tab. The graph lists cached GitHub commits or reads a local .git
    // folder, and the section tabs below it show the full commit message, the
    // changed files and the unified diff. Commit details are loaded on demand and
    // cached through CommitCache so every section keeps working offline.
    public class RepoHistoryView : UserControl
    {
        private readonly RepoStore _store;
        private readonly Panel _toolbar;
        private readonly Label _branchLabel;
        private readonly Label _countLabel;
        private readonly LinkLabel _githubLink;
        private readonly LinkLabel _openCommitLink;
        private readonly Button _refreshButton;
        private readonly CommitSectionTabs _sectionTabs;
        private readonly CommitGraphView _graph;
        private readonly CommitMessageView _messageView;
        private readonly CommitFilesView _filesView;
        private readonly CommitDiffView _diffView;
        private RepoRecord _record;
        private bool _loading;
        private bool _loaded;
        private string _detailSha;
        private CommitDetail _detail;
        private bool _detailLoading;
        private string _detailError;
        private int _section;
        private int _pendingDiffFile = -1;

        public RepoHistoryView(RepoStore store, RepoRecord record)
        {
            _store = store;
            _record = record;
            BackColor = UiTheme.Canvas;
            Dock = DockStyle.Fill;
            Font = UiTheme.BodyFont;

            _graph = new CommitGraphView();
            _graph.Dock = DockStyle.Fill;
            _graph.Margin = Padding.Empty;
            _graph.SelectionChanged += delegate { OnCommitSelectionChanged(); };
            _graph.CommitActivated += delegate { OpenSelectedCommitDetail(); };

            _messageView = new CommitMessageView();
            _messageView.Dock = DockStyle.Fill;
            _messageView.Margin = Padding.Empty;
            _messageView.Visible = false;

            _filesView = new CommitFilesView();
            _filesView.Dock = DockStyle.Fill;
            _filesView.Margin = Padding.Empty;
            _filesView.Visible = false;
            _filesView.FileActivated += delegate { OpenSelectedFileDiff(); };

            _diffView = new CommitDiffView();
            _diffView.Dock = DockStyle.Fill;
            _diffView.Margin = Padding.Empty;
            _diffView.Visible = false;

            Controls.Add(_graph);
            Controls.Add(_messageView);
            Controls.Add(_filesView);
            Controls.Add(_diffView);

            _sectionTabs = new CommitSectionTabs();
            _sectionTabs.Dock = DockStyle.Top;
            _sectionTabs.Margin = Padding.Empty;
            _sectionTabs.SelectedIndexChanged += delegate { ShowSection(); };
            Controls.Add(_sectionTabs);

            _toolbar = new Panel();
            _toolbar.Dock = DockStyle.Top;
            _toolbar.Height = 56;
            _toolbar.BackColor = UiTheme.Surface;
            _toolbar.Padding = new Padding(18, 10, 16, 8);
            _toolbar.Paint += ToolbarPaint;
            Controls.Add(_toolbar);

            TableLayoutPanel toolbarLayout = new TableLayoutPanel();
            toolbarLayout.Dock = DockStyle.Fill;
            toolbarLayout.ColumnCount = 2;
            toolbarLayout.RowCount = 1;
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbarLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            toolbarLayout.Margin = Padding.Empty;
            toolbarLayout.BackColor = UiTheme.Surface;
            _toolbar.Controls.Add(toolbarLayout);

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

            _branchLabel = new Label();
            _branchLabel.Dock = DockStyle.Fill;
            _branchLabel.Margin = Padding.Empty;
            _branchLabel.Font = UiTheme.BodyBold;
            _branchLabel.ForeColor = UiTheme.Text;
            _branchLabel.AutoSize = false;
            _branchLabel.AutoEllipsis = true;
            _branchLabel.TextAlign = ContentAlignment.MiddleLeft;
            _branchLabel.UseMnemonic = false;
            _branchLabel.BackColor = UiTheme.Surface;
            labels.Controls.Add(_branchLabel, 0, 0);

            _countLabel = new Label();
            _countLabel.Dock = DockStyle.Fill;
            _countLabel.Margin = Padding.Empty;
            _countLabel.Font = UiTheme.SmallFont;
            _countLabel.ForeColor = UiTheme.Muted;
            _countLabel.AutoSize = false;
            _countLabel.AutoEllipsis = true;
            _countLabel.TextAlign = ContentAlignment.MiddleLeft;
            _countLabel.UseMnemonic = false;
            _countLabel.BackColor = UiTheme.Surface;
            labels.Controls.Add(_countLabel, 0, 1);

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
            _refreshButton.Click += delegate { RefreshHistory(); };
            actions.Controls.Add(_refreshButton);

            _openCommitLink = MakeToolbarLink("Open commit");
            _openCommitLink.Margin = new Padding(12, 8, 0, 0);
            _openCommitLink.Visible = false;
            _openCommitLink.LinkClicked += delegate { OpenSelectedOnGitHub(); };
            actions.Controls.Add(_openCommitLink);

            _githubLink = MakeToolbarLink("Open on GitHub");
            _githubLink.Margin = new Padding(12, 8, 0, 0);
            _githubLink.Visible = false;
            _githubLink.LinkClicked += delegate
            {
                string url = BuildGithubUrl();
                if (url.Length > 0)
                {
                    UiTheme.OpenUrl(url);
                }
            };
            actions.Controls.Add(_githubLink);

            UpdateLabels();
            _graph.SetStatus("Commit history", "Choose this tab to load the repository history.");
            ResetDetail();
        }

        public CommitGraphView GraphView
        {
            get { return _graph; }
        }

        public CommitSectionTabs SectionTabs
        {
            get { return _sectionTabs; }
        }

        public CommitMessageView MessageView
        {
            get { return _messageView; }
        }

        public CommitFilesView FilesView
        {
            get { return _filesView; }
        }

        public CommitDiffView DiffView
        {
            get { return _diffView; }
        }

        public void Bind(RepoRecord record)
        {
            _record = record;
            _loading = false;
            _loaded = false;
            _refreshButton.Enabled = true;
            ResetDetail();
            _graph.SetStatus("Commit history", "Choose this tab to load the repository history.");
            UpdateLabels();
        }

        // Loads cached commits immediately, or starts a background read the first time
        // the History tab becomes visible.
        public void EnsureLoaded()
        {
            if (_loaded || _loading || _record == null)
            {
                return;
            }

            _loaded = true;
            if (IsLocal)
            {
                LoadLocalHistory();
                return;
            }

            List<CommitInfo> cached = _record.Commits;
            if (cached != null && cached.Count > 0)
            {
                ShowCommits(cached, "cached from GitHub", false);
                return;
            }

            if (CanFetch)
            {
                StartFetch();
                return;
            }

            _countLabel.Text = "History unavailable";
            _graph.SetStatus(
                "No commit history",
                "This repository has no cached commits and no GitHub address. Sync it to download its history.");
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

        private bool IsLocal
        {
            get
            {
                return _record == null ||
                    string.Equals(_record.Owner, "local", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrEmpty(_record.HtmlUrl);
            }
        }

        private bool CanFetch
        {
            get
            {
                return _record != null &&
                    !string.IsNullOrEmpty(_record.Owner) &&
                    !string.IsNullOrEmpty(_record.Name) &&
                    !string.Equals(_record.Owner, "local", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(_record.HtmlUrl);
            }
        }

        private bool CanUseLocalGit
        {
            get
            {
                return _record != null &&
                    !string.IsNullOrEmpty(_record.SourcePath) &&
                    GitHistoryReader.HasGitDirectory(_record.SourcePath);
            }
        }

        private void UpdateLabels()
        {
            string branch = _record == null ? string.Empty : _record.Branch;
            if (string.IsNullOrEmpty(branch) || string.Equals(branch, "default", StringComparison.OrdinalIgnoreCase))
            {
                branch = _record == null ? string.Empty : _record.DefaultBranch;
            }

            if (string.Equals(branch, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                branch = IsLocal ? "Local repository" : "Detached HEAD";
            }

            if (string.IsNullOrEmpty(branch) || string.Equals(branch, "default", StringComparison.OrdinalIgnoreCase))
            {
                branch = "Default branch";
            }

            _branchLabel.Text = branch;
            _countLabel.Text = IsLocal ? "Read from the local .git folder" : "Commits cached from GitHub";
            _githubLink.Visible = BuildGithubUrl().Length > 0;
            UpdateOpenCommitLink();
        }

        private void ToolbarPaint(object sender, PaintEventArgs e)
        {
            using (Pen border = new Pen(UiTheme.Border))
            {
                e.Graphics.DrawLine(border, 0, _toolbar.Height - 1, _toolbar.Width, _toolbar.Height - 1);
            }
        }

        private void RefreshHistory()
        {
            if (_record == null || _loading)
            {
                return;
            }

            ResetDetail();
            _loaded = true;
            if (IsLocal)
            {
                LoadLocalHistory();
            }
            else
            {
                StartFetch();
            }
        }

        private void LoadLocalHistory()
        {
            string source = _record == null ? string.Empty : _record.SourcePath;
            if (string.IsNullOrEmpty(source))
            {
                _countLabel.Text = "History unavailable";
                _graph.SetStatus(
                    "History is not linked",
                    "This local copy was added before history support. Drag the original repository folder in again to link its .git directory.");
                return;
            }

            if (!GitHistoryReader.HasGitDirectory(source))
            {
                _countLabel.Text = "History unavailable";
                _graph.SetStatus(
                    "The .git folder is missing",
                    "History is read from " + source + ", but that folder no longer contains a .git directory. Re-add the repository to refresh the link.");
                return;
            }

            SetLoading("Reading commits from the local .git folder...");
            RepoRecord target = _record;

            ThreadPool.QueueUserWorkItem(delegate
            {
                GitHistoryResult result = null;
                string error = null;
                try
                {
                    result = GitHistoryReader.Load(source, 300);
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                Post(target, delegate
                {
                    _loading = false;
                    _refreshButton.Enabled = true;
                    if (result == null)
                    {
                        _countLabel.Text = "History unavailable";
                        _graph.SetStatus(
                            "Could not read the git history",
                            string.IsNullOrEmpty(error) ? "git log returned no data." : error);
                        return;
                    }

                    if (!string.IsNullOrEmpty(result.Branch))
                    {
                        _branchLabel.Text = result.Branch;
                    }

                    ShowCommits(result.Commits, "local .git", result.Truncated);
                });
            });
        }

        private void StartFetch()
        {
            SetLoading("Fetching the latest commits from GitHub...");
            RepoRecord target = _record;
            string token = _store != null && _store.Settings != null
                ? _store.Settings.GitHubToken
                : string.Empty;
            GitHubDownloader downloader = new GitHubDownloader(_store, token);

            ThreadPool.QueueUserWorkItem(delegate
            {
                List<CommitInfo> commits = null;
                string error = null;
                try
                {
                    commits = downloader.FetchCommitsAsync(target, 100, CancellationToken.None).Result;
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

                if (commits != null && commits.Count > 0)
                {
                    target.Commits = commits;
                    target.CommitsUpdatedAtUtc = DateTime.UtcNow;
                    try
                    {
                        _store.SaveRecord(target);
                    }
                    catch
                    {
                    }
                }

                Post(target, delegate
                {
                    _loading = false;
                    _refreshButton.Enabled = true;
                    if (!string.IsNullOrEmpty(error))
                    {
                        _countLabel.Text = "History unavailable";
                        _graph.SetStatus("Could not load commits", error);
                        return;
                    }

                    if (commits == null || commits.Count == 0)
                    {
                        _countLabel.Text = "No commits loaded";
                        _graph.SetStatus(
                            "No commits loaded",
                            "GitHub returned no commits for this branch. Check the branch name and your connection, then refresh.");
                        return;
                    }

                    ShowCommits(commits, "cached from GitHub", false);
                });
            });
        }

        private void SetLoading(string message)
        {
            _loading = true;
            _refreshButton.Enabled = false;
            _countLabel.Text = "Loading commit history...";
            _graph.SetStatus("Loading commit history", message);
        }

        private void ShowCommits(List<CommitInfo> commits, string source, bool truncated)
        {
            int count = commits == null ? 0 : commits.Count;
            if (count == 0)
            {
                _countLabel.Text = "No commits";
                _graph.SetStatus(
                    "No commits found",
                    "The branch has no commits, or the history could not be read.");
                return;
            }

            ResetDetail();
            string label = count == 1 ? "1 commit" : count + (truncated ? "+" : string.Empty) + " commits";
            _countLabel.Text = label + "  \u00b7  " + source;
            _graph.SetCommits(commits);
        }

        private string BuildGithubUrl()
        {
            if (_record == null || string.IsNullOrEmpty(_record.HtmlUrl))
            {
                return string.Empty;
            }

            string url = _record.HtmlUrl.TrimEnd('/') + "/commits";
            string branch = _record.Branch;
            if (string.IsNullOrEmpty(branch) || string.Equals(branch, "default", StringComparison.OrdinalIgnoreCase))
            {
                branch = _record.DefaultBranch;
            }

            if (!string.IsNullOrEmpty(branch) &&
                !string.Equals(branch, "HEAD", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(branch, "default", StringComparison.OrdinalIgnoreCase))
            {
                url += "/" + branch.Trim('/');
            }

            return url;
        }

        private string BuildCommitUrl(CommitInfo commit)
        {
            if (commit == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(commit.HtmlUrl))
            {
                return commit.HtmlUrl;
            }

            if (_record != null &&
                !string.IsNullOrEmpty(_record.HtmlUrl) &&
                !string.Equals(_record.Owner, "local", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(commit.Sha))
            {
                return _record.HtmlUrl.TrimEnd('/') + "/commit/" + commit.Sha;
            }

            return string.Empty;
        }

        private void UpdateOpenCommitLink()
        {
            bool visible = BuildCommitUrl(_graph.SelectedCommit).Length > 0;
            if (_openCommitLink.Visible != visible)
            {
                _openCommitLink.Visible = visible;
            }
        }

        private void OpenSelectedOnGitHub()
        {
            string url = BuildCommitUrl(_graph.SelectedCommit);
            if (url.Length > 0)
            {
                UiTheme.OpenUrl(url);
            }
        }

        // Double-clicking the graph (or pressing Enter) opens the readable detail
        // rather than leaving the app for GitHub.
        private void OpenSelectedCommitDetail()
        {
            if (_graph.SelectedCommit == null)
            {
                return;
            }

            _sectionTabs.SelectedIndex = CommitSectionTabs.MessageIndex;
            EnsureDetail();
        }

        private void OpenSelectedFileDiff()
        {
            int index = _filesView.SelectedIndex;
            if (index < 0)
            {
                return;
            }

            _pendingDiffFile = index;
            _sectionTabs.SelectedIndex = CommitSectionTabs.DiffIndex;
            if (_detail != null)
            {
                _diffView.ScrollToFile(index);
                _pendingDiffFile = -1;
            }
        }

        private void ShowSection()
        {
            _section = _sectionTabs.SelectedIndex;

            _graph.Visible = _section == CommitSectionTabs.GraphIndex;
            _messageView.Visible = _section == CommitSectionTabs.MessageIndex;
            _filesView.Visible = _section == CommitSectionTabs.FilesIndex;
            _diffView.Visible = _section == CommitSectionTabs.DiffIndex;

            if (_section != CommitSectionTabs.GraphIndex)
            {
                EnsureDetail();
            }
        }

        private void OnCommitSelectionChanged()
        {
            CommitInfo commit = _graph.SelectedCommit;
            UpdateOpenCommitLink();

            if (commit == null)
            {
                ClearDetailState(
                    "No commit selected",
                    "Choose a commit in the graph to read its message, files and diff.");
                return;
            }

            if (!string.Equals(_detailSha, commit.Sha, StringComparison.OrdinalIgnoreCase))
            {
                _detailSha = commit.Sha;
                _detail = null;
                _detailLoading = false;
                _detailError = null;
                _pendingDiffFile = -1;
                ShowDetailStatus(
                    "Commit " + ShortSha(commit.Sha),
                    "Choose the Commit message, Files or Diff tab to load this commit.");
            }

            if (_section != CommitSectionTabs.GraphIndex)
            {
                EnsureDetail();
            }
        }

        private void ClearDetailState(string title, string message)
        {
            _detailSha = null;
            _detail = null;
            _detailLoading = false;
            _detailError = null;
            _pendingDiffFile = -1;
            ShowDetailStatus(title, message);
        }

        private void ResetDetail()
        {
            _detailSha = null;
            _detail = null;
            _detailLoading = false;
            _detailError = null;
            _pendingDiffFile = -1;

            if (_sectionTabs.SelectedIndex != CommitSectionTabs.GraphIndex)
            {
                _sectionTabs.SelectedIndex = CommitSectionTabs.GraphIndex;
            }
            else
            {
                ShowSection();
            }

            ShowDetailStatus(
                "Commit details",
                "Select a commit in the graph, then choose Commit message, Files or Diff.");
            UpdateOpenCommitLink();
        }

        private void EnsureDetail()
        {
            if (_record == null || string.IsNullOrEmpty(_detailSha))
            {
                return;
            }

            if (_detail != null)
            {
                return;
            }

            if (_detailLoading)
            {
                return;
            }

            if (!string.IsNullOrEmpty(_detailError))
            {
                ShowDetailStatus("Could not load commit", _detailError);
                return;
            }

            BeginLoadDetail(_detailSha);
        }

        private void BeginLoadDetail(string sha)
        {
            if (string.IsNullOrEmpty(sha))
            {
                return;
            }

            CommitDetail cached = CommitCache.Read(_record, sha);
            if (cached != null)
            {
                ApplyDetail(cached, sha);
                return;
            }

            bool useLocal = CanUseLocalGit;
            bool useGitHub = !useLocal && CanFetch;
            if (!useLocal && !useGitHub)
            {
                _detailError = "This repository has no local .git folder and no GitHub address, so its commits cannot be loaded.";
                ShowDetailStatus("Commit details unavailable", _detailError);
                return;
            }

            _detailLoading = true;
            _detailError = null;
            ShowDetailStatus(
                "Loading commit " + ShortSha(sha),
                useLocal
                    ? "Reading the message and diff from the local .git folder..."
                    : "Fetching the commit message and diff from GitHub...");

            RepoRecord target = _record;
            string source = target.SourcePath;
            string token = _store != null && _store.Settings != null
                ? _store.Settings.GitHubToken
                : string.Empty;

            ThreadPool.QueueUserWorkItem(delegate
            {
                CommitDetail detail = null;
                string error = null;
                try
                {
                    if (useLocal)
                    {
                        detail = GitHistoryReader.LoadCommitDetail(source, sha);
                    }
                    else
                    {
                        GitHubDownloader downloader = new GitHubDownloader(_store, token);
                        detail = downloader.FetchCommitDetailAsync(
                            target,
                            sha,
                            CancellationToken.None).Result;
                    }

                    if (detail != null)
                    {
                        CommitCache.Write(target, detail);
                    }
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

                Post(target, delegate
                {
                    if (!string.Equals(_detailSha, sha, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }

                    _detailLoading = false;
                    if (detail == null)
                    {
                        _detailError = string.IsNullOrEmpty(error)
                            ? "The commit details could not be read."
                            : error;
                        ShowDetailStatus("Could not load commit", _detailError);
                        return;
                    }

                    ApplyDetail(detail, sha);
                });
            });
        }

        private void ApplyDetail(CommitDetail detail, string sha)
        {
            _detail = detail;
            _detailLoading = false;
            _detailError = null;
            if (detail != null && !string.IsNullOrEmpty(detail.Sha))
            {
                _detailSha = detail.Sha;
            }
            else
            {
                _detailSha = sha;
            }

            _messageView.SetDetail(detail);
            _filesView.SetFiles(detail);
            _diffView.SetDetail(detail);

            if (_pendingDiffFile >= 0)
            {
                _diffView.ScrollToFile(_pendingDiffFile);
                _pendingDiffFile = -1;
            }

            UpdateOpenCommitLink();
        }

        private void ShowDetailStatus(string title, string message)
        {
            _messageView.SetStatus(title, message);
            _filesView.SetStatus(title, message);
            _diffView.SetStatus(title, message);
        }

        private static string ShortSha(string sha)
        {
            if (string.IsNullOrEmpty(sha))
            {
                return string.Empty;
            }

            return sha.Length > 7 ? sha.Substring(0, 7) : sha;
        }

        private void Post(RepoRecord target, MethodInvoker action)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    return;
                }

                BeginInvoke((MethodInvoker)delegate
                {
                    if (IsDisposed || !ReferenceEquals(_record, target))
                    {
                        return;
                    }

                    action();
                });
            }
            catch
            {
                // The view was closed while the background read was still running.
            }
        }
    }
}
