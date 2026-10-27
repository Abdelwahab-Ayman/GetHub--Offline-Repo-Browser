using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace GetHub.Rendering
{
    public class MarkdownRenderer
    {
        private class LinkReference
        {
            public string Url;
            public string Title;
        }

        private class MarkdownState
        {
            public RepoContext Context;
            public Dictionary<string, LinkReference> References;
            public Dictionary<string, int> Slugs;
        }

        private class ListItem
        {
            public int Indent;
            public bool Ordered;
            public string Content;
        }

        private static readonly Regex ReferencePattern = new Regex(
            @"^ {0,3}\[([^\]]+)\]:\s*<?([^\s>]+)>?(?:\s+(?:""([^""]*)""|'([^']*)'|\(([^)]*)\)))?\s*$",
            RegexOptions.Compiled);

        private static readonly Regex ListItemPattern = new Regex(
            @"^(\s*)([*+-]|\d{1,9}[.)])\s+(.*)$",
            RegexOptions.Compiled);

        private static readonly Regex HeadingPattern = new Regex(
            @"^( {0,3})(#{1,6})(?:[ \t]+(.*))?$",
            RegexOptions.Compiled);

        private static readonly Regex SetextPattern = new Regex(
            @"^ {0,3}(=+|-+)[ \t]*$",
            RegexOptions.Compiled);

        private static readonly Regex HorizontalRulePattern = new Regex(
            @"^ {0,3}((\*[ \t]*){3,}|(-[ \t]*){3,}|(_[ \t]*){3,})$",
            RegexOptions.Compiled);

        private static readonly Regex BlockquotePattern = new Regex(
            @"^ {0,3}>[ \t]?",
            RegexOptions.Compiled);

        private static readonly Regex TaskPattern = new Regex(
            @"^\[([ xX])\]\s+",
            RegexOptions.Compiled);

        private static readonly Regex EntityPattern = new Regex(
            @"\G&(?:#\d{1,7}|#x[0-9A-Fa-f]{1,6}|[A-Za-z][A-Za-z0-9]{1,31});",
            RegexOptions.Compiled);

        private static readonly Regex HtmlTagNamePattern = new Regex(
            @"^<\s*/?\s*([A-Za-z][A-Za-z0-9]*)",
            RegexOptions.Compiled);

        private static readonly HashSet<string> HtmlBlockTags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "address", "article", "aside", "base", "basefont", "blockquote", "body",
                "caption", "center", "col", "colgroup", "dd", "details", "dialog", "dir",
                "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form",
                "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head", "header",
                "hr", "html", "iframe", "legend", "li", "link", "main", "menu", "menuitem",
                "nav", "noframes", "ol", "optgroup", "option", "p", "param", "section",
                "source", "summary", "table", "tbody", "td", "tfoot", "th", "thead",
                "title", "tr", "track", "ul"
            };

        public string Render(string markdown, RepoContext context)
        {
            if (markdown == null)
            {
                return string.Empty;
            }

            string normalized = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\t", "    ");
            List<string> lines = new List<string>(normalized.Split('\n'));

            MarkdownState state = new MarkdownState();
            state.Context = context ?? new RepoContext();
            state.References = ExtractReferences(lines);
            state.Slugs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            StringBuilder builder = new StringBuilder(normalized.Length + 128);
            RenderBlocks(lines, 0, lines.Count, builder, state);
            return builder.ToString();
        }

        private static Dictionary<string, LinkReference> ExtractReferences(List<string> lines)
        {
            Dictionary<string, LinkReference> references =
                new Dictionary<string, LinkReference>(StringComparer.OrdinalIgnoreCase);

            bool inFence = false;
            char fenceChar = '\0';
            int fenceLength = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];

                if (inFence)
                {
                    if (IsFenceClose(line, fenceChar, fenceLength))
                    {
                        inFence = false;
                    }

                    continue;
                }

                char currentFenceChar;
                int currentFenceLength;
                string info;
                if (TryGetFence(line, out currentFenceChar, out currentFenceLength, out info))
                {
                    inFence = true;
                    fenceChar = currentFenceChar;
                    fenceLength = currentFenceLength;
                    continue;
                }

                Match match = ReferencePattern.Match(line);
                if (match.Success)
                {
                    LinkReference reference = new LinkReference();
                    reference.Url = match.Groups[2].Value;
                    if (match.Groups[3].Success)
                    {
                        reference.Title = match.Groups[3].Value;
                    }
                    else if (match.Groups[4].Success)
                    {
                        reference.Title = match.Groups[4].Value;
                    }
                    else if (match.Groups[5].Success)
                    {
                        reference.Title = match.Groups[5].Value;
                    }
                    else
                    {
                        reference.Title = string.Empty;
                    }

                    references[match.Groups[1].Value.Trim()] = reference;
                    lines[i] = string.Empty;
                }
            }

            return references;
        }

        private void RenderBlocks(List<string> lines, int start, int end, StringBuilder builder, MarkdownState state)
        {
            int index = start;
            while (index < end)
            {
                string line = lines[index];
                if (IsBlank(line))
                {
                    index++;
                    continue;
                }

                char fenceChar;
                int fenceLength;
                string info;
                if (TryGetFence(line, out fenceChar, out fenceLength, out info))
                {
                    int close = FindFenceClose(lines, index + 1, end, fenceChar, fenceLength);
                    StringBuilder code = new StringBuilder();
                    for (int i = index + 1; i < close; i++)
                    {
                        if (code.Length > 0)
                        {
                            code.Append('\n');
                        }

                        code.Append(lines[i]);
                    }

                    string language = FirstToken(info);
                    builder.Append("<pre class=\"snippet\"><code>");
                    builder.Append(SyntaxHighlighter.Highlight(code.ToString(), language));
                    builder.Append("</code></pre>\n");
                    index = close < end ? close + 1 : end;
                    continue;
                }

                if (IsIndentedCode(line))
                {
                    StringBuilder code = new StringBuilder();
                    int first = index;
                    while (index < end)
                    {
                        string current = lines[index];
                        if (IsBlank(current))
                        {
                            code.Append('\n');
                            index++;
                            continue;
                        }

                        if (!IsIndentedCode(current))
                        {
                            break;
                        }

                        if (index > first && code.Length > 0 && code[code.Length - 1] != '\n')
                        {
                            code.Append('\n');
                        }

                        code.Append(StripIndent(current, 4));
                        index++;
                    }

                    builder.Append("<pre class=\"snippet\"><code>");
                    builder.Append(SyntaxHighlighter.Highlight(code.ToString().TrimEnd('\n'), null));
                    builder.Append("</code></pre>\n");
                    continue;
                }

                Match heading = HeadingPattern.Match(line);
                if (heading.Success)
                {
                    int level = heading.Groups[2].Value.Length;
                    string text = heading.Groups[3].Success ? heading.Groups[3].Value : string.Empty;
                    text = StripTrailingHashes(text);
                    AppendHeading(builder, level, text, state);
                    index++;
                    continue;
                }

                if (index + 1 < end && !IsBlank(line) &&
                    SetextPattern.IsMatch(lines[index + 1]) &&
                    !StartsBlock(line))
                {
                    int level = lines[index + 1].TrimStart().StartsWith("=", StringComparison.Ordinal) ? 1 : 2;
                    AppendHeading(builder, level, line.Trim(), state);
                    index += 2;
                    continue;
                }

                if (HorizontalRulePattern.IsMatch(line))
                {
                    builder.Append("<hr />\n");
                    index++;
                    continue;
                }

                if (BlockquotePattern.IsMatch(line))
                {
                    List<string> inner = new List<string>();
                    while (index < end)
                    {
                        string current = lines[index];
                        if (BlockquotePattern.IsMatch(current))
                        {
                            inner.Add(BlockquotePattern.Replace(current, string.Empty, 1));
                            index++;
                            continue;
                        }

                        if (IsBlank(current))
                        {
                            int next = index + 1;
                            while (next < end && IsBlank(lines[next]))
                            {
                                next++;
                            }

                            if (next < end && BlockquotePattern.IsMatch(lines[next]))
                            {
                                inner.Add(string.Empty);
                                index++;
                                continue;
                            }
                        }

                        break;
                    }

                    builder.Append("<blockquote>\n");
                    RenderBlocks(inner, 0, inner.Count, builder, state);
                    builder.Append("</blockquote>\n");
                    continue;
                }

                if (ListItemPattern.IsMatch(line))
                {
                    if (TryRenderList(lines, ref index, end, builder, state))
                    {
                        continue;
                    }
                }

                if (IsTableStart(lines, index, end))
                {
                    RenderTable(lines, ref index, end, builder, state);
                    continue;
                }

                if (IsHtmlBlockStart(line))
                {
                    StringBuilder html = new StringBuilder();
                    while (index < end && !IsBlank(lines[index]))
                    {
                        if (html.Length > 0)
                        {
                            html.Append('\n');
                        }

                        html.Append(lines[index]);
                        index++;
                    }

                    builder.Append(HtmlSanitizer.Sanitize(html.ToString(), state.Context));
                    builder.Append('\n');
                    continue;
                }

                StringBuilder paragraph = new StringBuilder();
                int paragraphStart = index;
                while (index < end)
                {
                    if (IsBlank(lines[index]))
                    {
                        break;
                    }

                    if (index > paragraphStart && IsBlockStarter(lines, index, end))
                    {
                        break;
                    }

                    if (index > paragraphStart && index + 1 < end &&
                        SetextPattern.IsMatch(lines[index + 1]) &&
                        !StartsBlock(lines[index]))
                    {
                        break;
                    }

                    if (paragraph.Length > 0)
                    {
                        paragraph.Append('\n');
                    }

                    paragraph.Append(lines[index]);
                    index++;
                }

                if (paragraph.Length == 0)
                {
                    index = Math.Max(index + 1, paragraphStart + 1);
                    continue;
                }

                builder.Append("<p>");
                builder.Append(RenderInline(paragraph.ToString(), state, false));
                builder.Append("</p>\n");
            }
        }

        private bool TryRenderList(
            List<string> lines,
            ref int index,
            int end,
            StringBuilder builder,
            MarkdownState state)
        {
            int baseIndent = GetIndent(lines[index]);
            List<ListItem> items = new List<ListItem>();

            while (index < end)
            {
                string line = lines[index];
                Match match = ListItemPattern.Match(line);
                if (match.Success)
                {
                    int indent = match.Groups[1].Value.Length;
                    if (indent < baseIndent)
                    {
                        break;
                    }

                    ListItem item = new ListItem();
                    item.Indent = indent;
                    item.Ordered = char.IsDigit(match.Groups[2].Value[0]);
                    item.Content = match.Groups[3].Value;
                    index++;

                    while (index < end)
                    {
                        string continuation = lines[index];
                        if (IsBlank(continuation))
                        {
                            int next = index;
                            while (next < end && IsBlank(lines[next]))
                            {
                                next++;
                            }

                            if (next < end)
                            {
                                Match nextItem = ListItemPattern.Match(lines[next]);
                                if ((nextItem.Success && nextItem.Groups[1].Value.Length >= indent) ||
                                    GetIndent(lines[next]) > indent)
                                {
                                    item.Content += "\n";
                                    index++;
                                    continue;
                                }
                            }

                            break;
                        }

                        Match nestedMatch = ListItemPattern.Match(continuation);
                        if (nestedMatch.Success)
                        {
                            break;
                        }

                        if (GetIndent(continuation) > indent || !StartsBlock(continuation))
                        {
                            item.Content += "\n" + continuation.Trim();
                            index++;
                            continue;
                        }

                        break;
                    }

                    items.Add(item);
                    continue;
                }

                if (IsBlank(line))
                {
                    int next = index;
                    while (next < end && IsBlank(lines[next]))
                    {
                        next++;
                    }

                    if (next < end)
                    {
                        Match nextItem = ListItemPattern.Match(lines[next]);
                        if (nextItem.Success && nextItem.Groups[1].Value.Length >= baseIndent)
                        {
                            index++;
                            continue;
                        }
                    }
                }

                break;
            }

            if (items.Count == 0)
            {
                return false;
            }

            int itemIndex = 0;
            RenderListItems(items, ref itemIndex, baseIndent, builder, state);
            return true;
        }

        private int RenderListItems(
            List<ListItem> items,
            ref int index,
            int indent,
            StringBuilder builder,
            MarkdownState state)
        {
            if (index >= items.Count)
            {
                return index;
            }

            bool ordered = items[index].Ordered;
            builder.Append(ordered ? "<ol>\n" : "<ul>\n");

            while (index < items.Count)
            {
                ListItem item = items[index];
                if (item.Indent < indent)
                {
                    break;
                }

                if (item.Indent > indent)
                {
                    break;
                }

                bool task = false;
                bool taskChecked = false;
                string content = item.Content;
                Match taskMatch = TaskPattern.Match(content);
                if (taskMatch.Success)
                {
                    task = true;
                    taskChecked = taskMatch.Groups[1].Value == "x" || taskMatch.Groups[1].Value == "X";
                    content = content.Substring(taskMatch.Length);
                }

                builder.Append("<li");
                if (task)
                {
                    builder.Append(" class=\"task-list-item\"");
                }

                builder.Append('>');

                if (task)
                {
                    builder.Append("<input type=\"checkbox\" disabled=\"disabled\"");
                    if (taskChecked)
                    {
                        builder.Append(" checked=\"checked\"");
                    }

                    builder.Append(" /> ");
                }

                builder.Append(RenderInline(content, state, false));
                index++;

                while (index < items.Count && items[index].Indent > indent)
                {
                    int nestedIndent = items[index].Indent;
                    index = RenderListItems(items, ref index, nestedIndent, builder, state);
                }

                builder.Append("</li>\n");
            }

            builder.Append(ordered ? "</ol>\n" : "</ul>\n");
            return index;
        }

        private bool IsTableStart(List<string> lines, int index, int end)
        {
            if (index + 1 >= end)
            {
                return false;
            }

            string header = lines[index];
            if (header.IndexOf('|') < 0)
            {
                return false;
            }

            string separator = lines[index + 1];
            if (separator.IndexOf('-') < 0)
            {
                return false;
            }

            List<string> cells = SplitTableRow(separator);
            if (cells.Count == 0)
            {
                return false;
            }

            foreach (string cell in cells)
            {
                if (!Regex.IsMatch(cell.Trim(), @"^:?-{1,}:?$"))
                {
                    return false;
                }
            }

            return true;
        }

        private void RenderTable(
            List<string> lines,
            ref int index,
            int end,
            StringBuilder builder,
            MarkdownState state)
        {
            List<string> header = SplitTableRow(lines[index]);
            List<string> separator = SplitTableRow(lines[index + 1]);
            string[] alignments = new string[header.Count];

            for (int column = 0; column < header.Count; column++)
            {
                string cell = column < separator.Count ? separator[column].Trim() : string.Empty;
                bool left = cell.StartsWith(":", StringComparison.Ordinal);
                bool right = cell.EndsWith(":", StringComparison.Ordinal);
                if (left && right)
                {
                    alignments[column] = "center";
                }
                else if (right)
                {
                    alignments[column] = "right";
                }
                else if (left)
                {
                    alignments[column] = "left";
                }
            }

            index += 2;
            builder.Append("<table>\n<thead>\n<tr>\n");
            for (int column = 0; column < header.Count; column++)
            {
                AppendTableCell(builder, "th", header[column], alignments[column], state);
            }

            builder.Append("</tr>\n</thead>\n<tbody>\n");

            while (index < end && !IsBlank(lines[index]) && lines[index].IndexOf('|') >= 0)
            {
                List<string> row = SplitTableRow(lines[index]);
                builder.Append("<tr>\n");
                for (int column = 0; column < header.Count; column++)
                {
                    string cell = column < row.Count ? row[column] : string.Empty;
                    AppendTableCell(builder, "td", cell, alignments[column], state);
                }

                builder.Append("</tr>\n");
                index++;
            }

            builder.Append("</tbody>\n</table>\n");
        }

        private void AppendTableCell(
            StringBuilder builder,
            string tag,
            string content,
            string alignment,
            MarkdownState state)
        {
            builder.Append('<');
            builder.Append(tag);
            if (!string.IsNullOrEmpty(alignment))
            {
                builder.Append(" align=\"");
                builder.Append(alignment);
                builder.Append('"');
            }

            builder.Append('>');
            builder.Append(RenderInline(content.Trim(), state, false));
            builder.Append("</");
            builder.Append(tag);
            builder.Append(">\n");
        }

        private static List<string> SplitTableRow(string line)
        {
            string value = line.Trim();
            if (value.StartsWith("|", StringComparison.Ordinal))
            {
                value = value.Substring(1);
            }

            if (value.EndsWith("|", StringComparison.Ordinal))
            {
                value = value.Substring(0, value.Length - 1);
            }

            List<string> cells = new List<string>();
            StringBuilder current = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' && i + 1 < value.Length && value[i + 1] == '|')
                {
                    current.Append('|');
                    i++;
                    continue;
                }

                if (c == '|')
                {
                    cells.Add(current.ToString());
                    current.Length = 0;
                    continue;
                }

                current.Append(c);
            }

            cells.Add(current.ToString());
            return cells;
        }

        private void AppendHeading(StringBuilder builder, int level, string text, MarkdownState state)
        {
            string slug = CreateSlug(text, state);
            builder.Append("<h");
            builder.Append(level);
            builder.Append(" id=\"");
            builder.Append(WebUtility.HtmlEncode(slug));
            builder.Append("\">");
            builder.Append(RenderInline(text, state, false));
            builder.Append("</h");
            builder.Append(level);
            builder.Append(">\n");
        }

        private static string CreateSlug(string text, MarkdownState state)
        {
            string plain = PlainText(text).Trim().ToLowerInvariant();
            StringBuilder slug = new StringBuilder();
            bool lastDash = false;
            foreach (char c in plain)
            {
                if (char.IsLetterOrDigit(c))
                {
                    slug.Append(c);
                    lastDash = false;
                }
                else if (c == ' ' || c == '-' || c == '_')
                {
                    if (!lastDash && slug.Length > 0)
                    {
                        slug.Append('-');
                        lastDash = true;
                    }
                }
            }

            while (slug.Length > 0 && slug[slug.Length - 1] == '-')
            {
                slug.Length--;
            }

            if (slug.Length == 0)
            {
                slug.Append("section");
            }

            string baseSlug = slug.ToString();
            int count;
            if (state.Slugs.TryGetValue(baseSlug, out count))
            {
                count++;
                state.Slugs[baseSlug] = count;
                return baseSlug + "-" + count;
            }

            state.Slugs[baseSlug] = 0;
            return baseSlug;
        }

        private string RenderInline(string text, MarkdownState state, bool insideLink)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder(text.Length + 32);
            int index = 0;

            while (index < text.Length)
            {
                char current = text[index];

                if (current == '\\')
                {
                    if (index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        builder.Append("<br />\n");
                        index += 2;
                        continue;
                    }

                    if (index + 1 < text.Length && IsEscapable(text[index + 1]))
                    {
                        AppendEscapedChar(builder, text[index + 1]);
                        index += 2;
                        continue;
                    }

                    AppendEscapedChar(builder, current);
                    index++;
                    continue;
                }

                if (current == '`')
                {
                    int run = CountRun(text, index, '`');
                    int end = FindCodeSpanEnd(text, index + run, run);
                    if (end >= 0)
                    {
                        string code = text.Substring(index + run, end - (index + run)).Replace('\n', ' ');
                        if (code.Length > 2 && code[0] == ' ' && code[code.Length - 1] == ' ')
                        {
                            code = code.Substring(1, code.Length - 2);
                        }

                        builder.Append("<code>");
                        AppendEscaped(builder, code);
                        builder.Append("</code>");
                        index = end + run;
                        continue;
                    }
                }

                if (current == '!' && index + 1 < text.Length && text[index + 1] == '[')
                {
                    int consumed;
                    string image = TryBuildLinkOrImage(text, index, state, true, insideLink, out consumed);
                    if (image != null)
                    {
                        builder.Append(image);
                        index += consumed;
                        continue;
                    }
                }

                if (current == '[' && !insideLink)
                {
                    int consumed;
                    string link = TryBuildLinkOrImage(text, index, state, false, false, out consumed);
                    if (link != null)
                    {
                        builder.Append(link);
                        index += consumed;
                        continue;
                    }
                }

                if (current == '<')
                {
                    int end = text.IndexOf('>', index + 1);
                    if (end > index)
                    {
                        string inner = text.Substring(index + 1, end - index - 1);
                        if (IsUrlAutolink(inner))
                        {
                            string href = inner.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                                ? "https://" + inner
                                : inner;
                            builder.Append("<a href=\"");
                            builder.Append(WebUtility.HtmlEncode(href));
                            builder.Append("\">");
                            builder.Append(WebUtility.HtmlEncode(inner));
                            builder.Append("</a>");
                            index = end + 1;
                            continue;
                        }

                        if (IsEmailAutolink(inner))
                        {
                            string address = inner.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                                ? inner.Substring(7)
                                : inner;
                            builder.Append("<a href=\"mailto:");
                            builder.Append(WebUtility.HtmlEncode(address));
                            builder.Append("\">");
                            builder.Append(WebUtility.HtmlEncode(address));
                            builder.Append("</a>");
                            index = end + 1;
                            continue;
                        }

                        string tag = text.Substring(index, end - index + 1);
                        string sanitized = HtmlSanitizer.SanitizeTag(tag, state.Context);
                        if (sanitized.Length > 0)
                        {
                            builder.Append(sanitized);
                            index = end + 1;
                            continue;
                        }
                    }

                    AppendEscapedChar(builder, current);
                    index++;
                    continue;
                }

                if (!insideLink && (current == 'h' || current == 'H') &&
                    (StartsWithAt(text, index, "http://") || StartsWithAt(text, index, "https://")) &&
                    (index == 0 || char.IsWhiteSpace(text[index - 1]) || text[index - 1] == '('))
                {
                    int end = index;
                    while (end < text.Length &&
                           !char.IsWhiteSpace(text[end]) &&
                           text[end] != '<' && text[end] != '>' &&
                           text[end] != '"' && text[end] != '\'')
                    {
                        end++;
                    }

                    int trimmed = end;
                    while (trimmed > index && ".,;:!?)]}".IndexOf(text[trimmed - 1]) >= 0)
                    {
                        trimmed--;
                    }

                    string url = text.Substring(index, trimmed - index);
                    builder.Append("<a href=\"");
                    builder.Append(WebUtility.HtmlEncode(url));
                    builder.Append("\">");
                    builder.Append(WebUtility.HtmlEncode(url));
                    builder.Append("</a>");
                    index = trimmed;
                    continue;
                }

                if (current == '~' && index + 1 < text.Length && text[index + 1] == '~')
                {
                    int close = FindDelimiter(text, index, "~~");
                    if (close > index + 2)
                    {
                        string inner = text.Substring(index + 2, close - (index + 2));
                        builder.Append("<del>");
                        builder.Append(RenderInline(inner, state, insideLink));
                        builder.Append("</del>");
                        index = close + 2;
                        continue;
                    }
                }

                if (current == '*' || current == '_')
                {
                    int handled = TryRenderEmphasis(text, index, state, insideLink, builder);
                    if (handled > 0)
                    {
                        index += handled;
                        continue;
                    }
                }

                if (current == '&')
                {
                    Match entity = EntityPattern.Match(text, index);
                    if (entity.Success)
                    {
                        builder.Append(entity.Value);
                        index += entity.Length;
                        continue;
                    }
                }

                if (current == '\n')
                {
                    if (index >= 2 && text[index - 1] == ' ' && text[index - 2] == ' ')
                    {
                        while (builder.Length > 0 && builder[builder.Length - 1] == ' ')
                        {
                            builder.Length--;
                        }

                        builder.Append("<br />\n");
                    }
                    else
                    {
                        builder.Append('\n');
                    }

                    index++;
                    continue;
                }

                AppendEscapedChar(builder, current);
                index++;
            }

            return builder.ToString();
        }

        private int TryRenderEmphasis(
            string text,
            int index,
            MarkdownState state,
            bool insideLink,
            StringBuilder builder)
        {
            char marker = text[index];
            int run = CountRun(text, index, marker);

            if (marker == '_' && index > 0 && char.IsLetterOrDigit(text[index - 1]) &&
                index + run < text.Length && char.IsLetterOrDigit(text[index + run]))
            {
                return 0;
            }

            if (run >= 3)
            {
                int close3 = FindDelimiter(text, index, new string(marker, 3));
                if (close3 > index + 3)
                {
                    string inner = text.Substring(index + 3, close3 - (index + 3));
                    builder.Append("<strong><em>");
                    builder.Append(RenderInline(inner, state, insideLink));
                    builder.Append("</em></strong>");
                    return close3 + 3 - index;
                }
            }

            if (run >= 2)
            {
                int close2 = FindDelimiter(text, index, new string(marker, 2));
                if (close2 > index + 2)
                {
                    string inner = text.Substring(index + 2, close2 - (index + 2));
                    if (!string.IsNullOrWhiteSpace(inner))
                    {
                        builder.Append("<strong>");
                        builder.Append(RenderInline(inner, state, insideLink));
                        builder.Append("</strong>");
                        return close2 + 2 - index;
                    }
                }
            }

            if (index + 1 < text.Length && !char.IsWhiteSpace(text[index + 1]))
            {
                int close1 = FindDelimiter(text, index, new string(marker, 1));
                if (close1 > index + 1)
                {
                    string inner = text.Substring(index + 1, close1 - (index + 1));
                    if (!string.IsNullOrWhiteSpace(inner))
                    {
                        builder.Append("<em>");
                        builder.Append(RenderInline(inner, state, insideLink));
                        builder.Append("</em>");
                        return close1 + 1 - index;
                    }
                }
            }

            return 0;
        }

        private string TryBuildLinkOrImage(
            string text,
            int start,
            MarkdownState state,
            bool isImage,
            bool insideLink,
            out int consumed)
        {
            consumed = 0;
            int bracketStart = isImage ? start + 1 : start;
            int closeLabel = FindClosingBracket(text, bracketStart);
            if (closeLabel < 0)
            {
                return null;
            }

            string label = text.Substring(bracketStart + 1, closeLabel - bracketStart - 1);
            int after = closeLabel + 1;
            string destination = null;
            string title = null;

            if (after < text.Length && text[after] == '(')
            {
                int closeParen = FindClosingParen(text, after);
                if (closeParen < 0)
                {
                    return null;
                }

                string inside = text.Substring(after + 1, closeParen - after - 1);
                ParseDestination(inside, out destination, out title);
                consumed = closeParen + 1 - start;
            }
            else if (after < text.Length && text[after] == '[')
            {
                int closeReference = text.IndexOf(']', after + 1);
                if (closeReference < 0)
                {
                    return null;
                }

                string referenceId = text.Substring(after + 1, closeReference - after - 1);
                if (referenceId.Length == 0)
                {
                    referenceId = label;
                }

                LinkReference reference;
                if (!state.References.TryGetValue(referenceId.Trim(), out reference))
                {
                    return null;
                }

                destination = reference.Url;
                title = reference.Title;
                consumed = closeReference + 1 - start;
            }
            else
            {
                LinkReference reference;
                if (!state.References.TryGetValue(label.Trim(), out reference))
                {
                    return null;
                }

                destination = reference.Url;
                title = reference.Title;
                consumed = closeLabel + 1 - start;
            }

            if (string.IsNullOrEmpty(destination))
            {
                destination = "#";
            }

            if (isImage)
            {
                string source = PathResolver.ResolveImage(destination, state.Context);
                StringBuilder builder = new StringBuilder();
                builder.Append("<img src=\"");
                builder.Append(WebUtility.HtmlEncode(source));
                builder.Append("\" alt=\"");
                builder.Append(WebUtility.HtmlEncode(PlainText(label)));
                builder.Append('"');
                if (!string.IsNullOrEmpty(title))
                {
                    builder.Append(" title=\"");
                    builder.Append(WebUtility.HtmlEncode(title));
                    builder.Append('"');
                }

                builder.Append(" />");
                return builder.ToString();
            }

            if (insideLink)
            {
                return null;
            }

            string href = PathResolver.ResolveHref(destination, state.Context);
            StringBuilder link = new StringBuilder();
            link.Append("<a href=\"");
            link.Append(WebUtility.HtmlEncode(href));
            link.Append('"');
            if (!string.IsNullOrEmpty(title))
            {
                link.Append(" title=\"");
                link.Append(WebUtility.HtmlEncode(title));
                link.Append('"');
            }

            link.Append('>');
            link.Append(RenderInline(label, state, true));
            link.Append("</a>");
            return link.ToString();
        }

        private static void ParseDestination(string value, out string destination, out string title)
        {
            destination = string.Empty;
            title = string.Empty;
            string text = value.Trim();
            if (text.Length == 0)
            {
                return;
            }

            string rest;
            if (text[0] == '<')
            {
                int end = text.IndexOf('>');
                if (end < 0)
                {
                    destination = text;
                    return;
                }

                destination = text.Substring(1, end - 1);
                rest = text.Substring(end + 1).Trim();
            }
            else
            {
                int whitespace = -1;
                for (int i = 0; i < text.Length; i++)
                {
                    if (char.IsWhiteSpace(text[i]))
                    {
                        whitespace = i;
                        break;
                    }
                }

                if (whitespace < 0)
                {
                    destination = text;
                    return;
                }

                destination = text.Substring(0, whitespace);
                rest = text.Substring(whitespace).Trim();
            }

            if (rest.Length >= 2)
            {
                char first = rest[0];
                char last = rest[rest.Length - 1];
                if ((first == '"' && last == '"') || (first == '\'' && last == '\'') ||
                    (first == '(' && last == ')'))
                {
                    title = rest.Substring(1, rest.Length - 2);
                }
            }
        }

        private static int FindClosingBracket(string text, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '`')
                {
                    int run = CountRun(text, i, '`');
                    int end = FindCodeSpanEnd(text, i + run, run);
                    if (end >= 0)
                    {
                        i = end + run - 1;
                        continue;
                    }
                }

                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static int FindClosingParen(string text, int openIndex)
        {
            int depth = 0;
            for (int i = openIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\')
                {
                    i++;
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static int FindDelimiter(string text, int start, string marker)
        {
            int index = start + marker.Length;
            while (index <= text.Length - marker.Length)
            {
                int found = text.IndexOf(marker, index, StringComparison.Ordinal);
                if (found < 0)
                {
                    return -1;
                }

                int backslashes = 0;
                for (int i = found - 1; i >= 0 && text[i] == '\\'; i--)
                {
                    backslashes++;
                }

                if (backslashes % 2 == 1)
                {
                    index = found + marker.Length;
                    continue;
                }

                if (found == start + marker.Length)
                {
                    index = found + marker.Length;
                    continue;
                }

                if (marker.Length == 1)
                {
                    char markerChar = marker[0];
                    if ((found > 0 && text[found - 1] == markerChar) ||
                        (found + 1 < text.Length && text[found + 1] == markerChar))
                    {
                        index = found + 1;
                        continue;
                    }
                }

                return found;
            }

            return -1;
        }

        private static int FindCodeSpanEnd(string text, int start, int run)
        {
            string marker = new string('`', run);
            int found = text.IndexOf(marker, start, StringComparison.Ordinal);
            return found;
        }

        private static int CountRun(string text, int start, char c)
        {
            int count = 0;
            while (start + count < text.Length && text[start + count] == c)
            {
                count++;
            }

            return count;
        }

        private static bool IsEscapable(char c)
        {
            return "\\`*_{}[]()#+-.!|>~<&\"'=$%^:;?,/@".IndexOf(c) >= 0;
        }

        private static bool StartsWithAt(string text, int index, string value)
        {
            return index + value.Length <= text.Length &&
                string.Compare(text, index, value, 0, value.Length, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static bool IsUrlAutolink(string value)
        {
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsEmailAutolink(string value)
        {
            string candidate = value.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                ? value.Substring(7)
                : value;
            if (candidate.IndexOf('@') <= 0 || candidate.IndexOf(' ') >= 0)
            {
                return false;
            }

            return candidate.IndexOf('.') > candidate.IndexOf('@');
        }

        private static string PlainText(string value)
        {
            string text = value ?? string.Empty;
            text = Regex.Replace(text, @"!\[([^\]]*)\]\([^)]*\)", "$1");
            text = Regex.Replace(text, @"\[([^\]]*)\]\([^)]*\)", "$1");
            text = Regex.Replace(text, @"<[^>]+>", string.Empty);
            text = text.Replace("`", string.Empty);
            text = text.Replace("**", string.Empty);
            text = text.Replace("__", string.Empty);
            text = text.Replace("~~", string.Empty);
            text = text.Replace("*", string.Empty);
            text = text.Replace("_", string.Empty);
            return text;
        }

        private static string StripTrailingHashes(string value)
        {
            string text = value.TrimEnd();
            int end = text.Length;
            while (end > 0 && text[end - 1] == '#')
            {
                end--;
            }

            if (end < text.Length && end > 0 && char.IsWhiteSpace(text[end - 1]))
            {
                return text.Substring(0, end).TrimEnd();
            }

            return text;
        }

        private static string FirstToken(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            int space = value.IndexOfAny(new char[] { ' ', '\t' });
            if (space < 0)
            {
                return value;
            }

            return value.Substring(0, space);
        }

        private static bool TryGetFence(
            string line,
            out char fenceChar,
            out int fenceLength,
            out string info)
        {
            fenceChar = '\0';
            fenceLength = 0;
            info = string.Empty;

            int index = 0;
            while (index < line.Length && index < 4 && line[index] == ' ')
            {
                index++;
            }

            if (index > 3 || index >= line.Length)
            {
                return false;
            }

            char c = line[index];
            if (c != '`' && c != '~')
            {
                return false;
            }

            int start = index;
            while (index < line.Length && line[index] == c)
            {
                index++;
            }

            int length = index - start;
            if (length < 3)
            {
                return false;
            }

            fenceChar = c;
            fenceLength = length;
            info = line.Substring(index).Trim();
            return true;
        }

        private static bool IsFenceClose(string line, char fenceChar, int fenceLength)
        {
            int index = 0;
            while (index < line.Length && index < 4 && line[index] == ' ')
            {
                index++;
            }

            if (index > 3 || index >= line.Length || line[index] != fenceChar)
            {
                return false;
            }

            int count = 0;
            while (index < line.Length && line[index] == fenceChar)
            {
                count++;
                index++;
            }

            if (count < fenceLength)
            {
                return false;
            }

            return line.Substring(index).Trim().Length == 0;
        }

        private static int FindFenceClose(List<string> lines, int start, int end, char fenceChar, int fenceLength)
        {
            for (int i = start; i < end; i++)
            {
                if (IsFenceClose(lines[i], fenceChar, fenceLength))
                {
                    return i;
                }
            }

            return end;
        }

        private static bool IsIndentedCode(string line)
        {
            return line.StartsWith("    ", StringComparison.Ordinal);
        }

        private static string StripIndent(string line, int spaces)
        {
            if (line.Length <= spaces)
            {
                return string.Empty;
            }

            return line.Substring(spaces);
        }

        private static int GetIndent(string line)
        {
            int count = 0;
            while (count < line.Length && line[count] == ' ')
            {
                count++;
            }

            return count;
        }

        private static bool IsBlank(string line)
        {
            return string.IsNullOrWhiteSpace(line);
        }

        private static bool IsHtmlBlockStart(string line)
        {
            string value = line.TrimStart();
            if (value.StartsWith("<!--", StringComparison.Ordinal) ||
                value.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Match tag = HtmlTagNamePattern.Match(value);
            return tag.Success && HtmlBlockTags.Contains(tag.Groups[1].Value);
        }

        private static bool StartsBlock(string line)
        {
            char fenceChar;
            int fenceLength;
            string info;
            if (TryGetFence(line, out fenceChar, out fenceLength, out info))
            {
                return true;
            }

            if (HeadingPattern.IsMatch(line) || HorizontalRulePattern.IsMatch(line) ||
                BlockquotePattern.IsMatch(line) || ListItemPattern.IsMatch(line) ||
                IsHtmlBlockStart(line))
            {
                return true;
            }

            return false;
        }

        private bool IsBlockStarter(List<string> lines, int index, int end)
        {
            string line = lines[index];
            if (StartsBlock(line))
            {
                return true;
            }

            return IsTableStart(lines, index, end);
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (char c in value)
            {
                AppendEscapedChar(builder, c);
            }
        }

        private static void AppendEscapedChar(StringBuilder builder, char c)
        {
            switch (c)
            {
                case '&':
                    builder.Append("&amp;");
                    break;
                case '<':
                    builder.Append("&lt;");
                    break;
                case '>':
                    builder.Append("&gt;");
                    break;
                case '"':
                    builder.Append("&quot;");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }
    }
}
