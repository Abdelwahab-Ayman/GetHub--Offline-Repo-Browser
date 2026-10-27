using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using GetHub.Models;

namespace GetHub.Services
{
    public class GitHubDownloader
    {
        private const string UserAgent = "GetHub/1.0";
        private static readonly Regex NamePattern = new Regex(@"^[A-Za-z0-9_.-]+$", RegexOptions.Compiled);

        private readonly RepoStore _store;
        private readonly string _token;

        static GitHubDownloader()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
            }
        }

        public GitHubDownloader(RepoStore store, string token)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            _store = store;
            _token = token == null ? string.Empty : token.Trim();
        }

        public static RepoReference ParseReference(string input, string branchOverride)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                throw new FormatException("Enter a GitHub repository address.");
            }

            string value = input.Trim();
            if (value.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            {
                value = "https://github.com/" + value.Substring("git@github.com:".Length);
            }

            if (!value.Contains("://"))
            {
                value = "https://" + value.TrimStart('/');
            }

            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri))
            {
                throw new FormatException("That does not look like a valid GitHub URL.");
            }

            string host = uri.Host.ToLowerInvariant();
            if (host != "github.com" && host != "www.github.com")
            {
                throw new FormatException("Only github.com repository addresses are supported.");
            }

            string path = Uri.UnescapeDataString(uri.AbsolutePath);
            string[] segments = path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
            {
                throw new FormatException("The URL should look like https://github.com/owner/repository.");
            }

            string owner = segments[0];
            string name = segments[1];
            if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            if (!NamePattern.IsMatch(owner))
            {
                throw new FormatException("The repository owner contains invalid characters.");
            }

            if (!NamePattern.IsMatch(name))
            {
                throw new FormatException("The repository name contains invalid characters.");
            }

            string branch = string.Empty;
            for (int i = 2; i < segments.Length - 1; i++)
            {
                if (string.Equals(segments[i], "tree", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segments[i], "blob", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(segments[i], "raw", StringComparison.OrdinalIgnoreCase))
                {
                    branch = segments[i + 1];
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(branchOverride))
            {
                branch = branchOverride.Trim();
            }

            if (branch.Length > 0)
            {
                if (branch.IndexOfAny(new char[] { ' ', '\t', '\r', '\n' }) >= 0)
                {
                    throw new FormatException("The branch name contains invalid whitespace.");
                }
            }

            RepoReference reference = new RepoReference();
            reference.Owner = owner;
            reference.Name = name;
            reference.Branch = branch;
            return reference;
        }

        public async Task<RepoRecord> DownloadAsync(
            RepoReference reference,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            if (reference == null)
            {
                throw new ArgumentNullException("reference");
            }

            string zipPath = Path.Combine(_store.TempPath, Guid.NewGuid().ToString("N") + ".zip");
            string extractRoot = null;

            try
            {
                Report(progress, DownloadProgress.Busy("Contacting GitHub..."));

                RepoInfo info = await FetchRepoInfoAsync(
                    reference.Owner,
                    reference.Name,
                    cancellationToken).ConfigureAwait(false);

                string branch = string.IsNullOrWhiteSpace(reference.Branch)
                    ? info.DefaultBranch
                    : reference.Branch.Trim();

                if (string.IsNullOrEmpty(branch))
                {
                    branch = "HEAD";
                }

                string archiveUrl = "https://api.github.com/repos/" +
                    Uri.EscapeDataString(reference.Owner) + "/" +
                    Uri.EscapeDataString(reference.Name) + "/zipball/" +
                    Uri.EscapeDataString(branch);

                await DownloadArchiveAsync(
                    archiveUrl,
                    zipPath,
                    progress,
                    cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                Report(progress, DownloadProgress.Busy("Extracting repository files..."));

                string shortExtractBase = Path.Combine(Path.GetTempPath(), "GetHubTmp");
                try
                {
                    Directory.CreateDirectory(shortExtractBase);
                }
                catch
                {
                }

                extractRoot = Path.Combine(shortExtractBase, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(extractRoot);

                string repositoryDirectory = _store.GetRepositoryDirectory(info.Owner, info.Name);

                bool strippedTopFolder = false;
                try
                {
                    int maxRelativeLength = GetMaxRelativeLength(repositoryDirectory);
                    strippedTopFolder = ExtractArchiveSafe(zipPath, extractRoot, maxRelativeLength);
                }
                catch (Exception ex2)
                {
                    throw new GitHubException("The downloaded archive could not be extracted: " + ex2.Message, ex2);
                }

                string contentRoot = strippedTopFolder ? extractRoot : FindContentRoot(extractRoot);

                Report(progress, DownloadProgress.Busy("Writing the offline copy..."));

                try
                {
                    _store.InstallSnapshot(repositoryDirectory, contentRoot);
                }
                catch (Exception ex)
                {
                    throw new GitHubException("Could not update the local repository cache: " + ex.Message, ex);
                }

                RepoRecord record = new RepoRecord();
                record.Owner = info.Owner;
                record.Name = info.Name;
                record.FullName = info.FullName;
                record.Description = info.Description;
                record.DefaultBranch = info.DefaultBranch;
                record.Branch = branch;
                record.HtmlUrl = info.HtmlUrl;
                record.Language = info.Language;
                record.Stars = info.Stars;
                record.Forks = info.Forks;
                record.OpenIssues = info.OpenIssues;
                record.SizeKb = info.SizeKb;
                record.LicenseName = info.LicenseName;
                record.AvatarUrl = info.AvatarUrl;
                record.Homepage = info.Homepage;
                record.Watchers = info.Watchers;
                record.Topics = info.Topics;
                record.LocalPath = repositoryDirectory;
                record.DownloadedAtUtc = DateTime.UtcNow;

                // Best effort: a rate limit or missing endpoint must not fail the download.
                try
                {
                    await FillDetailListsAsync(record, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                }

                _store.SaveRecord(record);
                Report(progress, DownloadProgress.Busy("Repository saved offline."));
                return record;
            }
            catch (OperationCanceledException)
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    throw new GitHubException("The request to GitHub timed out.");
                }

                throw;
            }
            catch (HttpRequestException ex)
            {
                throw new GitHubException("Could not connect to GitHub. Check your internet connection and try again.", ex);
            }
            finally
            {
                TryDeleteFile(zipPath);
                if (extractRoot != null)
                {
                    TryDeleteDirectory(extractRoot);
                }
            }
        }

        private async Task<RepoInfo> FetchRepoInfoAsync(
            string owner,
            string name,
            CancellationToken cancellationToken)
        {
            string url = "https://api.github.com/repos/" +
                Uri.EscapeDataString(owner) + "/" +
                Uri.EscapeDataString(name);

            using (HttpClient client = CreateClient(TimeSpan.FromMinutes(2)))
            using (HttpRequestMessage request = CreateRequest(url))
            using (HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw await CreateApiExceptionAsync(response, owner + "/" + name).ConfigureAwait(false);
                }

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> map = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (map == null)
                {
                    throw new GitHubException("GitHub returned an unexpected response.");
                }

                RepoInfo info = new RepoInfo();
                info.FullName = GetString(map, "full_name");
                info.Description = GetString(map, "description");
                info.DefaultBranch = GetString(map, "default_branch");
                info.HtmlUrl = GetString(map, "html_url");
                info.Language = GetString(map, "language");
                info.Stars = GetInt(map, "stargazers_count");
                info.Forks = GetInt(map, "forks_count");
                info.OpenIssues = GetInt(map, "open_issues_count");
                info.SizeKb = GetLong(map, "size");
                info.Homepage = GetString(map, "homepage");
                info.Watchers = GetInt(map, "subscribers_count");
                info.Topics = GetStringList(map, "topics");

                Dictionary<string, object> ownerMap = GetMap(map, "owner");
                if (ownerMap != null)
                {
                    info.Owner = GetString(ownerMap, "login");
                    info.AvatarUrl = GetString(ownerMap, "avatar_url");
                }

                Dictionary<string, object> licenseMap = GetMap(map, "license");
                if (licenseMap != null)
                {
                    info.LicenseName = GetString(licenseMap, "spdx_id");
                    if (string.IsNullOrEmpty(info.LicenseName) || info.LicenseName == "NOASSERTION")
                    {
                        info.LicenseName = GetString(licenseMap, "name");
                    }
                }

                if (string.IsNullOrEmpty(info.Owner))
                {
                    info.Owner = owner;
                }

                if (string.IsNullOrEmpty(info.Name))
                {
                    string fullName = info.FullName;
                    if (!string.IsNullOrEmpty(fullName))
                    {
                        int slash = fullName.IndexOf('/');
                        if (slash > 0)
                        {
                            info.Name = fullName.Substring(slash + 1);
                        }
                    }
                }

                if (string.IsNullOrEmpty(info.Name))
                {
                    info.Name = name;
                }

                if (string.IsNullOrEmpty(info.FullName))
                {
                    info.FullName = info.Owner + "/" + info.Name;
                }

                if (string.IsNullOrEmpty(info.HtmlUrl))
                {
                    info.HtmlUrl = "https://github.com/" + info.Owner + "/" + info.Name;
                }

                return info;
            }
        }

        // Refreshes the About / Releases / Packages / Contributors data of a repository that
        // is already stored locally (used by the sidebar's "Refresh details" action).
        public async Task<RepoRecord> FetchDetailsAsync(RepoRecord record, CancellationToken cancellationToken)
        {
            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            if (string.IsNullOrEmpty(record.Owner) || string.IsNullOrEmpty(record.Name))
            {
                throw new GitHubException("Only GitHub repositories have release and contributor details.");
            }

            RepoInfo info = await FetchRepoInfoAsync(record.Owner, record.Name, cancellationToken).ConfigureAwait(false);

            record.Owner = info.Owner;
            record.Name = info.Name;
            record.FullName = info.FullName;
            record.Description = info.Description;
            record.DefaultBranch = info.DefaultBranch;
            record.HtmlUrl = info.HtmlUrl;
            record.Language = info.Language;
            record.Stars = info.Stars;
            record.Forks = info.Forks;
            record.OpenIssues = info.OpenIssues;
            record.SizeKb = info.SizeKb;
            record.LicenseName = info.LicenseName;
            record.Homepage = info.Homepage;
            record.Watchers = info.Watchers;
            record.Topics = info.Topics;

            await FillDetailListsAsync(record, cancellationToken).ConfigureAwait(false);
            _store.SaveRecord(record);
            return record;
        }

        private async Task FillDetailListsAsync(RepoRecord record, CancellationToken cancellationToken)
        {
            string repoUrl = "https://api.github.com/repos/" +
                Uri.EscapeDataString(record.Owner) + "/" +
                Uri.EscapeDataString(record.Name);

            List<Dictionary<string, object>> releaseMaps = await GetListAsync(
                repoUrl + "/releases?per_page=5",
                cancellationToken).ConfigureAwait(false);

            List<ReleaseInfo> releases = new List<ReleaseInfo>();
            foreach (Dictionary<string, object> map in releaseMaps)
            {
                ReleaseInfo release = new ReleaseInfo();
                release.TagName = GetString(map, "tag_name");
                release.Name = GetString(map, "name");
                release.HtmlUrl = GetString(map, "html_url");
                release.IsPrerelease = GetBool(map, "prerelease");
                release.PublishedAtUtc = GetDate(map, "published_at");
                if (string.IsNullOrEmpty(release.HtmlUrl))
                {
                    release.HtmlUrl = record.HtmlUrl + "/releases";
                }

                releases.Add(release);
            }

            record.Releases = releases;

            List<Dictionary<string, object>> contributorMaps = await GetListAsync(
                repoUrl + "/contributors?per_page=12",
                cancellationToken).ConfigureAwait(false);

            List<ContributorInfo> contributors = new List<ContributorInfo>();
            foreach (Dictionary<string, object> map in contributorMaps)
            {
                string login = GetString(map, "login");
                if (string.IsNullOrEmpty(login))
                {
                    continue;
                }

                ContributorInfo contributor = new ContributorInfo();
                contributor.Login = login;
                contributor.HtmlUrl = GetString(map, "html_url");
                contributor.Contributions = GetInt(map, "contributions");
                contributors.Add(contributor);

                if (contributors.Count >= 12)
                {
                    break;
                }
            }

            record.Contributors = contributors;

            // The commit list is optional: an empty or inaccessible history must not
            // discard the releases and contributors that were already fetched.
            try
            {
                List<CommitInfo> commits = await FetchCommitsAsync(record, 100, cancellationToken).ConfigureAwait(false);
                if (commits.Count > 0)
                {
                    record.Commits = commits;
                    record.CommitsUpdatedAtUtc = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }

            record.DetailsLoaded = true;
            record.DetailsUpdatedAtUtc = DateTime.UtcNow;
        }

        // Reads the newest commits of a GitHub repository. The list is cached in repo.json
        // so the History tab keeps working without a network connection.
        public async Task<List<CommitInfo>> FetchCommitsAsync(
            RepoRecord record,
            int maxCommits,
            CancellationToken cancellationToken)
        {
            List<CommitInfo> commits = new List<CommitInfo>();
            if (record == null ||
                string.IsNullOrEmpty(record.Owner) ||
                string.IsNullOrEmpty(record.Name) ||
                string.Equals(record.Owner, "local", StringComparison.OrdinalIgnoreCase))
            {
                return commits;
            }

            string branch = record.Branch;
            if (string.IsNullOrWhiteSpace(branch) ||
                string.Equals(branch, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                branch = record.DefaultBranch;
            }

            if (string.Equals(branch, "default", StringComparison.OrdinalIgnoreCase))
            {
                branch = string.Empty;
            }

            string repoUrl = "https://api.github.com/repos/" +
                Uri.EscapeDataString(record.Owner) + "/" +
                Uri.EscapeDataString(record.Name);

            int limit = maxCommits <= 0 ? 100 : maxCommits;
            int page = 1;
            while (commits.Count < limit)
            {
                int perPage = Math.Min(100, limit - commits.Count);
                string url = repoUrl + "/commits?per_page=" + perPage + "&page=" + page;
                if (!string.IsNullOrEmpty(branch))
                {
                    url += "&sha=" + Uri.EscapeDataString(branch);
                }

                List<Dictionary<string, object>> maps = await GetListAsync(
                    url,
                    cancellationToken).ConfigureAwait(false);
                if (maps.Count == 0)
                {
                    break;
                }

                int added = 0;
                foreach (Dictionary<string, object> map in maps)
                {
                    CommitInfo commit = ParseCommit(map);
                    if (commit == null)
                    {
                        continue;
                    }

                    commits.Add(commit);
                    added++;
                    if (commits.Count >= limit)
                    {
                        break;
                    }
                }

                if (added == 0 || maps.Count < perPage)
                {
                    break;
                }

                page++;
            }

            return commits;
        }

        private static CommitInfo ParseCommit(Dictionary<string, object> map)
        {
            string sha = GetString(map, "sha");
            if (string.IsNullOrEmpty(sha))
            {
                return null;
            }

            CommitInfo commit = new CommitInfo();
            commit.Sha = sha;
            commit.HtmlUrl = GetString(map, "html_url");

            Dictionary<string, object> details = GetMap(map, "commit");
            if (details != null)
            {
                commit.Message = GetString(details, "message");
                Dictionary<string, object> author = GetMap(details, "author");
                if (author != null)
                {
                    commit.AuthorName = GetString(author, "name");
                    commit.AuthorDateUtc = GetDate(author, "date");
                }
            }

            Dictionary<string, object> user = GetMap(map, "author");
            if (user != null)
            {
                commit.AuthorLogin = GetString(user, "login");
            }

            object parentsValue;
            if (map.TryGetValue("parents", out parentsValue))
            {
                object[] parents = parentsValue as object[];
                if (parents != null)
                {
                    foreach (object item in parents)
                    {
                        Dictionary<string, object> parent = item as Dictionary<string, object>;
                        if (parent == null)
                        {
                            continue;
                        }

                        string parentSha = GetString(parent, "sha");
                        if (!string.IsNullOrEmpty(parentSha))
                        {
                            commit.Parents.Add(parentSha);
                        }
                    }
                }
            }

            return commit;
        }

        // Reads one commit with its changed files and unified patches. Used by the
        // History tab's Message/Files/Diff tabs; RepoHistoryView caches the result
        // on disk through CommitCache so it can be reopened offline.
        public async Task<CommitDetail> FetchCommitDetailAsync(
            RepoRecord record,
            string sha,
            CancellationToken cancellationToken)
        {
            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            if (string.IsNullOrEmpty(sha))
            {
                throw new ArgumentException("A commit id is required.", "sha");
            }

            if (string.IsNullOrEmpty(record.Owner) ||
                string.IsNullOrEmpty(record.Name) ||
                string.Equals(record.Owner, "local", StringComparison.OrdinalIgnoreCase))
            {
                throw new GitHubException("Only GitHub repositories can load commit details from the API.");
            }

            string url = "https://api.github.com/repos/" +
                Uri.EscapeDataString(record.Owner) + "/" +
                Uri.EscapeDataString(record.Name) + "/commits/" +
                Uri.EscapeDataString(sha);

            using (HttpClient client = CreateClient(TimeSpan.FromMinutes(2)))
            using (HttpRequestMessage request = CreateRequest(url))
            using (HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw await CreateApiExceptionAsync(
                        response,
                        record.GetDisplayName() + " commit " + sha).ConfigureAwait(false);
                }

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> map = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (map == null)
                {
                    throw new GitHubException("GitHub returned an unexpected commit response.");
                }

                CommitDetail detail = new CommitDetail();
                detail.Sha = GetString(map, "sha");
                if (string.IsNullOrEmpty(detail.Sha))
                {
                    detail.Sha = sha;
                }

                detail.HtmlUrl = GetString(map, "html_url");

                Dictionary<string, object> commitMap = GetMap(map, "commit");
                if (commitMap != null)
                {
                    detail.Message = GetString(commitMap, "message");

                    Dictionary<string, object> author = GetMap(commitMap, "author");
                    if (author != null)
                    {
                        detail.AuthorName = GetString(author, "name");
                        detail.AuthorEmail = GetString(author, "email");
                        detail.AuthorDateUtc = GetDate(author, "date");
                    }

                    Dictionary<string, object> committer = GetMap(commitMap, "committer");
                    if (committer != null)
                    {
                        detail.CommitterName = GetString(committer, "name");
                        detail.CommitterEmail = GetString(committer, "email");
                        detail.CommitDateUtc = GetDate(committer, "date");
                    }
                }

                Dictionary<string, object> stats = GetMap(map, "stats");
                if (stats != null)
                {
                    detail.Additions = GetInt(stats, "additions");
                    detail.Deletions = GetInt(stats, "deletions");
                }

                object filesValue;
                if (map.TryGetValue("files", out filesValue))
                {
                    object[] fileItems = filesValue as object[];
                    if (fileItems != null)
                    {
                        foreach (object item in fileItems)
                        {
                            Dictionary<string, object> fileMap = item as Dictionary<string, object>;
                            if (fileMap == null)
                            {
                                continue;
                            }

                            CommitFile file = new CommitFile();
                            file.Path = GetString(fileMap, "filename");
                            if (file.Path.Length == 0)
                            {
                                continue;
                            }

                            file.PreviousPath = GetString(fileMap, "previous_filename");
                            file.Status = NormalizeFileStatus(GetString(fileMap, "status"));
                            file.Additions = GetInt(fileMap, "additions");
                            file.Deletions = GetInt(fileMap, "deletions");
                            file.Changes = GetInt(fileMap, "changes");
                            if (file.Changes == 0)
                            {
                                file.Changes = file.Additions + file.Deletions;
                            }

                            file.Patch = GetString(fileMap, "patch");
                            detail.Files.Add(file);
                        }
                    }
                }

                // GitHub caps the file list at 300 entries without a flag.
                detail.Truncated = detail.Files.Count >= 300;

                object parentsValue;
                if (map.TryGetValue("parents", out parentsValue))
                {
                    object[] parentItems = parentsValue as object[];
                    if (parentItems != null)
                    {
                        foreach (object item in parentItems)
                        {
                            Dictionary<string, object> parent = item as Dictionary<string, object>;
                            if (parent == null)
                            {
                                continue;
                            }

                            string parentSha = GetString(parent, "sha");
                            if (!string.IsNullOrEmpty(parentSha))
                            {
                                detail.Parents.Add(parentSha);
                            }
                        }
                    }
                }

                if (detail.Additions == 0 && detail.Deletions == 0)
                {
                    foreach (CommitFile file in detail.Files)
                    {
                        detail.Additions += file.Additions;
                        detail.Deletions += file.Deletions;
                    }
                }

                return detail;
            }
        }

        private static string NormalizeFileStatus(string value)
        {
            switch ((value == null ? string.Empty : value).ToLowerInvariant())
            {
                case "added":
                case "removed":
                case "modified":
                case "renamed":
                case "copied":
                case "changed":
                case "unchanged":
                    return value.ToLowerInvariant();
                default:
                    return "modified";
            }
        }

        private async Task<List<Dictionary<string, object>>> GetListAsync(
            string url,
            CancellationToken cancellationToken)
        {
            List<Dictionary<string, object>> items = new List<Dictionary<string, object>>();
            try
            {
                using (HttpClient client = CreateClient(TimeSpan.FromSeconds(45)))
                using (HttpRequestMessage request = CreateRequest(url))
                using (HttpResponseMessage response = await client.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return items;
                    }

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    JavaScriptSerializer serializer = new JavaScriptSerializer();
                    object[] maps = serializer.DeserializeObject(json) as object[];
                    if (maps == null)
                    {
                        return items;
                    }

                    foreach (object item in maps)
                    {
                        Dictionary<string, object> map = item as Dictionary<string, object>;
                        if (map != null)
                        {
                            items.Add(map);
                        }
                    }
                }
            }
            catch
            {
                // Releases and contributors are optional; keep whatever we already have.
            }

            return items;
        }

        private async Task DownloadArchiveAsync(
            string url,
            string destinationPath,
            IProgress<DownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            using (HttpClient client = CreateClient())
            using (HttpRequestMessage request = CreateRequest(url))
            using (HttpResponseMessage response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    throw await CreateApiExceptionAsync(response, "repository archive").ConfigureAwait(false);
                }

                long total = -1;
                if (response.Content.Headers.ContentLength.HasValue)
                {
                    total = response.Content.Headers.ContentLength.Value;
                }

                Report(progress, DownloadProgress.Bytes("Downloading repository archive", 0, total));

                using (Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (FileStream output = new FileStream(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    131072,
                    true))
                {
                    byte[] buffer = new byte[131072];
                    long received = 0;
                    Stopwatch reportTimer = Stopwatch.StartNew();
                    int lastPercent = -1;

                    while (true)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int read = await input.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                        if (read <= 0)
                        {
                            break;
                        }

                        await output.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                        received += read;

                        int percent = total > 0 ? (int)Math.Min(100L, (received * 100L) / total) : -1;
                        if (reportTimer.ElapsedMilliseconds >= 120 || percent != lastPercent)
                        {
                            Report(progress, DownloadProgress.Bytes("Downloading repository archive", received, total));
                            lastPercent = percent;
                            reportTimer.Restart();
                        }
                    }

                    await output.FlushAsync(cancellationToken).ConfigureAwait(false);
                    Report(progress, DownloadProgress.Bytes("Downloaded repository archive", received, total > 0 ? total : received));
                }
            }
        }

        private static async Task<GitHubException> CreateApiExceptionAsync(
            HttpResponseMessage response,
            string subject)
        {
            string body = string.Empty;
            try
            {
                body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch
            {
            }

            string message = TryReadApiMessage(body);
            int status = (int)response.StatusCode;

            if (status == 401)
            {
                return new GitHubException(
                    "GitHub rejected the access token. Check the token in Settings and try again.");
            }

            if (status == 404)
            {
                return new GitHubException(
                    "GitHub could not find " + subject + ".\n\n" +
                    "Check the URL. Private repositories also need an access token in Settings.");
            }

            if (status == 403 && IsRateLimited(response))
            {
                string reset = response.Headers.Contains("X-RateLimit-Reset")
                    ? FirstHeader(response, "X-RateLimit-Reset")
                    : string.Empty;

                string when = string.Empty;
                long seconds;
                if (long.TryParse(reset, out seconds))
                {
                    try
                    {
                        DateTime local = DateTimeOffset.FromUnixTimeSeconds(seconds).LocalDateTime;
                        when = " The limit resets at " + local.ToString("t") + ".";
                    }
                    catch
                    {
                    }
                }

                return new GitHubException(
                    "GitHub's API rate limit has been reached." + when + "\n\n" +
                    "Add a personal access token in Settings to raise the limit.");
            }

            if (string.IsNullOrEmpty(message))
            {
                message = response.ReasonPhrase;
            }

            if (string.IsNullOrEmpty(message))
            {
                message = "GitHub returned an error.";
            }

            return new GitHubException("GitHub error (" + status + "): " + message);
        }

        private static bool IsRateLimited(HttpResponseMessage response)
        {
            if (!response.Headers.Contains("X-RateLimit-Remaining"))
            {
                return false;
            }

            string remaining = FirstHeader(response, "X-RateLimit-Remaining");
            return remaining == "0" || string.Equals(remaining, "0", StringComparison.Ordinal);
        }

        private static string FirstHeader(HttpResponseMessage response, string name)
        {
            IEnumerable<string> values;
            if (response.Headers.TryGetValues(name, out values))
            {
                foreach (string value in values)
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static string TryReadApiMessage(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return string.Empty;
            }

            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                Dictionary<string, object> map = serializer.DeserializeObject(json) as Dictionary<string, object>;
                if (map != null)
                {
                    return GetString(map, "message");
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private HttpClient CreateClient()
        {
            return CreateClient(TimeSpan.FromMinutes(45));
        }

        private HttpClient CreateClient(TimeSpan timeout)
        {
            HttpClientHandler handler = new HttpClientHandler();
            handler.AllowAutoRedirect = true;
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            HttpClient client = new HttpClient(handler);
            client.Timeout = timeout;
            return client;
        }

        private HttpRequestMessage CreateRequest(string url)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(UserAgent);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            if (!string.IsNullOrEmpty(_token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            }

            return request;
        }

        // Windows (and this .NET Framework runtime) rejects paths of 260+ characters, so
        // every extracted path has to fit inside the final snapshot folder.
        private const int MaxFsPathLength = 250;

        // Every folder name is capped by its depth, never by the remaining length of one
        // particular entry. That way all entries below a folder get the exact same folder
        // name, instead of each overflow creating its own truncated copy of the tree.
        private static readonly int[] DirectorySegmentBudgets =
            new int[] { 48, 34, 26, 22, 18, 16, 14, 12 };

        private const int DeepestDirectoryBudget = 12;
        private const int MinimumFileNameLength = 24;
        private const int MinimumDirectoryBudget = 8;

        private static int GetMaxRelativeLength(string repositoryDirectory)
        {
            string snapshot = Path.Combine(repositoryDirectory, "snapshot");
            int allowance = MaxFsPathLength - snapshot.Length - 1;
            if (allowance < 60)
            {
                allowance = 60;
            }

            return allowance;
        }

        // Returns true when the archive's wrapper folder (owner-repo-sha) was removed, so
        // extractRoot itself is the repository root.
        private static bool ExtractArchiveSafe(string zipPath, string extractRoot, int maxRelativeLength)
        {
            System.IO.Compression.ZipArchive archive = ZipFile.OpenRead(zipPath);
            string topFolder = null;
            try
            {
                topFolder = FindTopFolder(archive);
                foreach (System.IO.Compression.ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.FullName))
                    {
                        continue;
                    }

                    string relative = entry.FullName.Replace('\\', '/');
                    if (topFolder.Length > 0)
                    {
                        if (relative.Length <= topFolder.Length ||
                            !relative.StartsWith(topFolder, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        relative = relative.Substring(topFolder.Length);
                        if (relative.StartsWith("/", StringComparison.Ordinal))
                        {
                            relative = relative.Substring(1);
                        }

                        if (relative.Length == 0)
                        {
                            continue;
                        }
                    }

                    bool isDirectory = relative.EndsWith("/", StringComparison.Ordinal) ||
                        entry.Name.Length == 0;
                    string safeRelative = MakeSafeRelativePath(relative, maxRelativeLength, isDirectory);
                    if (string.IsNullOrEmpty(safeRelative))
                    {
                        continue;
                    }

                    string destPath = Path.Combine(extractRoot, safeRelative);
                    string destDir = Path.GetDirectoryName(destPath);
                    if (string.IsNullOrEmpty(destDir))
                    {
                        continue;
                    }

                    try
                    {
                        Directory.CreateDirectory(destDir);
                    }
                    catch
                    {
                        continue;
                    }

                    if (isDirectory)
                    {
                        continue;
                    }

                    try
                    {
                        using (Stream source = entry.Open())
                        using (FileStream target = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            source.CopyTo(target);
                        }
                    }
                    catch
                    {
                        // Skip single unreadable entries instead of failing the whole repository.
                    }
                }
            }
            finally
            {
                archive.Dispose();
            }

            return topFolder != null;
        }

        // GitHub zipballs put everything inside a single "owner-repo-sha" folder. Returns
        // null when the entries are not wrapped in a common folder.
        private static string FindTopFolder(System.IO.Compression.ZipArchive archive)
        {
            string top = null;
            foreach (System.IO.Compression.ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.FullName))
                {
                    continue;
                }

                string name = entry.FullName.Replace('\\', '/');
                int slash = name.IndexOf('/');
                if (slash <= 0)
                {
                    return null;
                }

                string first = name.Substring(0, slash);
                if (top == null)
                {
                    top = first;
                }
                else if (!string.Equals(top, first, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return top;
        }

        private static string MakeSafeRelativePath(string relativePath, int maxLength, bool isDirectory)
        {
            if (string.IsNullOrEmpty(relativePath))
            {
                return string.Empty;
            }

            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string[] parts = normalized.Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return string.Empty;
            }

            int directoryCount = isDirectory ? parts.Length : parts.Length - 1;
            string[] safeParts = new string[parts.Length];
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                System.Text.StringBuilder cleaned = new System.Text.StringBuilder(part.Length);
                for (int c = 0; c < part.Length; c++)
                {
                    char ch = part[c];
                    cleaned.Append(Array.IndexOf(invalid, ch) >= 0 ? '_' : ch);
                }

                string value = cleaned.ToString().Trim();
                if (i < directoryCount)
                {
                    // Windows silently drops trailing dots/spaces, which would make the
                    // extracted path differ from the path we wrote to.
                    value = value.TrimEnd(' ', '.');
                }

                if (value.Length == 0 || value == "." || value == "..")
                {
                    value = "_";
                }

                safeParts[i] = value;
            }

            // Depth based cap: identical for every entry that shares this folder.
            for (int i = 0; i < directoryCount; i++)
            {
                int budget = i < DirectorySegmentBudgets.Length
                    ? DirectorySegmentBudgets[i]
                    : DeepestDirectoryBudget;
                safeParts[i] = TrimSegment(safeParts[i], budget, false);
            }

            int directoryLimit = Math.Max(MinimumFileNameLength, maxLength - 1 - MinimumFileNameLength);
            string[] directoryParts = new string[directoryCount];
            Array.Copy(safeParts, directoryParts, directoryCount);
            string directoryPath = FitDirectoryPath(directoryParts, directoryLimit);

            if (isDirectory)
            {
                return directoryPath;
            }

            int room = maxLength - (directoryPath.Length == 0 ? 0 : directoryPath.Length + 1);
            if (room < 1)
            {
                room = 1;
            }

            string fileName = TrimSegment(safeParts[safeParts.Length - 1], room, true);
            if (directoryPath.Length == 0)
            {
                return fileName;
            }

            return directoryPath + Path.DirectorySeparatorChar + fileName;
        }

        private static string FitDirectoryPath(string[] directoryParts, int limit)
        {
            string joined = string.Join(Path.DirectorySeparatorChar.ToString(), directoryParts);
            if (joined.Length <= limit)
            {
                return joined;
            }

            // Pathological names: shrink the deepest folders first. This still depends only
            // on the folder list itself, so every file below the folder maps the same way.
            string[] fitted = (string[])directoryParts.Clone();
            int overflow = joined.Length - limit;
            for (int i = fitted.Length - 1; i >= 0 && overflow > 0; i--)
            {
                int shrinkable = fitted[i].Length - MinimumDirectoryBudget;
                if (shrinkable <= 0)
                {
                    continue;
                }

                int take = Math.Min(shrinkable, overflow);
                fitted[i] = TrimSegment(fitted[i], fitted[i].Length - take, false);
                overflow -= take;
            }

            joined = string.Join(Path.DirectorySeparatorChar.ToString(), fitted);
            if (joined.Length <= limit)
            {
                return joined;
            }

            // Last resort for extremely deep trees: keep the tail of the folder path.
            return joined.Substring(joined.Length - limit).TrimStart(Path.DirectorySeparatorChar);
        }

        private static string TrimSegment(string segment, int limit, bool isFileName)
        {
            if (segment.Length <= limit)
            {
                return segment;
            }

            string extension = isFileName ? Path.GetExtension(segment) : string.Empty;
            if (extension.Length > 12)
            {
                extension = string.Empty;
            }

            int keep = limit - extension.Length;
            if (keep < 1)
            {
                keep = 1;
                extension = string.Empty;
            }

            string suffix = "~" + StableSuffix(segment);
            if (keep > suffix.Length && segment.Length > suffix.Length)
            {
                keep -= suffix.Length;
            }
            else
            {
                suffix = string.Empty;
            }

            if (keep < 1)
            {
                keep = 1;
            }

            return segment.Substring(0, Math.Min(keep, segment.Length)) + suffix + extension;
        }

        private static string StableSuffix(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < value.Length; i++)
                {
                    hash = hash * 31 + value[i];
                }

                return (hash & 0xFFFF).ToString("x4");
            }
        }

        private static string FindContentRoot(string extractRoot)
        {
            string[] directories = Directory.GetDirectories(extractRoot);
            string[] files = Directory.GetFiles(extractRoot);
            if (directories.Length == 1 && files.Length == 0)
            {
                return directories[0];
            }

            return extractRoot;
        }

        private static void Report(IProgress<DownloadProgress> progress, DownloadProgress value)
        {
            if (progress != null)
            {
                progress.Report(value);
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

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                }
            }
            catch
            {
            }
        }

        private static Dictionary<string, object> GetMap(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value))
            {
                return value as Dictionary<string, object>;
            }

            return null;
        }

        private static string GetString(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value) && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            return string.Empty;
        }

        private static int GetInt(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToInt32(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return 0;
        }

        private static long GetLong(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return 0;
        }

        private static bool GetBool(Dictionary<string, object> map, string key)
        {
            object value;
            if (map != null && map.TryGetValue(key, out value) && value != null)
            {
                try
                {
                    return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
                }
                catch
                {
                }
            }

            return false;
        }

        private static DateTime GetDate(Dictionary<string, object> map, string key)
        {
            string text = GetString(map, key);
            if (string.IsNullOrEmpty(text))
            {
                return DateTime.MinValue;
            }

            DateTime parsed;
            if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            return DateTime.MinValue;
        }

        private static List<string> GetStringList(Dictionary<string, object> map, string key)
        {
            List<string> values = new List<string>();
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null)
            {
                return values;
            }

            object[] items = value as object[];
            if (items == null)
            {
                return values;
            }

            foreach (object item in items)
            {
                string text = item == null ? string.Empty : Convert.ToString(item, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(text))
                {
                    values.Add(text);
                }
            }

            return values;
        }

        private class RepoInfo
        {
            public string Owner { get; set; }
            public string Name { get; set; }
            public string FullName { get; set; }
            public string Description { get; set; }
            public string DefaultBranch { get; set; }
            public string HtmlUrl { get; set; }
            public string Language { get; set; }
            public int Stars { get; set; }
            public int Forks { get; set; }
            public int OpenIssues { get; set; }
            public long SizeKb { get; set; }
            public string LicenseName { get; set; }
            public string AvatarUrl { get; set; }
            public string Homepage { get; set; }
            public int Watchers { get; set; }
            public List<string> Topics { get; set; }

            public RepoInfo()
            {
                Owner = string.Empty;
                Name = string.Empty;
                FullName = string.Empty;
                Description = string.Empty;
                DefaultBranch = string.Empty;
                HtmlUrl = string.Empty;
                Language = string.Empty;
                LicenseName = string.Empty;
                AvatarUrl = string.Empty;
                Homepage = string.Empty;
                Topics = new List<string>();
            }
        }
    }
}
