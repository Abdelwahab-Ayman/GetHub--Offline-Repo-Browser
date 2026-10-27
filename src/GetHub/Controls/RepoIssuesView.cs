using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Rendering;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // The Issues tab. The list, filters and every issue thread are rendered in the
    // embedded browser. Signed-in users get fresh data from the REST API; when the
    // network is unavailable the last downloaded copy is shown instead.
    public class RepoIssuesView : GitHubSectionView
    {
        private static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan DetailRefreshAfter = TimeSpan.FromMinutes(10);

        private List<IssueSummary> _issues;
        private DateTime _updatedAtUtc = DateTime.MinValue;
        private string _filter = "open";
        private int _openNumber;
        private bool _listLoading;
        private bool _detailLoading;
        private CancellationTokenSource _listCancellation;
        private CancellationTokenSource _detailCancellation;
        private string _lastNotice;
        private bool _lastNoticeIsError;

        public RepoIssuesView(RepoStore store, RepoRecord record)
            : base(store, record, "Issues", "issues")
        {
            UpdateGithubUrl();
            SetStatus("Choose this tab to load issues.");
        }

        public int OpenIssueNumber
        {
            get { return _openNumber; }
        }

        public void OpenIssue(int number)
        {
            if (number <= 0)
            {
                return;
            }

            if (IsLocal(Record))
            {
                RenderLocal();
                return;
            }

            _openNumber = number;
            _lastNotice = null;
            _lastNoticeIsError = false;
            IssueDetail cached = IssueCache.ReadDetail(Record, number);
            if (cached != null)
            {
                RenderDetail(cached, null, false);
                if (IsSignedIn && IsDetailStale(number))
                {
                    StartDetailFetch(number);
                }

                return;
            }

            if (!IsSignedIn)
            {
                RenderSignInPage("Sign in to open issue #" + number + " and read its comments.");
                SetStatus("Not signed in");
                return;
            }

            RenderHtml(IssuePageBuilder.BuildMessagePage(
                Record,
                "Opening issue #" + number,
                "Downloading the issue and its comments from GitHub...",
                true));
            SetStatus("Loading issue #" + number + " from GitHub...");
            StartDetailFetch(number);
        }

        protected override void OnBind(RepoRecord record)
        {
            CancelLoading();
            _issues = null;
            _updatedAtUtc = DateTime.MinValue;
            _openNumber = 0;
            _lastNotice = null;
            _lastNoticeIsError = false;
            UpdateGithubUrl();
            SetStatus("Choose this tab to load issues.");
        }

        protected override void OnEnsureLoaded()
        {
            if (IsLocal(Record))
            {
                RenderLocal();
                return;
            }

            IssueCache.Index index = IssueCache.ReadIndex(Record);
            if (index != null)
            {
                _issues = index.Issues;
                _updatedAtUtc = index.UpdatedAtUtc;
                RenderList(
                    IsSignedIn ? null : "You are signed out. Showing the last issues downloaded from GitHub.",
                    false);
                if (IsSignedIn && IsStale(_updatedAtUtc, RefreshAfter))
                {
                    StartListFetch();
                }

                return;
            }

            if (!IsSignedIn)
            {
                RenderSignInPage(null);
                SetStatus("Not signed in");
                return;
            }

            StartListFetch();
        }

        protected override void OnReload()
        {
            if (IsLocal(Record))
            {
                RenderLocal();
                return;
            }

            if (!IsSignedIn)
            {
                if (_issues != null)
                {
                    RenderList("You are signed out. Showing the last issues downloaded from GitHub.", false);
                }
                else
                {
                    RenderSignInPage("Sign in to download this repository's issues into GetHub.");
                }

                SetStatus("Not signed in");
                return;
            }

            UpdateGithubUrl();
            if (_openNumber > 0)
            {
                StartDetailFetch(_openNumber);
                return;
            }

            StartListFetch();
        }

        protected override void OnAccountChanged()
        {
            if (!Visible)
            {
                return;
            }

            if (IsSignedIn)
            {
                if (_openNumber > 0)
                {
                    StartDetailFetch(_openNumber);
                }
                else
                {
                    StartListFetch();
                }

                return;
            }

            if (_issues != null)
            {
                RenderList("You signed out. Showing the last issues downloaded from GitHub.", false);
            }
            else
            {
                RenderSignInPage(null);
            }

            SetStatus("Not signed in");
        }

        protected override bool OnAppUrl(string host, Uri uri)
        {
            if (host == "issues")
            {
                string state = GetQueryValue(uri, "state");
                if (state.Length == 0)
                {
                    state = "open";
                }

                _filter = state;
                _openNumber = 0;
                _lastNotice = null;
                _lastNoticeIsError = false;
                if (_issues == null)
                {
                    OnEnsureLoaded();
                }
                else
                {
                    RenderList(null, false);
                }

                return true;
            }

            if (host == "issue")
            {
                int number;
                if (!int.TryParse(GetQueryValue(uri, "number"), out number) || number <= 0)
                {
                    return true;
                }

                string refresh = GetQueryValue(uri, "refresh");
                _openNumber = number;
                if (refresh == "1" && IsSignedIn)
                {
                    IssueDetail cached = IssueCache.ReadDetail(Record, number);
                    if (cached != null)
                    {
                        RenderDetail(cached, null, false);
                    }
                    else
                    {
                        RenderHtml(IssuePageBuilder.BuildMessagePage(
                            Record,
                            "Opening issue #" + number,
                            "Downloading the issue and its comments from GitHub...",
                            true));
                        SetStatus("Loading issue #" + number + " from GitHub...");
                    }

                    StartDetailFetch(number);
                    return true;
                }

                OpenIssue(number);
                return true;
            }

            return false;
        }

        private void StartListFetch()
        {
            if (_listLoading || IsLocal(Record))
            {
                return;
            }

            _listLoading = true;
            SetBusy(true);
            SetStatus(_issues == null
                ? "Loading issues from GitHub..."
                : "Refreshing issues from GitHub...");

            CancellationTokenSource cancellation = new CancellationTokenSource();
            _listCancellation = cancellation;
            FetchListAsync(cancellation);
        }

        private async void FetchListAsync(CancellationTokenSource cancellation)
        {
            string error = null;
            List<IssueSummary> issues = null;
            try
            {
                GitHubIssueService service = new GitHubIssueService(AccessToken);
                issues = await service.FetchIssuesAsync(Record, cancellation.Token);
                if (cancellation.IsCancellationRequested || IsDisposed)
                {
                    return;
                }

                _issues = issues;
                _updatedAtUtc = DateTime.UtcNow;
                IssueCache.WriteIndex(Record, issues);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (GitHubException ex)
            {
                error = ex.Message;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                if (ReferenceEquals(_listCancellation, cancellation))
                {
                    _listCancellation = null;
                    _listLoading = false;
                }

                try
                {
                    cancellation.Dispose();
                }
                catch
                {
                }
            }

            if (IsDisposed || Disposing)
            {
                return;
            }

            SetBusy(false);
            if (error != null)
            {
                string notice = "Could not refresh from GitHub: " + Safe(error);
                if (_issues != null)
                {
                    RenderList(notice + " Showing the last downloaded list.", true);
                }
                else
                {
                    RenderHtml(IssuePageBuilder.BuildMessagePage(
                        Record,
                        "Could not load issues",
                        error,
                        IsSignedIn));
                    SetStatus("Could not load issues");
                }

                return;
            }

            if (_openNumber == 0)
            {
                RenderList(null, false);
            }
            else
            {
                UpdateCountStatus();
            }
        }

        private void StartDetailFetch(int number)
        {
            if (_detailLoading || number <= 0 || IsLocal(Record))
            {
                return;
            }

            _detailLoading = true;
            SetBusy(true);
            SetStatus("Loading issue #" + number + " from GitHub...");

            CancellationTokenSource cancellation = new CancellationTokenSource();
            _detailCancellation = cancellation;
            FetchDetailAsync(number, cancellation);
        }

        private async void FetchDetailAsync(int number, CancellationTokenSource cancellation)
        {
            string error = null;
            IssueDetail detail = null;
            try
            {
                GitHubIssueService service = new GitHubIssueService(AccessToken);
                detail = await service.FetchIssueAsync(Record, number, cancellation.Token);
                if (cancellation.IsCancellationRequested || IsDisposed)
                {
                    return;
                }

                IssueCache.WriteDetail(Record, detail);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (GitHubException ex)
            {
                error = ex.Message;
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
            finally
            {
                if (ReferenceEquals(_detailCancellation, cancellation))
                {
                    _detailCancellation = null;
                    _detailLoading = false;
                }

                try
                {
                    cancellation.Dispose();
                }
                catch
                {
                }
            }

            if (IsDisposed || Disposing)
            {
                return;
            }

            SetBusy(false);
            if (_openNumber != number)
            {
                return;
            }

            if (error != null)
            {
                IssueDetail cached = IssueCache.ReadDetail(Record, number);
                if (cached != null)
                {
                    RenderDetail(
                        cached,
                        "Could not refresh this issue: " + Safe(error) + " Showing the cached copy.",
                        true);
                }
                else
                {
                    RenderHtml(IssuePageBuilder.BuildMessagePage(
                        Record,
                        "Could not load issue #" + number,
                        error,
                        IsSignedIn));
                    SetStatus("Could not load issue #" + number);
                }

                return;
            }

            RenderDetail(detail, null, false);
        }

        private void RenderList(string notice, bool isError)
        {
            _openNumber = 0;
            _lastNotice = notice;
            _lastNoticeIsError = isError;
            string html = IssuePageBuilder.BuildListPage(
                Record,
                _issues,
                _filter,
                _updatedAtUtc,
                notice,
                isError,
                IsSignedIn);
            RenderHtml(html);
            UpdateCountStatus();
        }

        private void RenderDetail(IssueDetail detail, string notice, bool isError)
        {
            _lastNotice = notice;
            _lastNoticeIsError = isError;
            DateTime cachedAt = GetDetailCachedAt(detail.Number);
            RenderHtml(IssuePageBuilder.BuildDetailPage(
                Record,
                detail,
                cachedAt,
                notice,
                isError,
                IsSignedIn));

            SetStatus("#" + detail.Number + " " + detail.Title);
        }

        private void RenderSignInPage(string message)
        {
            _openNumber = 0;
            RenderHtml(IssuePageBuilder.BuildSignInPage(Record, message == null ? null : Safe(message)));
        }

        private void RenderLocal()
        {
            SetGithubUrl(string.Empty);
            SetStatus("Local folder - issues live on GitHub");
            RenderHtml(IssuePageBuilder.BuildLocalPage(Record));
        }

        private void UpdateCountStatus()
        {
            if (_issues == null)
            {
                SetStatus(IsSignedIn ? "No issues loaded yet" : "Not signed in");
                return;
            }

            int open = 0;
            foreach (IssueSummary issue in _issues)
            {
                if (issue.IsOpen)
                {
                    open++;
                }
            }

            string status = _issues.Count + " " + (_issues.Count == 1 ? "issue" : "issues") +
                " (" + open + " open)";
            if (_updatedAtUtc != DateTime.MinValue)
            {
                status += " - cached " + Display.RelativeTime(_updatedAtUtc);
            }

            if (!IsSignedIn)
            {
                status += " - offline copy";
            }

            SetStatus(status);
        }

        private void UpdateGithubUrl()
        {
            if (Record == null || string.IsNullOrEmpty(Record.HtmlUrl))
            {
                SetGithubUrl(string.Empty);
                return;
            }

            SetGithubUrl(Record.HtmlUrl.TrimEnd('/') + "/issues");
        }

        private DateTime GetDetailCachedAt(int number)
        {
            try
            {
                string path = IssueCache.GetDetailPath(Record, number);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    return File.GetLastWriteTimeUtc(path);
                }
            }
            catch
            {
            }

            return DateTime.MinValue;
        }

        private bool IsDetailStale(int number)
        {
            DateTime cachedAt = GetDetailCachedAt(number);
            return cachedAt == DateTime.MinValue || IsStale(cachedAt, DetailRefreshAfter);
        }

        private static bool IsStale(DateTime utc, TimeSpan maxAge)
        {
            if (utc == DateTime.MinValue)
            {
                return true;
            }

            return DateTime.UtcNow - utc > maxAge;
        }

        private void CancelLoading()
        {
            Cancel(_listCancellation);
            Cancel(_detailCancellation);
            _listCancellation = null;
            _detailCancellation = null;
            _listLoading = false;
            _detailLoading = false;
        }

        private static void Cancel(CancellationTokenSource cancellation)
        {
            if (cancellation == null)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch
            {
            }
        }

        private static string Safe(string text)
        {
            return WebUtility.HtmlEncode(text ?? string.Empty);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CancelLoading();
            }

            base.Dispose(disposing);
        }
    }
}
