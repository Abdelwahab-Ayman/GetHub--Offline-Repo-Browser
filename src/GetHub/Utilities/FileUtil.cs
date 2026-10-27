using System;
using System.IO;
using System.Text;

namespace GetHub.Utilities
{
    public class TextFileContent
    {
        public string Text { get; set; }
        public bool Truncated { get; set; }
        public bool Binary { get; set; }
        public long Size { get; set; }
        public int LineCount { get; set; }
        public Encoding Encoding { get; set; }

        public TextFileContent()
        {
            Text = string.Empty;
            Encoding = Encoding.UTF8;
        }
    }

    public static class FileUtil
    {
        private static readonly string[] ImageExtensions =
            new string[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".svg", ".webp", ".avif", ".tif", ".tiff" };

        private static readonly string[] MarkdownExtensions =
            new string[] { ".md", ".markdown", ".mdown", ".mkd", ".mdx", ".txt" };

        public static bool IsImage(string path)
        {
            return Array.IndexOf(ImageExtensions, Path.GetExtension(path).ToLowerInvariant()) >= 0;
        }

        public static bool IsSvg(string path)
        {
            return string.Equals(Path.GetExtension(path), ".svg", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsWebp(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext == ".webp" || ext == ".avif";
        }

        public static bool IsMarkdown(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (Array.IndexOf(MarkdownExtensions, ext) >= 0)
            {
                return true;
            }

            string name = Path.GetFileName(path).ToLowerInvariant();
            if (name == "readme" || name == "license" || name == "changelog" || name == "contributing" || name == "authors")
            {
                return true;
            }

            return false;
        }

        public static bool IsPdf(string path)
        {
            return string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public static string GetLanguage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".cs": return "csharp";
                case ".csx": return "csharp";
                case ".fs": return "fsharp";
                case ".vb": return "vb";
                case ".java": return "java";
                case ".kt":
                case ".kts": return "kotlin";
                case ".js":
                case ".mjs":
                case ".cjs": return "javascript";
                case ".jsx": return "javascript";
                case ".ts": return "typescript";
                case ".tsx": return "typescript";
                case ".c": return "c";
                case ".h": return "c";
                case ".cpp":
                case ".cc":
                case ".cxx":
                case ".hpp":
                case ".hh": return "cpp";
                case ".go": return "go";
                case ".rs": return "rust";
                case ".swift": return "swift";
                case ".php": return "php";
                case ".rb": return "ruby";
                case ".py":
                case ".pyw": return "python";
                case ".sh":
                case ".bash":
                case ".zsh": return "bash";
                case ".ps1":
                case ".psm1": return "powershell";
                case ".bat":
                case ".cmd": return "bat";
                case ".sql": return "sql";
                case ".json":
                case ".jsonc": return "json";
                case ".xml":
                case ".xaml":
                case ".csproj":
                case ".vbproj":
                case ".props":
                case ".targets":
                case ".config": return "xml";
                case ".html":
                case ".htm": return "html";
                case ".svg": return "xml";
                case ".css": return "css";
                case ".scss":
                case ".sass":
                case ".less": return "css";
                case ".yml":
                case ".yaml": return "yaml";
                case ".toml": return "toml";
                case ".ini": return "ini";
                case ".r": return "r";
                case ".lua": return "lua";
                case ".dart": return "dart";
                case ".groovy":
                case ".gradle": return "groovy";
                case ".pl":
                case ".pm": return "perl";
                case ".md":
                case ".markdown":
                case ".mdown":
                case ".mkd":
                case ".mdx": return "markdown";
                case ".diff":
                case ".patch": return "diff";
                default: return string.Empty;
            }
        }

        public static TextFileContent ReadText(string path, int maxBytes)
        {
            TextFileContent result = new TextFileContent();
            FileInfo info = new FileInfo(path);
            result.Size = info.Exists ? info.Length : 0;

            byte[] bytes;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int wanted = (int)Math.Min(stream.Length, maxBytes);
                bytes = new byte[wanted];
                int read = 0;
                while (read < wanted)
                {
                    int chunk = stream.Read(bytes, read, wanted - read);
                    if (chunk <= 0)
                    {
                        break;
                    }

                    read += chunk;
                }

                if (read < wanted)
                {
                    Array.Resize<byte>(ref bytes, read);
                }

                result.Truncated = stream.Length > read;
            }

            if (LooksBinary(bytes))
            {
                result.Binary = true;
                result.Text = string.Empty;
                return result;
            }

            string text;
            Encoding encoding;
            if (TryDecode(bytes, result.Truncated, out text, out encoding))
            {
                result.Text = text;
                result.Encoding = encoding;
            }
            else
            {
                result.Binary = true;
                return result;
            }

            result.LineCount = CountLines(text);
            return result;
        }

        public static string ReadAllText(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string text;
            Encoding encoding;
            if (TryDecode(bytes, false, out text, out encoding))
            {
                return text;
            }

            return Encoding.Default.GetString(bytes);
        }

        public static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int lines = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    lines++;
                }
            }

            if (text.EndsWith("\n", StringComparison.Ordinal))
            {
                lines--;
            }

            return lines;
        }

        private static bool LooksBinary(byte[] bytes)
        {
            int limit = Math.Min(bytes.Length, 8192);
            int controlCount = 0;
            for (int i = 0; i < limit; i++)
            {
                byte b = bytes[i];
                if (b == 0)
                {
                    return true;
                }

                if (b < 8 || (b > 13 && b < 32))
                {
                    controlCount++;
                }
            }

            return limit > 0 && controlCount > limit / 20;
        }

        private static bool TryDecode(byte[] bytes, bool truncated, out string text, out Encoding encoding)
        {
            text = null;
            encoding = Encoding.UTF8;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = new UTF8Encoding(false);
                text = encoding.GetString(bytes, 3, bytes.Length - 3);
                return true;
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                encoding = Encoding.Unicode;
                text = encoding.GetString(bytes, 2, bytes.Length - 2);
                return true;
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                encoding = Encoding.BigEndianUnicode;
                text = encoding.GetString(bytes, 2, bytes.Length - 2);
                return true;
            }

            UTF8Encoding strict = new UTF8Encoding(false, true);
            int length = bytes.Length;
            int minLength = truncated ? Math.Max(0, length - 4) : length;
            while (length >= minLength)
            {
                try
                {
                    encoding = new UTF8Encoding(false);
                    text = strict.GetString(bytes, 0, length);
                    return true;
                }
                catch (DecoderFallbackException)
                {
                    if (!truncated)
                    {
                        break;
                    }

                    length--;
                }
            }

            try
            {
                encoding = Encoding.Default;
                text = encoding.GetString(bytes);
                return !ContainsMostlyGarbage(text);
            }
            catch
            {
                return false;
            }
        }

        private static bool ContainsMostlyGarbage(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            int sample = Math.Min(text.Length, 4096);
            int replacement = 0;
            for (int i = 0; i < sample; i++)
            {
                if (text[i] == '\uFFFD')
                {
                    replacement++;
                }
            }

            return replacement > sample / 10;
        }
    }
}
