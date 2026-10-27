using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Rendering
{
    public static class SiteBuilder
    {
        private const int MaxCodeBytes = 512 * 1024;
        private const int MaxReadmeBytes = 1024 * 1024;
        private const int MaxPreviewLines = 8000;

        private const string FolderIcon =
            "<svg class='icon' viewBox='0 0 16 16' aria-hidden='true'><path fill='#54aeff' d='M1.75 1A1.75 1.75 0 0 0 0 2.75v10.5C0 14.216.784 15 1.75 15h12.5A1.75 1.75 0 0 0 16 13.25v-8.5A1.75 1.75 0 0 0 14.25 3H7.5a.25.25 0 0 1-.2-.1l-.9-1.2C6.07 1.26 5.55 1 5 1H1.75Z'/></svg>";

        private const string FileIcon =
            "<svg class='icon' viewBox='0 0 16 16' aria-hidden='true'><path fill='#6e7781' d='M4 0h5l5 5v9a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V2a2 2 0 0 1 2-2Z'/><path fill='#ffffff' d='M9 0v4a1 1 0 0 0 1 1h4L9 0Z'/></svg>";

        private const string ReadmeIcon =
            "<svg class='icon' viewBox='0 0 16 16' aria-hidden='true'><path fill='#6e7781' d='M2 2.75A1.75 1.75 0 0 1 3.75 1h8.5A1.75 1.75 0 0 1 14 2.75v10.5A1.75 1.75 0 0 1 12.25 15h-8.5A1.75 1.75 0 0 1 2 13.25V2.75Zm2.5.75a.75.75 0 0 0 0 1.5h7a.75.75 0 0 0 0-1.5h-7Zm0 3a.75.75 0 0 0 0 1.5h7a.75.75 0 0 0 0-1.5h-7Zm0 3a.75.75 0 0 0 0 1.5h4.5a.75.75 0 0 0 0-1.5h-4.5Z'/></svg>";

        public static string BuildDirectoryPage(RepoRecord record, string relativeDirectory)
        {
            string snapshot = record.GetSnapshotPath();
            string normalized = PathResolver.NormalizeRelative(relativeDirectory);
            string directory = PathResolver.GetFullPath(snapshot, normalized);

            if (directory == null || !Directory.Exists(directory))
            {
                return BuildNotFoundPage(record, normalized);
            }

            RepoContext context = CreateContext(record, directory, normalized);
            StringBuilder body = new StringBuilder();

            body.Append("<div class='crumbs'>");
            body.Append(BuildBreadcrumbs(record, normalized, false));
            body.Append("<span class='right'>");
            body.Append("<span class='pill'>");
            body.Append(E(record.Branch));
            body.Append("</span><span class='pill offline'>offline copy</span>");
            body.Append("</span></div>");

            FileSystemInfo[] entries;
            try
            {
                entries = new DirectoryInfo(directory).GetFileSystemInfos();
            }
            catch (Exception ex)
            {
                body.Append(BuildMessage(
                    "This folder could not be read",
                    E(ex.Message),
                    null));
                return Wrap(record.GetDisplayName(), body.ToString(), directory);
            }

            List<FileSystemInfo> sorted = new List<FileSystemInfo>();
            foreach (FileSystemInfo entry in entries)
            {
                if (string.Equals(entry.Name, ".git", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sorted.Add(entry);
            }

            sorted.Sort(delegate(FileSystemInfo a, FileSystemInfo b)
            {
                bool aDirectory = (a.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                bool bDirectory = (b.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                if (aDirectory != bDirectory)
                {
                    return aDirectory ? -1 : 1;
                }

                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });

            body.Append("<div class='box'>");
            body.Append("<div class='box-head'><span><strong>");
            body.Append(normalized.Length == 0 ? E(record.Name) : E(Path.GetFileName(normalized)));
            body.Append("</strong></span><span class='right'>");
            body.Append(sorted.Count);
            body.Append(sorted.Count == 1 ? " item" : " items");
            body.Append("</span></div>");
            body.Append("<table class='files'><thead><tr><th>Name</th><th class='size'>Size</th>");
            body.Append("<th class='date'>Modified</th></tr></thead><tbody>");

            if (normalized.Length > 0)
            {
                string parent = GetParentPath(normalized);
                body.Append("<tr><td>");
                body.Append(Link(PathResolver.BuildBrowseUrl(parent, false), FolderIcon + " .."));
                body.Append("</td><td class='size'>-</td><td class='date'>-</td></tr>");
            }

            int shown = 0;
            foreach (FileSystemInfo entry in sorted)
            {
                if (shown >= 2500)
                {
                    break;
                }

                bool isDirectory = (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory;
                string childRelative = normalized.Length == 0
                    ? entry.Name
                    : normalized + "/" + entry.Name;
                string url = PathResolver.BuildBrowseUrl(childRelative, false);

                body.Append("<tr><td>");
                body.Append("<a href=\"");
                body.Append(E(url));
                body.Append("\">");
                body.Append(isDirectory ? FolderIcon : FileIcon);
                body.Append(E(entry.Name));
                body.Append("</a></td><td class='size'>");
                if (isDirectory)
                {
                    body.Append("-");
                }
                else
                {
                    FileInfo file = entry as FileInfo;
                    body.Append(E(Display.HumanSize(file == null ? 0 : file.Length)));
                }

                body.Append("</td><td class='date'>");
                try
                {
                    body.Append(E(entry.LastWriteTime.ToString("MMM d, yyyy HH:mm")));
                }
                catch
                {
                    body.Append("-");
                }

                body.Append("</td></tr>");
                shown++;
            }

            if (shown == 0)
            {
                body.Append("<tr><td class='empty' colspan='3'>This folder is empty.</td></tr>");
            }

            if (sorted.Count > shown)
            {
                body.Append("<tr><td class='empty' colspan='3'>Showing the first ");
                body.Append(shown);
                body.Append(" of ");
                body.Append(sorted.Count);
                body.Append(" items.</td></tr>");
            }

            body.Append("</tbody></table></div>");

            string readme = FindReadme(directory);
            if (!string.IsNullOrEmpty(readme))
            {
                body.Append(BuildReadme(record, readme, directory, context));
            }
            else
            {
                string[] allFiles = null;
                try
                {
                    allFiles = Directory.GetFiles(directory, "*.*", SearchOption.TopDirectoryOnly);
                }
                catch
                {
                }

                if (allFiles != null)
                {
                    foreach (string f in allFiles)
                    {
                        string fname = Path.GetFileName(f).ToLowerInvariant();
                        if (fname == "readme" || fname.EndsWith(".md", StringComparison.Ordinal) || fname.EndsWith(".markdown", StringComparison.Ordinal) || fname.EndsWith(".mdx", StringComparison.Ordinal))
                        {
                            body.Append(BuildReadme(record, f, directory, context));
                            break;
                        }
                    }
                }
            }

            return Wrap(record.GetDisplayName(), body.ToString(), directory);
        }

        public static string BuildFilePage(RepoRecord record, string relativeFile, bool raw)
        {
            string snapshot = record.GetSnapshotPath();
            string normalized = PathResolver.NormalizeRelative(relativeFile);
            string filePath = PathResolver.GetFullPath(snapshot, normalized);

            if (filePath == null || !File.Exists(filePath))
            {
                return BuildNotFoundPage(record, normalized);
            }

            string directory = Path.GetDirectoryName(filePath);
            RepoContext context = CreateContext(record, directory, normalized);
            string fileName = Path.GetFileName(filePath);
            bool isMarkdown = FileUtil.IsMarkdown(filePath);
            bool isImage = FileUtil.IsImage(filePath);
            bool isSvg = FileUtil.IsSvg(filePath);
            bool isPdf = FileUtil.IsPdf(filePath);
            bool isWebp = FileUtil.IsWebp(filePath);
            long size = 0;
            try
            {
                size = new FileInfo(filePath).Length;
            }
            catch
            {
            }

            StringBuilder body = new StringBuilder();
            body.Append("<div class='crumbs'>");
            body.Append(BuildBreadcrumbs(record, normalized, true));
            body.Append("<span class='right'><span class='pill'>");
            body.Append(E(record.Branch));
            body.Append("</span><span class='pill offline'>offline copy</span></span></div>");

            StringBuilder actions = new StringBuilder();
            if ((isMarkdown || isSvg) && !isWebp)
            {
                string toggleUrl = PathResolver.BuildBrowseUrl(normalized, !raw);
                actions.Append(Link(toggleUrl, raw ? "Preview" : "Raw"));
            }

            actions.Append(Link(
                PathResolver.BuildOpenUrl(normalized),
                "Open externally"));

            body.Append("<div class='box'>");
            body.Append("<div class='box-head filehead'><span class='filename'>");
            body.Append(isMarkdown ? ReadmeIcon : FileIcon);
            body.Append(E(fileName));
            body.Append("</span><span class='filemeta'>");
            body.Append(E(Display.HumanSize(size)));
            body.Append("</span><span class='right actions'>");
            body.Append(actions.ToString());
            body.Append("</span></div>");

            if (isImage && !raw)
            {
                if (isWebp)
                {
                    body.Append(BuildMessage(
                        "Image format not previewable",
                        "Windows and the embedded browser do not support this image format.",
                        PathResolver.BuildOpenUrl(normalized),
                        "Open externally"));
                }
                else
                {
                    body.Append("<div class='image-view'><img src=\"");
                    body.Append(E(PathResolver.ToFileUri(filePath)));
                    body.Append("\" alt=\"");
                    body.Append(E(fileName));
                    body.Append("\" /></div>");
                }
            }
            else if (isPdf)
            {
                body.Append(BuildMessage(
                    "PDF preview is not available",
                    "Open the PDF in your default reader to view it.",
                    PathResolver.BuildOpenUrl(normalized),
                    "Open externally"));
            }
            else
            {
                TextFileContent content = FileUtil.ReadText(filePath, isMarkdown ? MaxReadmeBytes : MaxCodeBytes);
                if (content.Binary)
                {
                    body.Append(BuildMessage(
                        "Binary file",
                        "This file is not text, so it cannot be previewed in the browser.",
                        PathResolver.BuildOpenUrl(normalized),
                        "Open externally"));
                }
                else if (content.Text.Length == 0)
                {
                    body.Append(BuildMessage(
                        "Empty file",
                        "This file does not contain any data.",
                        null));
                }
                else if (isMarkdown && !raw)
                {
                    if (content.Truncated)
                    {
                        body.Append("<div class='notice'>This preview shows the first part of a larger file.</div>");
                    }

                    MarkdownRenderer renderer = new MarkdownRenderer();
                    body.Append("<div class='markdown-body pad'>");
                    body.Append(renderer.Render(content.Text, context));
                    body.Append("</div>");
                }
                else
                {
                    string language = isSvg ? "xml" : FileUtil.GetLanguage(filePath);
                    body.Append(BuildCodeView(
                        content.Text,
                        language,
                        content.Truncated,
                        MaxPreviewLines));
                }
            }

            body.Append("</div>");
            return Wrap(record.GetDisplayName() + " / " + normalized, body.ToString(), directory);
        }

        public static string BuildNotFoundPage(RepoRecord record, string relativePath)
        {
            RepoContext context = CreateContext(
                record,
                record.GetSnapshotPath(),
                relativePath);

            StringBuilder body = new StringBuilder();
            body.Append("<div class='crumbs'>");
            body.Append(BuildBreadcrumbs(record, relativePath, false));
            body.Append("</div>");
            body.Append(BuildMessage(
                "Not found in the offline copy",
                "The path <code>" + E(relativePath) + "</code> does not exist in this snapshot. " +
                "Try syncing the repository again.",
                null));
            return Wrap(record.GetDisplayName(), body.ToString(), context.CurrentDirectory);
        }

        public static string BuildErrorPage(string title, string message)
        {
            StringBuilder body = new StringBuilder();
            body.Append(BuildMessage(title, E(message), null));
            return Wrap("GetHub", body.ToString(), null);
        }

        private static RepoContext CreateContext(RepoRecord record, string directory, string relativePath)
        {
            RepoContext context = new RepoContext();
            context.RepoRoot = record.GetSnapshotPath();
            context.CurrentDirectory = directory;
            context.CurrentFileRelative = relativePath;
            context.Owner = record.Owner;
            context.Name = record.Name;
            context.Branch = record.Branch;
            return context;
        }

        private static string BuildReadme(RepoRecord record, string readmePath, string directory, RepoContext context)
        {
            TextFileContent content = FileUtil.ReadText(readmePath, MaxReadmeBytes);
            if (content.Binary)
            {
                return string.Empty;
            }

            string relative = PathResolver.GetRelativePath(record.GetSnapshotPath(), readmePath);
            string name = Path.GetFileName(readmePath);
            StringBuilder builder = new StringBuilder();
            builder.Append("<div class='readme'>");
            builder.Append("<div class='readme-head'>");
            builder.Append(ReadmeIcon);
            builder.Append(E(name));
            builder.Append("<span class='right'>");
            builder.Append(Link(PathResolver.BuildBrowseUrl(relative, false), "View file"));
            builder.Append("</span></div>");
            if (content.Truncated)
            {
                builder.Append("<div class='notice'>This preview shows the first part of a larger README.</div>");
            }

            builder.Append("<div class='readme-body markdown-body'>");
            if (FileUtil.IsMarkdown(readmePath))
            {
                MarkdownRenderer renderer = new MarkdownRenderer();
                builder.Append(renderer.Render(content.Text, context));
            }
            else
            {
                string lang = FileUtil.GetLanguage(readmePath);
                builder.Append("<pre class='readme-text'>");
                builder.Append(SyntaxHighlighter.Highlight(content.Text, lang));
                builder.Append("</pre>");
            }

            builder.Append("</div></div>");
            return builder.ToString();
        }

        private static string BuildCodeView(string text, string language, bool truncated, int maxLines)
        {
            bool linesTruncated;
            string code = LimitLines(text, maxLines, out linesTruncated);
            int lineCount = FileUtil.CountLines(code);
            StringBuilder builder = new StringBuilder();

            if (truncated || linesTruncated)
            {
                builder.Append("<div class='notice'>This preview is truncated. Use Open externally to view the complete file.</div>");
            }

            builder.Append("<div class='codebox'><div class='codewrap'>");
            builder.Append("<pre class='gutter'>");
            for (int line = 1; line <= lineCount; line++)
            {
                if (line > 1)
                {
                    builder.Append('\n');
                }

                builder.Append(line);
            }

            builder.Append("</pre><pre class='code'>");
            builder.Append(SyntaxHighlighter.Highlight(code, language));
            builder.Append("</pre></div></div>");
            return builder.ToString();
        }

        private static string LimitLines(string text, int maxLines, out bool truncated)
        {
            truncated = false;
            if (maxLines <= 0 || string.IsNullOrEmpty(text))
            {
                return text;
            }

            int line = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    line++;
                    if (line > maxLines)
                    {
                        truncated = true;
                        return text.Substring(0, i);
                    }
                }
            }

            return text;
        }

        private static string FindReadme(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return null;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directory);
            }
            catch
            {
                return null;
            }

            string best = null;
            int bestRank = int.MaxValue;
            foreach (string file in files)
            {
                string lower = Path.GetFileName(file).ToLowerInvariant();
                int rank = int.MaxValue;
                if (lower == "readme.md" || lower == "readme.markdown" ||
                    lower == "readme.mdown" || lower == "readme.mkd" || lower == "readme.mdx")
                {
                    rank = 0;
                }
                else if (lower == "readme.txt" || lower == "readme")
                {
                    rank = 1;
                }
                else if (lower.StartsWith("readme.", StringComparison.Ordinal) &&
                         (lower.EndsWith(".md", StringComparison.Ordinal) ||
                          lower.EndsWith(".markdown", StringComparison.Ordinal) ||
                          lower.EndsWith(".txt", StringComparison.Ordinal)))
                {
                    rank = 2;
                }

                if (rank < bestRank)
                {
                    bestRank = rank;
                    best = file;
                }
            }

            return best;
        }

        private static string BuildBreadcrumbs(RepoRecord record, string relativePath, bool isFile)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Link(PathResolver.BuildBrowseUrl(string.Empty, false), E(record.GetDisplayName())));
            string normalized = PathResolver.NormalizeRelative(relativePath);
            if (normalized.Length == 0)
            {
                return builder.ToString();
            }

            string[] pieces = normalized.Split('/');
            StringBuilder accumulated = new StringBuilder();
            for (int i = 0; i < pieces.Length; i++)
            {
                if (accumulated.Length > 0)
                {
                    accumulated.Append('/');
                }

                accumulated.Append(pieces[i]);
                builder.Append("<span class='sep'>/</span>");
                bool last = i == pieces.Length - 1;
                if (last && isFile)
                {
                    builder.Append("<span class='current'>");
                    builder.Append(E(pieces[i]));
                    builder.Append("</span>");
                }
                else
                {
                    builder.Append(Link(
                        PathResolver.BuildBrowseUrl(accumulated.ToString(), false),
                        E(pieces[i])));
                }
            }

            return builder.ToString();
        }

        private static string BuildMessage(string title, string message, string actionUrl, string actionText)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<div class='box'><div class='message'><h2>");
            builder.Append(title);
            builder.Append("</h2><p>");
            builder.Append(message);
            builder.Append("</p>");
            if (!string.IsNullOrEmpty(actionUrl) && !string.IsNullOrEmpty(actionText))
            {
                builder.Append("<p>");
                builder.Append(Link(actionUrl, actionText));
                builder.Append("</p>");
            }

            builder.Append("</div></div>");
            return builder.ToString();
        }

        private static string BuildMessage(string title, string message, string actionUrl)
        {
            return BuildMessage(title, message, actionUrl, "Open externally");
        }

        private static string GetParentPath(string relativePath)
        {
            string normalized = PathResolver.NormalizeRelative(relativePath);
            int slash = normalized.LastIndexOf('/');
            if (slash < 0)
            {
                return string.Empty;
            }

            return normalized.Substring(0, slash);
        }

        private static string Link(string url, string text)
        {
            return "<a href=\"" + E(url) + "\">" + text + "</a>";
        }

        // Used by the Issues and Projects pages, which share the repository
        // stylesheet but add their own small set of component rules.
        public static string BuildPage(string title, string body, string extraCss)
        {
            return Wrap(title, body, null, extraCss);
        }

        private static string Wrap(string title, string body, string baseDirectory)
        {
            return Wrap(title, body, baseDirectory, null);
        }

        private static string Wrap(string title, string body, string baseDirectory, string extraCss)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<!DOCTYPE html>\n<html><head>");
            builder.Append("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\" />");
            builder.Append("<meta charset=\"utf-8\" />");
            builder.Append("<title>");
            builder.Append(E(title));
            builder.Append("</title>");
            if (!string.IsNullOrEmpty(baseDirectory))
            {
                string baseUri = GetDirectoryUri(baseDirectory);
                if (!string.IsNullOrEmpty(baseUri))
                {
                    builder.Append("<base href=\"");
                    builder.Append(E(baseUri));
                    builder.Append("\" />");
                }
            }

            builder.Append("<style>");
            if (UiTheme.IsLight)
            {
                builder.Append(CssLight);
            }
            else
            {
                builder.Append(Css);
            }
            if (!string.IsNullOrEmpty(extraCss))
            {
                builder.Append(extraCss);
            }

            builder.Append("</style></head><body><div class='page'>");
            builder.Append(body);
            builder.Append("</div></body></html>");
            return builder.ToString();
        }

        private static string GetDirectoryUri(string directory)
        {
            try
            {
                string path = Path.GetFullPath(directory);
                if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    path += Path.DirectorySeparatorChar;
                }

                return new Uri(path).AbsoluteUri;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string E(string value)
        {
            return WebUtility.HtmlEncode(value ?? string.Empty);
        }

        private const string Css = @"
* { box-sizing: border-box; }
body { margin:0; padding:0; background:#0d1117; color:#e6edf3; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size:14px; line-height:1.5; }
a { color:#2f81f7; text-decoration:none; }
a:hover { text-decoration:underline; }
.page { max-width:1400px; margin:0 auto; padding:20px 36px 72px; }
.crumbs { margin:0 0 14px; font-size:16px; color:#8b949e; overflow:hidden; }
.crumbs a { font-weight:600; color:#2f81f7; }
.crumbs .sep { margin:0 7px; color:#6e7681; font-weight:400; }
.crumbs .current { font-weight:600; color:#e6edf3; }
.crumbs .right { float:right; font-size:12px; }
.pill { display:inline-block; padding:2px 10px; border:1px solid #30363d; border-radius:20px; background:#161b22; color:#8b949e; font-size:12px; margin-left:8px; line-height:18px; }
.pill.offline { color:#238636; background:rgba(35,134,54,0.15); border-color:rgba(35,134,54,0.5); font-weight:600; }
.box { border:1px solid #30363d; border-radius:6px; overflow:hidden; background:#161b22; margin-bottom:24px; }
.box-head { padding:9px 16px; background:#161b22; border-bottom:1px solid #30363d; font-size:13px; color:#e6edf3; overflow:hidden; }
.box-head .right { float:right; }
.files { width:100%; border-collapse:collapse; }
.files th { text-align:left; font-weight:600; font-size:12px; color:#8b949e; background:#161b22; padding:8px 16px; border-bottom:1px solid #30363d; }
.files td { padding:7px 16px; border-bottom:1px solid #21262d; font-size:14px; vertical-align:middle; }
.files tr:last-child td { border-bottom:none; }
.files tr:hover td { background:#1c2128; }
.files td a { color:#e6edf3; font-weight:500; }
.files td a:hover { color:#2f81f7; text-decoration:none; }
.files .size, .files .date { color:#8b949e; font-size:12px; white-space:nowrap; }
.files th.size, .files td.size { width:110px; }
.files th.date, .files td.date { width:180px; }
.files td.empty { color:#8b949e; text-align:center; padding:30px; }
.icon { width:16px; height:16px; vertical-align:-3px; margin-right:8px; }
.readme { border:1px solid #30363d; border-radius:6px; background:#161b22; margin-top:-8px; margin-bottom:24px; overflow:hidden; }
.readme-head { padding:9px 16px; background:#161b22; border-bottom:1px solid #30363d; font-weight:600; font-size:13px; color:#e6edf3; }
.readme-head .right { float:right; font-weight:400; font-size:12px; }
.readme-body { padding:28px 40px; }
.filehead .filename { font-weight:600; color:#e6edf3; font-size:14px; }
.filemeta { color:#8b949e; font-size:12px; margin-left:10px; }
.actions a { margin-left:14px; font-size:12px; color:#2f81f7; }
.markdown-body { font-size:15px; line-height:1.6; word-wrap:break-word; color:#e6edf3; }
.markdown-body > :first-child { margin-top:0 !important; }
.markdown-body > :last-child { margin-bottom:0 !important; }
.markdown-body h1, .markdown-body h2, .markdown-body h3, .markdown-body h4, .markdown-body h5, .markdown-body h6 { margin:24px 0 16px; font-weight:600; line-height:1.25; color:#e6edf3; }
.markdown-body h1 { font-size:2em; padding-bottom:.3em; border-bottom:1px solid #30363d; }
.markdown-body h2 { font-size:1.5em; padding-bottom:.3em; border-bottom:1px solid #30363d; }
.markdown-body h3 { font-size:1.25em; }
.markdown-body h4 { font-size:1em; }
.markdown-body h5 { font-size:.875em; }
.markdown-body h6 { font-size:.85em; color:#8b949e; }
.markdown-body p { margin:0 0 16px; }
.markdown-body ul, .markdown-body ol { margin:0 0 16px; padding-left:2em; }
.markdown-body li { margin:4px 0; }
.markdown-body li > ul, .markdown-body li > ol { margin:4px 0; }
.markdown-body blockquote { margin:0 0 16px; padding:0 1em; color:#8b949e; border-left:.25em solid #30363d; }
.markdown-body hr { height:.25em; padding:0; margin:24px 0; background:#30363d; border:0; }
.markdown-body a { color:#2f81f7; }
.markdown-body code { padding:.2em .4em; margin:0; font-size:85%; background:rgba(110,118,129,0.4); border-radius:6px; font-family:ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace; }
.markdown-body pre { margin:0 0 16px; padding:16px; overflow:auto; background:#161b22; border-radius:6px; border:1px solid #30363d; }
.markdown-body pre code { padding:0; background:transparent; border-radius:0; font-size:100%; }
.markdown-body pre code .kw { color:#ff7b72; }
.markdown-body pre code .str { color:#a5d6ff; }
.markdown-body pre code .cm { color:#8b949e; font-style:italic; }
.markdown-body pre code .num { color:#79c0ff; }
.markdown-body pre code .tag { color:#7ee787; }
.markdown-body pre code .attr { color:#79c0ff; }
.markdown-body pre code .meta { color:#d2a8ff; }
.markdown-body pre code .add { color:#aff5b4; background:rgba(46,160,67,0.15); display:block; }
.markdown-body pre code .del { color:#ffdcd7; background:rgba(248,81,73,0.15); display:block; }
.markdown-body table { border-collapse:collapse; margin:0 0 16px; display:block; overflow:auto; }
.markdown-body table th, .markdown-body table td { padding:6px 13px; border:1px solid #30363d; }
.markdown-body table th { font-weight:600; background:#161b22; }
.markdown-body img { max-width:100%; }
.markdown-body .task-list-item { list-style:none; margin-left:-1.4em; }
.markdown-body .task-list-item input { margin-right:.45em; }
.markdown-body kbd { display:inline-block; padding:3px 5px; font-size:11px; line-height:10px; color:#e6edf3; vertical-align:middle; background:#161b22; border:1px solid #30363d; border-radius:6px; box-shadow:inset 0 -1px 0 #30363d; }
.readme-text { margin:0; padding:0; background:transparent; white-space:pre-wrap; font-family:ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace; font-size:12px; color:#e6edf3; }
.notice { padding:10px 16px; background:rgba(187,128,9,0.15); border-bottom:1px solid rgba(187,128,9,0.4); color:#d29922; font-size:12px; }
.codebox { background:#0d1117; }
.codewrap { overflow:auto; max-height:72vh; background:#0d1117; white-space:nowrap; }
.gutter { display:inline-block; vertical-align:top; margin:0; padding:12px 12px 24px 16px; color:#6e7681; text-align:right; border-right:1px solid #30363d; background:#0d1117; font-family:ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace; font-size:12px; line-height:20px; -ms-user-select:none; user-select:none; }
.code { display:inline-block; vertical-align:top; margin:0; padding:12px 18px 24px 18px; background:#0d1117; color:#e6edf3; font-family:ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace; font-size:12px; line-height:20px; white-space:pre; }
.code .kw { color:#ff7b72; }
.code .str { color:#a5d6ff; }
.code .cm { color:#8b949e; font-style:italic; }
.code .num { color:#79c0ff; }
.code .tag { color:#7ee787; }
.code .attr { color:#79c0ff; }
.code .meta { color:#d2a8ff; }
.code .add { color:#aff5b4; background:rgba(46,160,67,0.15); display:block; }
.code .del { color:#ffdcd7; background:rgba(248,81,73,0.15); display:block; }
.image-view { padding:24px; text-align:center; background:#161b22; }
.image-view img { max-width:100%; }
.message { padding:52px 28px; text-align:center; color:#8b949e; }
.message h2 { margin:0 0 8px; color:#e6edf3; font-size:18px; }
.message p { margin:0 0 12px; }
.message code { background:#161b22; padding:2px 5px; border-radius:4px; font-family:Consolas, monospace; font-size:12px; color:#e6edf3; }
";

        private const string CssLight = @"
* { box-sizing: border-box; }
body { margin:0; padding:0; background:#ffffff; color:#1f2328; font-family:-apple-system, BlinkMacSystemFont, 'Segoe UI', Helvetica, Arial, sans-serif; font-size:14px; line-height:1.5; }
a { color:#0969da; text-decoration:none; }
a:hover { text-decoration:underline; }
.page { max-width:1400px; margin:0 auto; padding:20px 36px 72px; }
.crumbs { margin:0 0 14px; font-size:16px; color:#57606a; overflow:hidden; }
.crumbs a { font-weight:600; color:#0969da; }
.crumbs .sep { margin:0 7px; color:#8c959f; font-weight:400; }
.crumbs .current { font-weight:600; color:#1f2328; }
.crumbs .right { float:right; font-size:12px; }
.pill { display:inline-block; padding:2px 10px; border:1px solid #d0d7de; border-radius:20px; background:#f6f8fa; color:#57606a; font-size:12px; margin-left:8px; line-height:18px; }
.pill.offline { color:#1a7f37; background:#dafbe1; border-color:#aceebb; font-weight:600; }
.box { border:1px solid #d0d7de; border-radius:6px; overflow:hidden; background:#ffffff; margin-bottom:24px; }
.box-head { padding:9px 16px; background:#f6f8fa; border-bottom:1px solid #d0d7de; font-size:13px; color:#1f2328; overflow:hidden; }
.box-head .right { float:right; }
.files { width:100%; border-collapse:collapse; }
.files th { text-align:left; font-weight:600; font-size:12px; color:#57606a; background:#f6f8fa; padding:8px 16px; border-bottom:1px solid #d0d7de; }
.files td { padding:7px 16px; border-bottom:1px solid #eaeef2; font-size:14px; vertical-align:middle; }
.files tr:last-child td { border-bottom:none; }
.files tr:hover td { background:#f6f8fa; }
.files td a { color:#1f2328; font-weight:500; }
.files td a:hover { color:#0969da; text-decoration:none; }
.files .size, .files .date { color:#57606a; font-size:12px; white-space:nowrap; }
.files th.size, .files td.size { width:110px; }
.files th.date, .files td.date { width:180px; }
.files td.empty { color:#57606a; text-align:center; padding:30px; }
.icon { width:16px; height:16px; vertical-align:-3px; margin-right:8px; }
.readme { border:1px solid #d0d7de; border-radius:6px; background:#ffffff; margin-top:-8px; margin-bottom:24px; overflow:hidden; }
.readme-head { padding:9px 16px; background:#f6f8fa; border-bottom:1px solid #d0d7de; font-weight:600; font-size:13px; color:#1f2328; }
.readme-head .right { float:right; font-weight:400; font-size:12px; }
.readme-body { padding:28px 40px; }
.filehead .filename { font-weight:600; color:#1f2328; font-size:14px; }
.filemeta { color:#57606a; font-size:12px; margin-left:10px; }
.actions a { margin-left:14px; font-size:12px; }
.markdown-body { font-size:15px; line-height:1.6; word-wrap:break-word; color:#1f2328; }
.markdown-body > :first-child { margin-top:0 !important; }
.markdown-body > :last-child { margin-bottom:0 !important; }
.markdown-body h1, .markdown-body h2, .markdown-body h3, .markdown-body h4, .markdown-body h5, .markdown-body h6 { margin:24px 0 16px; font-weight:600; line-height:1.25; color:#1f2328; }
.markdown-body h1 { font-size:2em; padding-bottom:.3em; border-bottom:1px solid #d8dee4; }
.markdown-body h2 { font-size:1.5em; padding-bottom:.3em; border-bottom:1px solid #d8dee4; }
.markdown-body h3 { font-size:1.25em; }
.markdown-body h4 { font-size:1em; }
.markdown-body h5 { font-size:.875em; }
.markdown-body h6 { font-size:.85em; color:#57606a; }
.markdown-body p { margin:0 0 16px; }
.markdown-body ul, .markdown-body ol { margin:0 0 16px; padding-left:2em; }
.markdown-body li { margin:4px 0; }
.markdown-body li > ul, .markdown-body li > ol { margin:4px 0; }
.markdown-body blockquote { margin:0 0 16px; padding:0 1em; color:#57606a; border-left:.25em solid #d0d7de; }
.markdown-body hr { height:.25em; padding:0; margin:24px 0; background:#d0d7de; border:0; }
.markdown-body a { color:#0969da; }
.markdown-body code { padding:.2em .4em; margin:0; font-size:85%; background:rgba(175,184,193,.2); border-radius:6px; font-family:ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace; }
.markdown-body pre { margin:0 0 16px; padding:16px; overflow:auto; background:#f6f8fa; border-radius:6px; border:1px solid #d0d7de; }
.markdown-body pre code { padding:0; background:transparent; border-radius:0; font-size:100%; }
.markdown-body pre code .kw { color:#cf222e; }
.markdown-body pre code .str { color:#0a3069; }
.markdown-body pre code .cm { color:#6e7781; font-style:italic; }
.markdown-body pre code .num { color:#0550ae; }
.markdown-body pre code .tag { color:#116329; }
.markdown-body pre code .attr { color:#0550ae; }
.markdown-body pre code .meta { color:#8250df; }
.markdown-body pre code .add { color:#1a7f37; background:#dafbe1; display:block; }
.markdown-body pre code .del { color:#cf222e; background:#ffebe9; display:block; }
.markdown-body table { border-collapse:collapse; margin:0 0 16px; display:block; overflow:auto; }
.markdown-body table th, .markdown-body table td { padding:6px 13px; border:1px solid #d0d7de; }
.markdown-body table th { font-weight:600; background:#f6f8fa; }
.markdown-body img { max-width:100%; }
.markdown-body .task-list-item { list-style:none; margin-left:-1.4em; }
.markdown-body .task-list-item input { margin-right:.45em; }
.markdown-body kbd { display:inline-block; padding:3px 5px; font-size:11px; line-height:10px; color:#1f2328; vertical-align:middle; background:#f6f8fa; border:1px solid #d0d7de; border-radius:6px; box-shadow:inset 0 -1px 0 #d0d7de; }
.readme-text { margin:0; padding:0; background:transparent; white-space:pre-wrap; font-family:ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace; font-size:12px; color:#1f2328; }
.notice { padding:10px 16px; background:#fff8c5; border-bottom:1px solid #d4a72c; color:#7d4e00; font-size:12px; }
.codebox { background:#ffffff; }
.codewrap { overflow:auto; max-height:72vh; background:#ffffff; white-space:nowrap; }
.gutter { display:inline-block; vertical-align:top; margin:0; padding:12px 12px 24px 16px; color:#6e7781; text-align:right; border-right:1px solid #eaeef2; background:#ffffff; font-family:ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace; font-size:12px; line-height:20px; -ms-user-select:none; user-select:none; }
.code { display:inline-block; vertical-align:top; margin:0; padding:12px 18px 24px 18px; background:#ffffff; color:#1f2328; font-family:ui-monospace, SFMono-Regular, Consolas, 'Liberation Mono', Menlo, monospace; font-size:12px; line-height:20px; white-space:pre; }
.code .kw { color:#cf222e; }
.code .str { color:#0a3069; }
.code .cm { color:#6e7781; font-style:italic; }
.code .num { color:#0550ae; }
.code .tag { color:#116329; }
.code .attr { color:#0550ae; }
.code .meta { color:#8250df; }
.code .add { color:#1a7f37; background:#dafbe1; display:block; }
.code .del { color:#cf222e; background:#ffebe9; display:block; }
.image-view { padding:24px; text-align:center; background:#f6f8fa; }
.image-view img { max-width:100%; }
.message { padding:52px 28px; text-align:center; color:#57606a; }
.message h2 { margin:0 0 8px; color:#1f2328; font-size:18px; }
.message p { margin:0 0 12px; }
.message code { background:#f6f8fa; padding:2px 5px; border-radius:4px; font-family:Consolas, monospace; font-size:12px; color:#1f2328; }
";
    }
}
