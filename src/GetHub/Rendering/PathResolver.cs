using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace GetHub.Rendering
{
    public static class PathResolver
    {
        private static readonly Regex SchemePattern =
            new Regex(@"^[A-Za-z][A-Za-z0-9+.-]*:", RegexOptions.Compiled);

        public static bool IsInside(string root, string path)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path))
            {
                return false;
            }

            string fullRoot;
            string fullPath;
            try
            {
                fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return false;
            }

            if (string.Equals(fullRoot, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return fullPath.StartsWith(
                fullRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeRelative(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            string value = path.Replace('\\', '/');
            string[] pieces = value.Split(new char[] { '/' }, StringSplitOptions.None);
            List<string> stack = new List<string>();

            foreach (string piece in pieces)
            {
                if (piece.Length == 0 || piece == ".")
                {
                    continue;
                }

                if (piece == "..")
                {
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    continue;
                }

                stack.Add(piece);
            }

            return string.Join("/", stack.ToArray());
        }

        public static string GetFullPath(string root, string relativePath)
        {
            try
            {
                string normalized = NormalizeRelative(relativePath);
                string combined = Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar));
                string full = Path.GetFullPath(combined);
                if (!IsInside(root, full))
                {
                    return null;
                }

                return full;
            }
            catch
            {
                return null;
            }
        }

        public static string GetRelativePath(string root, string fullPath)
        {
            try
            {
                string fullRoot = Path.GetFullPath(root)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string full = Path.GetFullPath(fullPath);
                if (string.Equals(fullRoot, full, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Empty;
                }

                if (full.Length <= fullRoot.Length)
                {
                    return string.Empty;
                }

                string relative = full.Substring(fullRoot.Length).TrimStart(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                return relative.Replace('\\', '/');
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string BuildBrowseUrl(string relativePath, bool raw)
        {
            string url = "og://browse?path=" + Uri.EscapeDataString(relativePath ?? string.Empty);
            if (raw)
            {
                url += "&mode=raw";
            }

            return url;
        }

        public static string BuildOpenUrl(string relativePath)
        {
            return "og://openfile?path=" + Uri.EscapeDataString(relativePath ?? string.Empty);
        }

        public static string ToFileUri(string fullPath)
        {
            try
            {
                return new Uri(Path.GetFullPath(fullPath)).AbsoluteUri;
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string ResolveHref(string target, RepoContext context)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return "#";
            }

            string value = target.Trim();
            if (value.StartsWith("#", StringComparison.Ordinal))
            {
                return value;
            }

            if (value.StartsWith("//", StringComparison.Ordinal))
            {
                return "https:" + value;
            }

            Uri absolute;
            if (SchemePattern.IsMatch(value))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out absolute))
                {
                    return "#";
                }

                string scheme = absolute.Scheme.ToLowerInvariant();
                if (scheme == "http" || scheme == "https" || scheme == "mailto")
                {
                    string local = TryMapGithubFile(absolute, context);
                    if (!string.IsNullOrEmpty(local))
                    {
                        return BuildBrowseUrl(local, false);
                    }

                    return value;
                }

                if (scheme == "file")
                {
                    if (IsInside(context.RepoRoot, absolute.LocalPath))
                    {
                        return BuildBrowseUrl(GetRelativePath(context.RepoRoot, absolute.LocalPath), false);
                    }

                    return "#";
                }

                return "#";
            }

            string path;
            SplitTarget(value, out path);
            if (path.Length == 0)
            {
                return value.StartsWith("#", StringComparison.Ordinal) ? value : "#";
            }

            path = Uri.UnescapeDataString(path).Replace('\\', '/');

            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                string mapped = TryMapRootRelative(path, context, true);
                if (mapped != null)
                {
                    return BuildBrowseUrl(mapped, false);
                }

                return "https://github.com" + path;
            }

            string baseDirectory = string.IsNullOrEmpty(context.CurrentDirectory)
                ? context.RepoRoot
                : context.CurrentDirectory;

            string combined;
            try
            {
                combined = Path.GetFullPath(Path.Combine(
                    baseDirectory,
                    path.Replace('/', Path.DirectorySeparatorChar)));
            }
            catch
            {
                return "#";
            }

            if (!IsInside(context.RepoRoot, combined))
            {
                return "https://github.com/" + context.Owner + "/" + context.Name + "/" + value.TrimStart('/');
            }

            return BuildBrowseUrl(GetRelativePath(context.RepoRoot, combined), false);
        }

        public static string ResolveImage(string target, RepoContext context)
        {
            if (string.IsNullOrWhiteSpace(target))
            {
                return string.Empty;
            }

            string value = target.Trim();
            if (value.StartsWith("//", StringComparison.Ordinal))
            {
                value = "https:" + value;
            }

            Uri absolute;
            if (SchemePattern.IsMatch(value))
            {
                if (!Uri.TryCreate(value, UriKind.Absolute, out absolute))
                {
                    return value;
                }

                string scheme = absolute.Scheme.ToLowerInvariant();
                if (scheme == "file")
                {
                    if (IsInside(context.RepoRoot, absolute.LocalPath))
                    {
                        return ToFileUri(absolute.LocalPath);
                    }

                    return value;
                }

                if (scheme == "http" || scheme == "https")
                {
                    string local = TryMapGithubFile(absolute, context);
                    if (!string.IsNullOrEmpty(local))
                    {
                        string full = GetFullPath(context.RepoRoot, local);
                        if (full != null && File.Exists(full))
                        {
                            return ToFileUri(full);
                        }
                    }

                    return value;
                }

                return value;
            }

            string path;
            SplitTarget(value, out path);
            if (path.Length == 0)
            {
                return string.Empty;
            }

            path = Uri.UnescapeDataString(path).Replace('\\', '/');

            string baseDirectory = string.IsNullOrEmpty(context.CurrentDirectory)
                ? context.RepoRoot
                : context.CurrentDirectory;

            string relative;
            if (path.StartsWith("/", StringComparison.Ordinal))
            {
                relative = TryMapRootRelative(path, context, false);
                if (relative == null)
                {
                    relative = path.TrimStart('/');
                }
            }
            else
            {
                string combined;
                try
                {
                    combined = Path.GetFullPath(Path.Combine(
                        baseDirectory,
                        path.Replace('/', Path.DirectorySeparatorChar)));
                }
                catch
                {
                    return string.Empty;
                }

                if (!IsInside(context.RepoRoot, combined))
                {
                    return value;
                }

                relative = GetRelativePath(context.RepoRoot, combined);
            }

            string fullPath = GetFullPath(context.RepoRoot, relative);
            if (fullPath != null && File.Exists(fullPath))
            {
                return ToFileUri(fullPath);
            }

            return value;
        }

        public static string TryMapRootRelative(string path, RepoContext context, bool requireExists)
        {
            string value = path;
            string ownerPrefix = "/" + context.Owner + "/" + context.Name;
            if (value.StartsWith(ownerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                string rest = value.Substring(ownerPrefix.Length).TrimStart('/');
                string[] pieces = rest.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (pieces.Length >= 2)
                {
                    string kind = pieces[0].ToLowerInvariant();
                    if (kind == "blob" || kind == "tree" || kind == "raw")
                    {
                        if (pieces.Length == 2)
                        {
                            return string.Empty;
                        }

                        string mapped = string.Join("/", pieces, 2, pieces.Length - 2);
                        return CheckMapped(mapped, context, requireExists);
                    }
                }

                if (rest.Length == 0)
                {
                    return string.Empty;
                }

                return CheckMapped(rest, context, requireExists);
            }

            string rootRelative = value.TrimStart('/');
            string result = CheckMapped(rootRelative, context, requireExists);
            if (result != null)
            {
                return result;
            }

            return null;
        }

        private static string CheckMapped(string relative, RepoContext context, bool requireExists)
        {
            string normalized = NormalizeRelative(relative);
            if (!requireExists)
            {
                return normalized;
            }

            string full = GetFullPath(context.RepoRoot, normalized);
            if (full != null && (File.Exists(full) || Directory.Exists(full)))
            {
                return normalized;
            }

            return null;
        }

        private static string TryMapGithubFile(Uri uri, RepoContext context)
        {
            string host = uri.Host.ToLowerInvariant();
            string[] pieces = uri.AbsolutePath.Trim('/').Split(
                new char[] { '/' },
                StringSplitOptions.RemoveEmptyEntries);

            string relative = null;

            if (host == "raw.githubusercontent.com" && pieces.Length >= 4)
            {
                if (IsSameRepository(pieces[0], pieces[1], context))
                {
                    relative = string.Join("/", pieces, 3, pieces.Length - 3);
                }
            }
            else if ((host == "github.com" || host == "www.github.com") && pieces.Length >= 4)
            {
                if (IsSameRepository(pieces[0], pieces[1], context))
                {
                    string kind = pieces[2].ToLowerInvariant();
                    if (kind == "raw" || kind == "blob" || kind == "tree")
                    {
                        if (pieces.Length > 4)
                        {
                            relative = string.Join("/", pieces, 4, pieces.Length - 4);
                        }
                        else
                        {
                            relative = string.Empty;
                        }
                    }
                }
            }

            if (relative == null)
            {
                return null;
            }

            string decoded = Uri.UnescapeDataString(relative);
            return NormalizeRelative(decoded);
        }

        private static bool IsSameRepository(string owner, string name, RepoContext context)
        {
            if (string.IsNullOrEmpty(context.Owner) || string.IsNullOrEmpty(context.Name))
            {
                return false;
            }

            if (!string.Equals(owner, context.Owner, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string expected = context.Name;
            if (name.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            return string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static void SplitTarget(string value, out string path)
        {
            int index = value.IndexOfAny(new char[] { '#', '?' });
            if (index >= 0)
            {
                path = value.Substring(0, index);
            }
            else
            {
                path = value;
            }
        }
    }
}
