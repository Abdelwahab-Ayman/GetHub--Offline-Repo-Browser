using System;
using System.Drawing;
using System.Text;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Rendering
{
    // Shared CSS and small helpers for the signed-in pages (Issues, Projects).
    // The rules are appended to SiteBuilder's repository stylesheet so both sets
    // of pages use the same fonts and the same dark/light palette.
    internal static class GitHubPageStyles
    {
        public static string Css
        {
            get
            {
                StringBuilder css = new StringBuilder();
                css.Append("\n");
                css.Append(".gh-top { overflow:hidden; margin:0 0 6px; }\n");
                css.Append(".gh-top .gh-right { float:right; padding-top:6px; }\n");
                css.Append(".gh-title { margin:0; font-size:26px; font-weight:600; color:");
                css.Append(Hex(UiTheme.Text));
                css.Append("; line-height:1.25; }\n");
                css.Append(".gh-title .num { color:");
                css.Append(Hex(UiTheme.Faint));
                css.Append("; font-weight:400; }\n");
                css.Append(".gh-sub { color:");
                css.Append(Hex(UiTheme.Muted));
                css.Append("; font-size:13px; margin-top:5px; }\n");
                css.Append(".gh-btn { display:inline-block; padding:6px 14px; border-radius:6px; border:1px solid ");
                css.Append(Hex(UiTheme.Green));
                css.Append("; background:");
                css.Append(Hex(UiTheme.Green));
                css.Append("; color:#ffffff; font-size:13px; font-weight:600; text-decoration:none; }\n");
                css.Append(".gh-btn:hover { text-decoration:none; background:#2ea043; }\n");
                css.Append(".gh-btn.secondary { background:");
                css.Append(Hex(UiTheme.Surface));
                css.Append("; border-color:");
                css.Append(Hex(UiTheme.Border));
                css.Append("; color:");
                css.Append(Hex(UiTheme.Text));
                css.Append("; }\n");
                css.Append(".gh-btn.secondary:hover { background:");
                css.Append(Hex(UiTheme.HoverBackground));
                css.Append("; }\n");
                css.Append(".gh-filters { margin:16px 0 10px; }\n");
                css.Append(".gh-filter { display:inline-block; padding:5px 12px; border:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; border-right:none; background:");
                css.Append(Hex(UiTheme.Surface));
                css.Append("; color:");
                css.Append(Hex(UiTheme.Text));
                css.Append("; font-size:12px; text-decoration:none; }\n");
                css.Append(".gh-filter:first-child { border-radius:6px 0 0 6px; }\n");
                css.Append(".gh-filter:last-child { border-right:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; border-radius:0 6px 6px 0; }\n");
                css.Append(".gh-filter:hover { background:");
                css.Append(Hex(UiTheme.HoverBackground));
                css.Append("; text-decoration:none; }\n");
                css.Append(".gh-filter.active { background:");
                css.Append(Hex(UiTheme.Accent));
                css.Append("; border-color:");
                css.Append(Hex(UiTheme.Accent));
                css.Append("; color:#ffffff; font-weight:600; }\n");
                css.Append(".gh-list { border:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; border-radius:6px; background:");
                css.Append(Hex(UiTheme.Canvas));
                css.Append("; overflow:hidden; }\n");
                css.Append(".gh-row { position:relative; padding:12px 16px 12px 44px; border-bottom:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; }\n");
                css.Append(".gh-row:last-child { border-bottom:none; }\n");
                css.Append(".gh-row:hover { background:");
                css.Append(Hex(UiTheme.HoverBackground));
                css.Append("; }\n");
                css.Append(".gh-state { position:absolute; left:16px; top:13px; width:18px; text-align:center; font-size:17px; font-weight:700; line-height:20px; }\n");
                css.Append(".gh-state.open { color:");
                css.Append(Hex(UiTheme.Green));
                css.Append("; }\n");
                css.Append(".gh-state.closed { color:#8250df; }\n");
                css.Append(".gh-state.notplanned { color:");
                css.Append(Hex(UiTheme.Faint));
                css.Append("; }\n");
                css.Append(".gh-row-title { font-weight:600; font-size:15px; color:");
                css.Append(Hex(UiTheme.Text));
                css.Append("; text-decoration:none; }\n");
                css.Append(".gh-row-title:hover { color:");
                css.Append(Hex(UiTheme.Accent));
                css.Append("; text-decoration:none; }\n");
                css.Append(".gh-label { display:inline-block; padding:0 7px; border-radius:10px; font-size:11px; font-weight:600; line-height:18px; margin-left:5px; vertical-align:1px; }\n");
                css.Append(".gh-meta { color:");
                css.Append(Hex(UiTheme.Muted));
                css.Append("; font-size:12px; margin-top:4px; }\n");
                css.Append(".gh-meta a { color:");
                css.Append(Hex(UiTheme.Muted));
                css.Append("; }\n");
                css.Append(".gh-notice { margin:0 0 14px; padding:10px 14px; border:1px solid #9e6a03; background:rgba(187,128,9,0.12); color:");
                css.Append(UiTheme.IsLight ? "#7d4e00" : "#d29922");
                css.Append("; border-radius:6px; font-size:13px; }\n");
                css.Append(".gh-notice.bad { border-color:#da3633; background:rgba(248,81,73,0.12); color:");
                css.Append(UiTheme.IsLight ? "#a40e26" : "#ffa198");
                css.Append("; }\n");
                css.Append(".gh-empty { border:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; border-radius:6px; background:");
                css.Append(Hex(UiTheme.Surface));
                css.Append("; padding:46px 24px; text-align:center; }\n");
                css.Append(".gh-empty .mark { font-size:36px; color:");
                css.Append(Hex(UiTheme.Faint));
                css.Append("; line-height:1; margin-bottom:12px; }\n");
                css.Append(".gh-empty h2 { margin:0 0 8px; font-size:19px; color:");
                css.Append(Hex(UiTheme.Text));
                css.Append("; }\n");
                css.Append(".gh-empty p { margin:0 auto 18px; max-width:520px; color:");
                css.Append(Hex(UiTheme.Muted));
                css.Append("; font-size:13px; line-height:1.55; }\n");
                css.Append(".gh-detail { border:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; border-radius:6px; background:");
                css.Append(Hex(UiTheme.Canvas));
                css.Append("; margin-bottom:18px; overflow:hidden; }\n");
                css.Append(".gh-detail-head { padding:11px 16px; border-bottom:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; background:");
                css.Append(Hex(UiTheme.Surface));
                css.Append("; font-size:13px; color:");
                css.Append(Hex(UiTheme.Muted));
                css.Append("; }\n");
                css.Append(".gh-detail-body { padding:18px 20px; }\n");
                css.Append(".gh-comment { border:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; border-radius:6px; background:");
                css.Append(Hex(UiTheme.Canvas));
                css.Append("; margin:16px 0; overflow:hidden; }\n");
                css.Append(".gh-comment-head { padding:9px 14px; background:");
                css.Append(Hex(UiTheme.Surface));
                css.Append("; border-bottom:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; font-size:13px; color:");
                css.Append(Hex(UiTheme.Muted));
                css.Append("; }\n");
                css.Append(".gh-comment-head b { color:");
                css.Append(Hex(UiTheme.Text));
                css.Append("; }\n");
                css.Append(".gh-comment-body { padding:14px 16px; }\n");
                css.Append(".gh-comment-body.empty { color:");
                css.Append(Hex(UiTheme.Faint));
                css.Append("; font-style:italic; }\n");
                css.Append(".gh-badge { display:inline-block; padding:3px 10px; border-radius:20px; font-size:12px; font-weight:600; color:#ffffff; background:");
                css.Append(Hex(UiTheme.Green));
                css.Append("; margin-left:8px; vertical-align:3px; }\n");
                css.Append(".gh-badge.closed { background:#8250df; }\n");
                css.Append(".gh-badge.notplanned { background:#6e7781; }\n");
                css.Append(".gh-badge.draft { background:");
                css.Append(Hex(UiTheme.Faint));
                css.Append("; }\n");
                css.Append(".gh-badge.issue { background:#1f6feb; }\n");
                css.Append(".gh-badge.pr { background:#8250df; }\n");
                css.Append(".gh-badge.closed, .gh-badge.notplanned { color:#ffffff; }\n");
                css.Append(".gh-back { font-size:13px; }\n");
                css.Append(".gh-tabs { margin:14px 0 18px; }\n");
                css.Append(".gh-tab { display:inline-block; padding:5px 12px; margin:0 6px 6px 0; border:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; border-radius:6px; background:");
                css.Append(Hex(UiTheme.Surface));
                css.Append("; color:");
                css.Append(Hex(UiTheme.Text));
                css.Append("; font-size:12px; text-decoration:none; }\n");
                css.Append(".gh-tab:hover { background:");
                css.Append(Hex(UiTheme.HoverBackground));
                css.Append("; text-decoration:none; }\n");
                css.Append(".gh-tab.active { background:");
                css.Append(Hex(UiTheme.Accent));
                css.Append("; border-color:");
                css.Append(Hex(UiTheme.Accent));
                css.Append("; color:#ffffff; font-weight:600; }\n");
                css.Append(".gh-desc { color:");
                css.Append(Hex(UiTheme.Muted));
                css.Append("; font-size:13px; margin:4px 0 14px; }\n");
                css.Append(".board { width:100%; border-collapse:separate; border-spacing:10px 0; table-layout:fixed; margin-top:4px; }\n");
                css.Append(".board td.gh-column { vertical-align:top; background:");
                css.Append(Hex(UiTheme.Surface));
                css.Append("; border:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; border-radius:6px; padding:8px; }\n");
                css.Append(".gh-col-title { font-size:12px; font-weight:600; color:");
                css.Append(Hex(UiTheme.Muted));
                css.Append("; padding:2px 4px 9px; text-transform:uppercase; letter-spacing:.04em; }\n");
                css.Append(".gh-col-count { color:");
                css.Append(Hex(UiTheme.Faint));
                css.Append("; font-weight:400; }\n");
                css.Append(".gh-card { background:");
                css.Append(Hex(UiTheme.Canvas));
                css.Append("; border:1px solid ");
                css.Append(Hex(UiTheme.Border));
                css.Append("; border-radius:6px; padding:8px 10px; margin-bottom:8px; font-size:13px; word-wrap:break-word; }\n");
                css.Append(".gh-card a { color:");
                css.Append(Hex(UiTheme.Text));
                css.Append("; font-weight:600; text-decoration:none; }\n");
                css.Append(".gh-card a:hover { color:");
                css.Append(Hex(UiTheme.Accent));
                css.Append("; }\n");
                css.Append(".gh-card .meta { color:");
                css.Append(Hex(UiTheme.Muted));
                css.Append("; font-size:11px; margin-top:4px; }\n");
                css.Append(".gh-card .gh-badge { margin-left:0; font-size:10px; padding:2px 7px; }\n");
                css.Append(".gh-column-empty { color:");
                css.Append(Hex(UiTheme.Faint));
                css.Append("; font-size:12px; padding:6px 4px 10px; }\n");
                css.Append(".gh-foot { margin-top:22px; font-size:12px; color:");
                css.Append(Hex(UiTheme.Faint));
                css.Append("; }\n");
                return css.ToString();
            }
        }

        public static string Hex(Color color)
        {
            return string.Format("#{0:x2}{1:x2}{2:x2}", color.R, color.G, color.B);
        }

        public static string E(string value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
        }

        public static string LabelHtml(IssueLabel label)
        {
            if (label == null || string.IsNullOrEmpty(label.Name))
            {
                return string.Empty;
            }

            return "<span class=\"gh-label\" style=\"background:" + E(label.Background) +
                ";color:" + E(label.Foreground) + "\">" + E(label.Name) + "</span>";
        }

        public static string StateClass(IssueSummary issue)
        {
            if (issue == null || issue.IsOpen)
            {
                return "open";
            }

            return string.Equals(issue.StateReason, "not_planned", StringComparison.OrdinalIgnoreCase)
                ? "notplanned"
                : "closed";
        }

        public static string StateMark(IssueSummary issue)
        {
            string state = StateClass(issue);
            if (state == "open")
            {
                return "&#9675;";
            }

            return state == "notplanned" ? "&#8856;" : "&#10003;";
        }

        public static string StateBadge(IssueSummary issue)
        {
            string state = StateClass(issue);
            string className = state == "open" ? "gh-badge" : "gh-badge " + state;
            return "<span class=\"" + className + "\">" + E(issue == null ? "Open" : issue.StateLabel) + "</span>";
        }

        public static string OpenInGitHub(string url, string text)
        {
            if (string.IsNullOrEmpty(url))
            {
                return string.Empty;
            }

            return "<a href=\"" + E(url) + "\">" + E(text) + "</a>";
        }
    }
}
