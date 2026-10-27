using System;
using System.Collections.Generic;
using System.Drawing;

namespace GetHub.Utilities
{
    public static class Display
    {
        private static readonly Dictionary<string, string> LanguageColors =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "C#", "#178600" },
                { "C++", "#f34b7d" },
                { "C", "#555555" },
                { "CSS", "#563d7c" },
                { "Dart", "#00B4AB" },
                { "Go", "#00ADD8" },
                { "HTML", "#e34c26" },
                { "Java", "#b07219" },
                { "JavaScript", "#f1e05a" },
                { "Kotlin", "#A97BFF" },
                { "Lua", "#000080" },
                { "PHP", "#4F5D95" },
                { "Python", "#3572A5" },
                { "Ruby", "#701516" },
                { "Rust", "#dea584" },
                { "Shell", "#89e051" },
                { "Swift", "#F05138" },
                { "TypeScript", "#3178c6" },
                { "Vue", "#41b883" },
                { "F#", "#b845fc" },
                { "Scala", "#c22d40" },
                { "PowerShell", "#012456" },
                { "Dockerfile", "#384d54" }
            };

        public static string Count(int value)
        {
            if (value >= 1000000)
            {
                return (value / 1000000.0).ToString("0.#") + "m";
            }

            if (value >= 1000)
            {
                return (value / 1000.0).ToString("0.#") + "k";
            }

            return value.ToString();
        }

        public static string HumanSize(long bytes)
        {
            if (bytes < 1024)
            {
                return bytes.ToString() + " B";
            }

            double value = bytes;
            string[] units = new string[] { "KB", "MB", "GB", "TB" };
            int unit = -1;
            do
            {
                value = value / 1024.0;
                unit++;
            }
            while (value >= 1024.0 && unit < units.Length - 1);

            if (value >= 100)
            {
                return value.ToString("0") + " " + units[unit];
            }

            return value.ToString("0.#") + " " + units[unit];
        }

        public static string RelativeTime(DateTime utc)
        {
            if (utc == DateTime.MinValue)
            {
                return "never";
            }

            DateTime local = utc.Kind == DateTimeKind.Utc ? utc.ToLocalTime() : utc;
            TimeSpan span = DateTime.Now - local;

            if (span.TotalSeconds < 0)
            {
                return "just now";
            }

            if (span.TotalSeconds < 60)
            {
                return "just now";
            }

            if (span.TotalMinutes < 60)
            {
                return ((int)span.TotalMinutes) + "m ago";
            }

            if (span.TotalHours < 24)
            {
                return ((int)span.TotalHours) + "h ago";
            }

            if (span.TotalDays < 30)
            {
                return ((int)span.TotalDays) + "d ago";
            }

            if (span.TotalDays < 365)
            {
                return ((int)(span.TotalDays / 30)) + "mo ago";
            }

            return local.ToString("MMM d, yyyy");
        }

        public static Color LanguageColor(string language)
        {
            string hex;
            if (!string.IsNullOrEmpty(language) && LanguageColors.TryGetValue(language, out hex))
            {
                return ColorTranslator.FromHtml(hex);
            }

            return Color.FromArgb(139, 148, 158);
        }
    }
}
