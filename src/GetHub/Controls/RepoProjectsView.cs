using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Rendering;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Controls
{
    // The Projects tab. Boards are loaded through the GraphQL API (Projects v2),
    // laid out as status columns in the embedded browser, and stored as a single
    // JSON snapshot so the last download keeps working offline.
    public class RepoProjectsView : GitHubSectionView
    {
        private static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(15);

        private List<ProjectSummary> _projects;
        private DateTime _updatedAtUtc = DateTime.MinValue;
        private int _openNumber;
        private bool _loading;
        private CancellationTokenSource _cancellation;

        public RepoProjectsView(RepoStore store, RepoRecord record)
            : base(store, record, "Projects", "projects")
        {
            UpdateGithubUrl();
            SetStatus("Choose this tab to load project boards.");
        }

        public void OpenProject(int number)
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

            ProjectSummary cached = FindProject(number);
            if (cached != null)
            {
                _openNumber = number;
                RenderBoard(cached, null, false);
                return;
            }

            if (!IsSignedIn)
            {
                RenderSignInPage("Sign in to open project #" + number + " and read its cards.");
                SetStatus("Not signed in");
                return;
            }

            _openNumber = number;
            RenderHtml(ProjectPageBuilder.BuildMessagePage(
                Record,
                "Opening project #" + number,
                "Downloading the board and its cards from GitHub...",
                true));
            SetStatus("Loading project #" + number + " from GitHub...");
            StartFetch();
        }

        protected override void OnBind(RepoRecord record)
        {
            CancelLoading();
            _projects = null;
            _updatedAtUtc = DateTime.MinValue;
            _openNumber = 0;
            UpdateGithubUrl();
            SetStatus("Choose this tab to load project boards.");
        }

        protected override void OnEnsureLoaded()
        {
            if (IsLocal(Record))
            {
                RenderLocal();
                return;
            }

            ProjectCache.Snapshot snapshot = ProjectCache.Read(Record);
            if (snapshot != null)
            {
                _projects = snapshot.Projects;
                _updatedAtUtc = snapshot.UpdatedAtUtc;
                RenderList(
                    IsSignedIn ? null : "You are signed out. Showing the last project boards downloaded from GitHub.",
                    false);
                if (IsSignedIn && IsStale(_updatedAtUtc))
                {
                    StartFetch();
                }

                return;
            }

            if (!IsSignedIn)
            {
                RenderSignInPage(null);
                SetStatus("Not signed in");
                return;
            }

            StartFetch();
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
                if (_projects != null)
                {
                    RenderList("You are signed out. Showing the last project boards downloaded from GitHub.", false);
                }
                else
                {
                    RenderSignInPage("Sign in to download this repository's project boards into GetHub.");
                }

                SetStatus("Not signed in");
                return;
            }

            UpdateGithubUrl();
            StartFetch();
        }

        protected override void OnAccountChanged()
        {
            if (!Visible)
            {
                return;
            }

            if (IsSignedIn)
            {
                StartFetch();
                return;
            }

            if (_projects != null)
            {
                RenderList("You signed out. Showing the last project boards downloaded from GitHub.", false);
            }
            else
            {
                RenderSignInPage(null);
            }

            SetStatus("Not signed in");
        }

        protected override bool OnAppUrl(string host, Uri uri)
        {
            if (host == "projects")
            {
                _openNumber = 0;
                if (_projects == null)
                {
                    OnEnsureLoaded();
                }
                else
                {
                    RenderList(null, false);
                }

                return true;
            }

            if (host == "project")
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
                    ProjectSummary cached = FindProject(number);
                    if (cached != null)
                    {
                        RenderBoard(cached, null, false);
                    }

                    StartFetch();
                    return true;
                }

                OpenProject(number);
                return true;
            }

            if (host == "issue")
            {
                int number;
                if (int.TryParse(GetQueryValue(uri, "number"), out number) && number > 0)
                {
                    OnIssueRequested(number);
                }

                return true;
            }

            return false;
        }

        private void StartFetch()
        {
            if (_loading || IsLocal(Record))
            {
                return;
            }

            _loading = true;
            SetBusy(true);
            SetStatus(_projects == null
                ? "Loading projects from GitHub..."
                : "Refreshing projects from GitHub...");

            CancellationTokenSource cancellation = new CancellationTokenSource();
            _cancellation = cancellation;
            FetchAsync(cancellation);
        }

        private async void FetchAsync(CancellationTokenSource cancellation)
        {
            string error = null;
            List<ProjectSummary> projects = null;
            try
            {
                GitHubProjectService service = new GitHubProjectService(AccessToken);
                projects = await service.FetchProjectsAsync(Record, cancellation.Token);
                if (cancellation.IsCancellationRequested || IsDisposed)
                {
                    return;
                }

                _projects = projects;
                _updatedAtUtc = DateTime.UtcNow;
                ProjectCache.Write(Record, projects);
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
                if (ReferenceEquals(_cancellation, cancellation))
                {
                    _cancellation = null;
                    _loading = false;
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
                if (_projects != null)
                {
                    if (_openNumber > 0)
                    {
                        ProjectSummary cached = FindProject(_openNumber);
                        if (cached != null)
                        {
                            RenderBoard(cached, notice + " Showing the cached board.", true);
                            return;
                        }
                    }

                    RenderList(notice + " Showing the last downloaded boards.", true);
                }
                else
                {
                    RenderHtml(ProjectPageBuilder.BuildMessagePage(
                        Record,
                        "Could not load projects",
                        error,
                        IsSignedIn));
                    SetStatus("Could not load projects");
                }

                return;
            }

            if (_openNumber > 0)
            {
                ProjectSummary current = FindProject(_openNumber);
                if (current != null)
                {
                    RenderBoard(current, null, false);
                    return;
                }

                _openNumber = 0;
                RenderList("That project is no longer on this repository. Showing all projects.", false);
                return;
            }

            RenderList(null, false);
        }

        private void RenderList(string notice, bool isError)
        {
            _openNumber = 0;
            RenderHtml(ProjectPageBuilder.BuildListPage(
                Record,
                _projects,
                _updatedAtUtc,
                notice,
                isError,
                IsSignedIn));

            if (_projects == null)
            {
                SetStatus(IsSignedIn ? "No projects loaded yet" : "Not signed in");
                return;
            }

            string status = _projects.Count + (_projects.Count == 1 ? " project" : " projects");
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

        private void RenderBoard(ProjectSummary project, string notice, bool isError)
        {
            if (project == null)
            {
                RenderList(notice, isError);
                return;
            }

            RenderHtml(ProjectPageBuilder.BuildBoardPage(
                Record,
                project,
                _updatedAtUtc,
                notice,
                isError,
                IsSignedIn));
            SetStatus(project.Title + " - " + project.ItemCountLabel);
        }

        private void RenderSignInPage(string message)
        {
            _openNumber = 0;
            RenderHtml(ProjectPageBuilder.BuildSignInPage(Record, message == null ? null : Safe(message)));
        }

        private void RenderLocal()
        {
            SetGithubUrl(string.Empty);
            SetStatus("Local folder - projects live on GitHub");
            RenderHtml(ProjectPageBuilder.BuildLocalPage(Record));
        }

        private ProjectSummary FindProject(int number)
        {
            if (_projects == null)
            {
                return null;
            }

            foreach (ProjectSummary project in _projects)
            {
                if (project.Number == number)
                {
                    return project;
                }
            }

            return null;
        }

        private void UpdateGithubUrl()
        {
            if (Record == null || string.IsNullOrEmpty(Record.HtmlUrl))
            {
                SetGithubUrl(string.Empty);
                return;
            }

            SetGithubUrl(Record.HtmlUrl.TrimEnd('/') + "/projects");
        }

        private static bool IsStale(DateTime utc)
        {
            if (utc == DateTime.MinValue)
            {
                return true;
            }

            return DateTime.UtcNow - utc > RefreshAfter;
        }

        private void CancelLoading()
        {
            if (_cancellation != null)
            {
                try
                {
                    _cancellation.Cancel();
                }
                catch
                {
                }
            }

            _cancellation = null;
            _loading = false;
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
