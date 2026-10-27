using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace GetHub.Rendering
{
    public static class HtmlSanitizer
    {
        private static readonly HashSet<string> AllowedTags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "a", "img", "br", "hr", "p", "div", "span", "strong", "b", "em", "i",
                "u", "s", "del", "ins", "sup", "sub", "kbd", "code", "pre", "blockquote",
                "ul", "ol", "li", "table", "thead", "tbody", "tfoot", "tr", "th", "td",
                "h1", "h2", "h3", "h4", "h5", "h6", "details", "summary", "picture",
                "source", "center", "dl", "dt", "dd", "mark", "small"
            };

        private static readonly HashSet<string> VoidTags =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "br", "img", "hr", "source", "wbr", "area", "col"
            };

        private static readonly HashSet<string> AllowedAttributes =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "href", "src", "alt", "title", "width", "height", "align",
                "colspan", "rowspan", "id", "class"
            };

        private static readonly Regex DangerousBlocks = new Regex(
            @"<\s*(script|style|iframe|object|embed|noscript|template|svg|math)\b[^>]*>.*?<\s*/\s*\1\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex DangerousTags = new Regex(
            @"<\s*/?\s*(script|style|iframe|object|embed|noscript|template|svg|math)\b[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex CommentPattern = new Regex(
            @"<!--.*?-->",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex TagPattern = new Regex(
            @"^<\s*(/?)\s*([a-zA-Z][a-zA-Z0-9]*)(.*?)(/?)\s*>$",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex AttributePattern = new Regex(
            @"([a-zA-Z_:][a-zA-Z0-9:_.-]*)(?:\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s""'>]+)))?",
            RegexOptions.Compiled);

        public static string Sanitize(string html, RepoContext context)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            string value = CommentPattern.Replace(html, string.Empty);
            value = DangerousBlocks.Replace(value, string.Empty);
            value = DangerousTags.Replace(value, string.Empty);

            return Regex.Replace(value, "<[^>]*>", delegate(Match match)
            {
                return SanitizeTag(match.Value, context);
            });
        }

        public static string SanitizeTag(string tag, RepoContext context)
        {
            if (string.IsNullOrEmpty(tag))
            {
                return string.Empty;
            }

            Match match = TagPattern.Match(tag.Trim());
            if (!match.Success)
            {
                return string.Empty;
            }

            string closing = match.Groups[1].Value;
            string name = match.Groups[2].Value.ToLowerInvariant();
            string attributeText = match.Groups[3].Value;
            string selfClosing = match.Groups[4].Value;

            if (!AllowedTags.Contains(name))
            {
                return string.Empty;
            }

            if (closing == "/")
            {
                return "</" + name + ">";
            }

            StringBuilder builder = new StringBuilder();
            builder.Append('<');
            builder.Append(name);

            MatchCollection attributes = AttributePattern.Matches(attributeText);
            foreach (Match attribute in attributes)
            {
                string attributeName = attribute.Groups[1].Value.ToLowerInvariant();
                if (!AllowedAttributes.Contains(attributeName))
                {
                    continue;
                }

                string attributeValue = attribute.Groups[2].Success
                    ? attribute.Groups[2].Value
                    : attribute.Groups[3].Success
                        ? attribute.Groups[3].Value
                        : attribute.Groups[4].Success
                            ? attribute.Groups[4].Value
                            : string.Empty;

                if (attributeName == "href")
                {
                    attributeValue = PathResolver.ResolveHref(attributeValue, context);
                }
                else if (attributeName == "src")
                {
                    if (attributeValue.TrimStart().StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    attributeValue = PathResolver.ResolveImage(attributeValue, context);
                    if (string.IsNullOrEmpty(attributeValue))
                    {
                        continue;
                    }
                }
                else if (attributeName == "width" || attributeName == "height" ||
                         attributeName == "colspan" || attributeName == "rowspan")
                {
                    if (!Regex.IsMatch(attributeValue, @"^[0-9]{1,5}%?$"))
                    {
                        continue;
                    }
                }
                else if (attributeName == "align")
                {
                    string lower = attributeValue.ToLowerInvariant();
                    if (lower != "left" && lower != "right" && lower != "center" && lower != "justify")
                    {
                        continue;
                    }
                }
                else if (attributeName == "id")
                {
                    if (!Regex.IsMatch(attributeValue, @"^[A-Za-z0-9_:.-]+$"))
                    {
                        continue;
                    }
                }
                else if (attributeName == "class")
                {
                    if (!Regex.IsMatch(attributeValue, @"^[A-Za-z0-9_ \t-]+$"))
                    {
                        continue;
                    }
                }

                if (attributeValue.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                    attributeValue.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                builder.Append(' ');
                builder.Append(attributeName);
                builder.Append("=\"");
                builder.Append(WebUtilityHtmlEncode(attributeValue));
                builder.Append('"');
            }

            if (selfClosing == "/" || VoidTags.Contains(name))
            {
                builder.Append(" />");
            }
            else
            {
                builder.Append('>');
            }

            return builder.ToString();
        }

        private static string WebUtilityHtmlEncode(string value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
