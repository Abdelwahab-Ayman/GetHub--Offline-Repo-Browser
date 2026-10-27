using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using GetHub.Models;

namespace GetHub.Services
{
    public class GitHistoryResult
    {
        public List<CommitInfo> Commits { get; set; }
        public string Branch { get; set; }
        public string RemoteUrl { get; set; }
        public bool Truncated { get; set; }

        public GitHistoryResult()
        {
            Commits = new List<CommitInfo>();
            Branch = string.Empty;
            RemoteUrl = string.Empty;
        }
    }

    // Reads the history of a local repository with the git command line client.
    // Downloaded repositories do not contain a .git folder, so they use the cached
    // GitHub REST commits instead (see GitHubDownloader.FetchCommitsAsync).
    public static class GitHistoryReader
    {
        private const char FieldSeparator = '\u001f';
        private const char RecordSeparator = '\u001e';
        private const int MaxPatchCharacters = 400000;
        private static readonly Regex ShaPattern =
            new Regex("^[0-9a-fA-F]{7,40}$", RegexOptions.Compiled);

        public static bool HasGitDirectory(string repositoryPath)
        {
            if (string.IsNullOrEmpty(repositoryPath))
            {
                return false;
            }

            try
            {
                return Directory.Exists(Path.Combine(repositoryPath, ".git")) ||
                    File.Exists(Path.Combine(repositoryPath, ".git"));
            }
            catch
            {
                return false;
            }
        }

        public static GitHistoryResult Load(string repositoryPath, int maxCommits)
        {
            GitHistoryResult result = new GitHistoryResult();
            if (!HasGitDirectory(repositoryPath))
            {
                throw new InvalidOperationException("The folder does not contain a .git directory.");
            }

            int exitCode;
            string error;
            string branch = RunGit(
                repositoryPath,
                "rev-parse --abbrev-ref HEAD",
                out exitCode,
                out error).Trim();
            if (exitCode == 0)
            {
                result.Branch = string.Equals(branch, "HEAD", StringComparison.Ordinal) ? "detached HEAD" : branch;
            }

            string remote = RunGit(
                repositoryPath,
                "config --get remote.origin.url",
                out exitCode,
                out error).Trim();
            if (exitCode == 0 && remote.Length > 0)
            {
                result.RemoteUrl = NormalizeRemoteUrl(remote);
            }

            int limit = maxCommits <= 0 ? 200 : maxCommits;
            string format = "%H%x1f%P%x1f%an%x1f%ae%x1f%aI%x1f%s%x1e";
            string output = RunGit(
                repositoryPath,
                "log -n " + limit.ToString(CultureInfo.InvariantCulture) +
                " --date=iso-strict --pretty=format:" + format,
                out exitCode,
                out error);

            if (exitCode != 0)
            {
                string detail = string.IsNullOrEmpty(error) ? "git log failed." : error.Trim();
                throw new InvalidOperationException(detail);
            }

            string[] records = output.Split(new char[] { RecordSeparator }, StringSplitOptions.RemoveEmptyEntries);
            string commitUrlBase = string.IsNullOrEmpty(result.RemoteUrl)
                ? string.Empty
                : result.RemoteUrl.TrimEnd('/') + "/commit/";

            foreach (string rawRecord in records)
            {
                string record = rawRecord.Trim('\r', '\n');
                if (record.Length == 0)
                {
                    continue;
                }

                string[] fields = record.Split(new char[] { FieldSeparator });
                if (fields.Length < 6)
                {
                    continue;
                }

                CommitInfo commit = new CommitInfo();
                commit.Sha = fields[0].Trim();
                if (commit.Sha.Length == 0)
                {
                    continue;
                }

                string[] parents = fields[1].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string parent in parents)
                {
                    commit.Parents.Add(parent);
                }

                commit.AuthorName = fields[2].Trim();
                commit.Message = fields[5];
                commit.AuthorDateUtc = ParseGitDate(fields[4]);
                commit.HtmlUrl = commitUrlBase.Length > 0 ? commitUrlBase + commit.Sha : string.Empty;
                result.Commits.Add(commit);
            }

            result.Truncated = result.Commits.Count >= limit;
            return result;
        }

        // Reads one commit in full: message, author/committer, parents, the changed
        // file list and a size-capped unified diff. Merge commits are diffed against
        // their first parent, which is what GitHub shows on a commit page.
        public static CommitDetail LoadCommitDetail(string repositoryPath, string sha)
        {
            if (!HasGitDirectory(repositoryPath))
            {
                throw new InvalidOperationException("The folder does not contain a .git directory.");
            }

            if (string.IsNullOrEmpty(sha) || !ShaPattern.IsMatch(sha))
            {
                throw new ArgumentException("The commit id is not valid.", "sha");
            }

            int exitCode;
            string error;
            string metadata = RunGit(
                repositoryPath,
                "show -s --date=iso-strict --format=%H%x1f%an%x1f%ae%x1f%aI%x1f%cn%x1f%ce%x1f%cI%x1f%P%x1f%B " + sha,
                out exitCode,
                out error);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(error) ? "git show failed." : error.Trim());
            }

            string[] fields = metadata.Split(new char[] { FieldSeparator }, 9);
            if (fields.Length < 9)
            {
                throw new InvalidOperationException("git returned an unexpected commit record.");
            }

            CommitDetail detail = new CommitDetail();
            detail.Sha = fields[0].Trim();
            detail.AuthorName = fields[1].Trim();
            detail.AuthorEmail = fields[2].Trim();
            detail.AuthorDateUtc = ParseGitDate(fields[3]);
            detail.CommitterName = fields[4].Trim();
            detail.CommitterEmail = fields[5].Trim();
            detail.CommitDateUtc = ParseGitDate(fields[6]);
            detail.Message = fields[8].TrimEnd('\r', '\n');

            string[] parents = fields[7].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string parent in parents)
            {
                detail.Parents.Add(parent);
            }

            if (string.IsNullOrEmpty(detail.Sha))
            {
                detail.Sha = sha;
            }

            bool merge = detail.Parents.Count > 1;
            string nameStatus = RunDiff(repositoryPath, merge, sha, "--name-status", out exitCode, out error);
            if (exitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrEmpty(error) ? "git diff failed." : error.Trim());
            }

            List<CommitFile> files = ParseNameStatus(nameStatus);
            string numstat = RunDiff(repositoryPath, merge, sha, "--numstat", out exitCode, out error);
            if (exitCode == 0)
            {
                ApplyNumstat(files, numstat);
            }

            string patch = RunDiff(
                repositoryPath,
                merge,
                sha,
                "--patch --unified=3",
                out exitCode,
                out error);
            if (exitCode == 0 && !string.IsNullOrEmpty(patch))
            {
                ApplyPatch(detail, files, patch);
            }

            for (int i = 0; i < files.Count; i++)
            {
                detail.Additions += files[i].Additions;
                detail.Deletions += files[i].Deletions;
            }

            detail.Files = files;
            return detail;
        }

        private static string RunDiff(
            string repositoryPath,
            bool merge,
            string sha,
            string options,
            out int exitCode,
            out string error)
        {
            string arguments = merge
                ? "diff --no-color --no-ext-diff -M " + options + " " + sha + "^1 " + sha
                : "show --format= --no-color --no-ext-diff -M " + options + " " + sha;
            return RunGit(repositoryPath, arguments, out exitCode, out error);
        }

        private static List<CommitFile> ParseNameStatus(string output)
        {
            List<CommitFile> files = new List<CommitFile>();
            string[] lines = (output == null ? string.Empty : output).Split(
                new char[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0)
                {
                    continue;
                }

                string[] fields = line.Split('\t');
                if (fields.Length < 2)
                {
                    continue;
                }

                CommitFile file = new CommitFile();
                file.Status = MapStatus(fields[0]);
                if ((file.Status == "renamed" || file.Status == "copied") && fields.Length >= 3)
                {
                    file.PreviousPath = fields[1];
                    file.Path = fields[2];
                }
                else
                {
                    file.Path = fields[1];
                }

                if (file.Path.Length == 0)
                {
                    continue;
                }

                files.Add(file);
            }

            return files;
        }

        private static string MapStatus(string value)
        {
            char letter = string.IsNullOrEmpty(value) ? 'M' : value[0];
            switch (char.ToUpperInvariant(letter))
            {
                case 'A':
                    return "added";
                case 'D':
                    return "removed";
                case 'R':
                    return "renamed";
                case 'C':
                    return "copied";
                default:
                    return "modified";
            }
        }

        private static void ApplyNumstat(List<CommitFile> files, string output)
        {
            List<int[]> counts = new List<int[]>();
            string[] lines = (output == null ? string.Empty : output).Split(
                new char[] { '\n' },
                StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string[] fields = rawLine.TrimEnd('\r').Split('\t');
                if (fields.Length < 3)
                {
                    continue;
                }

                bool binary = fields[0].Trim() == "-" || fields[1].Trim() == "-";
                counts.Add(new int[]
                {
                    ParseCount(fields[0]),
                    ParseCount(fields[1]),
                    binary ? 1 : 0
                });
            }

            int count = Math.Min(files.Count, counts.Count);
            for (int i = 0; i < count; i++)
            {
                files[i].Additions = counts[i][0];
                files[i].Deletions = counts[i][1];
                files[i].IsBinary = counts[i][2] == 1;
                files[i].Changes = files[i].Additions + files[i].Deletions;
            }
        }

        private static int ParseCount(string value)
        {
            int parsed;
            if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }

            return 0;
        }

        private static void ApplyPatch(CommitDetail detail, List<CommitFile> files, string patch)
        {
            List<string> sections = SplitPatchSections(patch);
            if (sections.Count != files.Count)
            {
                // Different git versions can merge or split rename sections; without a
                // reliable one-to-one match the file list is still useful, so keep it.
                detail.Truncated = true;
                return;
            }

            int remaining = MaxPatchCharacters;
            for (int i = 0; i < files.Count; i++)
            {
                bool binary;
                string hunks = ExtractHunks(sections[i], out binary);
                if (binary)
                {
                    files[i].IsBinary = true;
                }

                if (string.IsNullOrEmpty(hunks))
                {
                    continue;
                }

                if (remaining <= 0)
                {
                    files[i].Truncated = true;
                    detail.Truncated = true;
                    continue;
                }

                if (hunks.Length > remaining)
                {
                    hunks = hunks.Substring(0, remaining);
                    files[i].Truncated = true;
                    detail.Truncated = true;
                }

                remaining -= hunks.Length;
                files[i].Patch = hunks;
            }
        }

        private static List<string> SplitPatchSections(string patch)
        {
            List<string> sections = new List<string>();
            StringBuilder current = null;
            string[] lines = (patch == null ? string.Empty : patch).Split(new char[] { '\n' });
            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd('\r');
                if (line.StartsWith("diff --git ", StringComparison.Ordinal))
                {
                    if (current != null)
                    {
                        sections.Add(current.ToString());
                    }

                    current = new StringBuilder();
                }

                if (current != null)
                {
                    current.Append(line);
                    current.Append('\n');
                }
            }

            if (current != null)
            {
                sections.Add(current.ToString());
            }

            return sections;
        }

        private static string ExtractHunks(string section, out bool binary)
        {
            binary = section.IndexOf("Binary files ", StringComparison.Ordinal) >= 0 ||
                section.IndexOf("GIT binary patch", StringComparison.Ordinal) >= 0;

            int index;
            if (section.StartsWith("@@", StringComparison.Ordinal))
            {
                index = 0;
            }
            else
            {
                index = section.IndexOf("\n@@", StringComparison.Ordinal);
                if (index >= 0)
                {
                    index++;
                }
            }

            if (index < 0)
            {
                return string.Empty;
            }

            return section.Substring(index).TrimEnd('\r', '\n');
        }

        private static DateTime ParseGitDate(string value)
        {            DateTime parsed;
            if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            return DateTime.MinValue;
        }

        private static string NormalizeRemoteUrl(string remote)
        {
            string value = remote.Trim();
            if (value.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
            {
                value = "https://github.com/" + value.Substring("git@github.com:".Length);
            }
            else if (value.StartsWith("ssh://git@github.com/", StringComparison.OrdinalIgnoreCase))
            {
                value = "https://github.com/" + value.Substring("ssh://git@github.com/".Length);
            }

            if (!value.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) &&
                !value.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (value.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                value = value.Substring(0, value.Length - 4);
            }

            return value.TrimEnd('/');
        }

        private static string RunGit(string workingDirectory, string arguments, out int exitCode, out string error)
        {
            exitCode = -1;
            error = string.Empty;

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "git";
            startInfo.Arguments = arguments;
            startInfo.WorkingDirectory = workingDirectory;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.StandardOutputEncoding = new UTF8Encoding(false);
            startInfo.StandardErrorEncoding = new UTF8Encoding(false);

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = startInfo;
                    if (!process.Start())
                    {
                        error = "Could not start git.";
                        return string.Empty;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    error = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(20000))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch
                        {
                        }

                        error = "git did not finish in time.";
                        return string.Empty;
                    }

                    exitCode = process.ExitCode;
                    return output;
                }
            }
            catch (System.ComponentModel.Win32Exception)
            {
                error = "Git is not installed or could not be found on PATH. Install Git to read the history of local folders.";
                return string.Empty;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return string.Empty;
            }
        }
    }
}
