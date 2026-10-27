using System;
using System.Collections.Generic;
using System.Text;

namespace GetHub.Rendering
{
    public static class SyntaxHighlighter
    {
        private class LanguageDefinition
        {
            public HashSet<string> Keywords;
            public string[] LineComments;
            public string[] BlockCommentOpen;
            public string[] BlockCommentClose;
            public char[] Quotes;
            public bool IgnoreCase;
            public bool TripleQuotes;
            public bool DoubledQuotes;
            public bool Markup;
            public bool Diff;

            public LanguageDefinition()
            {
                Keywords = new HashSet<string>(StringComparer.Ordinal);
                LineComments = new string[0];
                BlockCommentOpen = new string[0];
                BlockCommentClose = new string[0];
                Quotes = new char[] { '"', '\'' };
            }
        }

        private static readonly Dictionary<string, LanguageDefinition> Definitions = CreateDefinitions();

        public static string Highlight(string code, string language)
        {
            if (string.IsNullOrEmpty(code))
            {
                return string.Empty;
            }

            LanguageDefinition definition = GetDefinition(language);
            if (definition == null)
            {
                return Escape(code);
            }

            if (definition.Markup)
            {
                return HighlightMarkup(code);
            }

            if (definition.Diff)
            {
                return HighlightDiff(code);
            }

            return HighlightGeneric(code, definition);
        }

        private static LanguageDefinition GetDefinition(string language)
        {
            if (string.IsNullOrEmpty(language))
            {
                return null;
            }

            LanguageDefinition definition;
            if (Definitions.TryGetValue(language.Trim(), out definition))
            {
                return definition;
            }

            return null;
        }

        private static string HighlightGeneric(string code, LanguageDefinition definition)
        {
            StringBuilder builder = new StringBuilder(code.Length + 128);
            int index = 0;

            while (index < code.Length)
            {
                bool matched = false;

                for (int i = 0; i < definition.BlockCommentOpen.Length; i++)
                {
                    string open = definition.BlockCommentOpen[i];
                    if (StartsWith(code, index, open))
                    {
                        string close = definition.BlockCommentClose[i];
                        int end = code.IndexOf(close, index + open.Length, StringComparison.Ordinal);
                        int stop = end < 0 ? code.Length : end + close.Length;
                        AppendSpan(builder, "cm", code, index, stop - index);
                        index = stop;
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    continue;
                }

                foreach (string lineComment in definition.LineComments)
                {
                    if (StartsWith(code, index, lineComment))
                    {
                        int end = code.IndexOf('\n', index);
                        int stop = end < 0 ? code.Length : end;
                        AppendSpan(builder, "cm", code, index, stop - index);
                        index = stop;
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    continue;
                }

                char current = code[index];

                if (definition.TripleQuotes &&
                    (StartsWith(code, index, "\"\"\"") || StartsWith(code, index, "'''")))
                {
                    string delimiter = code.Substring(index, 3);
                    int end = code.IndexOf(delimiter, index + 3, StringComparison.Ordinal);
                    int stop = end < 0 ? code.Length : end + 3;
                    AppendSpan(builder, "str", code, index, stop - index);
                    index = stop;
                    continue;
                }

                if (Array.IndexOf(definition.Quotes, current) >= 0)
                {
                    int stop = ReadString(code, index, current, definition.DoubledQuotes);
                    AppendSpan(builder, "str", code, index, stop - index);
                    index = stop;
                    continue;
                }

                if (char.IsDigit(current))
                {
                    int length = ReadNumber(code, index);
                    if (length > 0)
                    {
                        AppendSpan(builder, "num", code, index, length);
                        index += length;
                        continue;
                    }
                }

                if (IsIdentifierStart(current))
                {
                    int length = ReadIdentifier(code, index);
                    string token = code.Substring(index, length);
                    if (IsKeyword(definition, token))
                    {
                        AppendSpan(builder, "kw", code, index, length);
                    }
                    else
                    {
                        AppendEscaped(builder, code, index, length);
                    }

                    index += length;
                    continue;
                }

                AppendEscapedChar(builder, current);
                index++;
            }

            return builder.ToString();
        }

        private static string HighlightMarkup(string code)
        {
            StringBuilder builder = new StringBuilder(code.Length + 128);
            int index = 0;

            while (index < code.Length)
            {
                if (StartsWith(code, index, "<!--"))
                {
                    int end = code.IndexOf("-->", index + 4, StringComparison.Ordinal);
                    int stop = end < 0 ? code.Length : end + 3;
                    AppendSpan(builder, "cm", code, index, stop - index);
                    index = stop;
                    continue;
                }

                char current = code[index];
                if (current == '<')
                {
                    int end = FindTagEnd(code, index);
                    if (end > index)
                    {
                        AppendTag(builder, code.Substring(index, end - index + 1));
                        index = end + 1;
                        continue;
                    }
                }

                AppendEscapedChar(builder, current);
                index++;
            }

            return builder.ToString();
        }

        private static string HighlightDiff(string code)
        {
            StringBuilder builder = new StringBuilder(code.Length + 128);
            string[] lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string cssClass = null;
                if (line.StartsWith("+++", StringComparison.Ordinal) ||
                    line.StartsWith("---", StringComparison.Ordinal) ||
                    line.StartsWith("@@", StringComparison.Ordinal) ||
                    line.StartsWith("diff ", StringComparison.Ordinal) ||
                    line.StartsWith("index ", StringComparison.Ordinal))
                {
                    cssClass = "meta";
                }
                else if (line.StartsWith("+", StringComparison.Ordinal))
                {
                    cssClass = "add";
                }
                else if (line.StartsWith("-", StringComparison.Ordinal))
                {
                    cssClass = "del";
                }

                if (cssClass != null)
                {
                    builder.Append("<span class=\"");
                    builder.Append(cssClass);
                    builder.Append("\">");
                    builder.Append(Escape(line));
                    builder.Append("</span>");
                }
                else
                {
                    builder.Append(Escape(line));
                }

                if (i < lines.Length - 1)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        private static void AppendTag(StringBuilder builder, string tag)
        {
            if (tag.Length < 2)
            {
                builder.Append(Escape(tag));
                return;
            }

            int index = 1;
            builder.Append("&lt;");

            if (index < tag.Length && tag[index] == '/')
            {
                builder.Append('/');
                index++;
            }

            int nameStart = index;
            while (index < tag.Length && (char.IsLetterOrDigit(tag[index]) || tag[index] == '-' || tag[index] == ':' || tag[index] == '_'))
            {
                index++;
            }

            if (index > nameStart)
            {
                AppendSpan(builder, "tag", tag, nameStart, index - nameStart);
            }

            while (index < tag.Length)
            {
                char c = tag[index];
                if (c == '>')
                {
                    builder.Append("&gt;");
                    index++;
                    continue;
                }

                if (char.IsWhiteSpace(c) || c == '/' || c == '=')
                {
                    AppendEscapedChar(builder, c);
                    index++;
                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    int end = ReadString(tag, index, c, false);
                    AppendSpan(builder, "str", tag, index, end - index);
                    index = end;
                    continue;
                }

                if (char.IsLetter(c) || c == '_' || c == ':' || c == '@')
                {
                    int start = index;
                    while (index < tag.Length &&
                           (char.IsLetterOrDigit(tag[index]) || tag[index] == '-' ||
                            tag[index] == ':' || tag[index] == '_' || tag[index] == '@' || tag[index] == '.'))
                    {
                        index++;
                    }

                    AppendSpan(builder, "attr", tag, start, index - start);
                    continue;
                }

                AppendEscapedChar(builder, c);
                index++;
            }
        }

        private static int FindTagEnd(string code, int start)
        {
            char quote = '\0';
            for (int i = start + 1; i < code.Length; i++)
            {
                char c = code[i];
                if (quote != '\0')
                {
                    if (c == quote)
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (c == '"' || c == '\'')
                {
                    quote = c;
                    continue;
                }

                if (c == '>')
                {
                    return i;
                }
            }

            return -1;
        }

        private static int ReadString(string code, int start, char quote, bool doubledQuotes)
        {
            int index = start + 1;
            while (index < code.Length)
            {
                char c = code[index];
                if (c == '\\')
                {
                    index += 2;
                    continue;
                }

                if (c == quote)
                {
                    if (doubledQuotes && index + 1 < code.Length && code[index + 1] == quote)
                    {
                        index += 2;
                        continue;
                    }

                    return index + 1;
                }

                if (c == '\n')
                {
                    return index;
                }

                index++;
            }

            return code.Length;
        }

        private static int ReadNumber(string code, int start)
        {
            int index = start;
            if (index + 1 < code.Length && code[index] == '0' &&
                (code[index + 1] == 'x' || code[index + 1] == 'X'))
            {
                index += 2;
                while (index < code.Length && (Uri.IsHexDigit(code[index]) || code[index] == '_'))
                {
                    index++;
                }

                return index - start;
            }

            if (index + 1 < code.Length && code[index] == '0' &&
                (code[index + 1] == 'b' || code[index + 1] == 'B'))
            {
                index += 2;
                while (index < code.Length && (code[index] == '0' || code[index] == '1' || code[index] == '_'))
                {
                    index++;
                }

                return index - start;
            }

            while (index < code.Length && (char.IsDigit(code[index]) || code[index] == '_'))
            {
                index++;
            }

            if (index < code.Length && code[index] == '.')
            {
                index++;
                while (index < code.Length && (char.IsDigit(code[index]) || code[index] == '_'))
                {
                    index++;
                }
            }

            if (index < code.Length && (code[index] == 'e' || code[index] == 'E'))
            {
                int exponent = index + 1;
                if (exponent < code.Length && (code[exponent] == '+' || code[exponent] == '-'))
                {
                    exponent++;
                }

                int digits = exponent;
                while (exponent < code.Length && char.IsDigit(code[exponent]))
                {
                    exponent++;
                }

                if (exponent > digits)
                {
                    index = exponent;
                }
            }

            while (index < code.Length && "uUlLfFdDmM".IndexOf(code[index]) >= 0)
            {
                index++;
            }

            return index - start;
        }

        private static int ReadIdentifier(string code, int start)
        {
            int index = start + 1;
            while (index < code.Length)
            {
                char c = code[index];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '@')
                {
                    index++;
                    continue;
                }

                break;
            }

            return index - start;
        }

        private static bool IsIdentifierStart(char c)
        {
            return char.IsLetter(c) || c == '_' || c == '$' || c == '@';
        }

        private static bool IsKeyword(LanguageDefinition definition, string token)
        {
            if (definition.IgnoreCase)
            {
                foreach (string keyword in definition.Keywords)
                {
                    if (string.Equals(keyword, token, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            return definition.Keywords.Contains(token);
        }

        private static bool StartsWith(string value, int index, string prefix)
        {
            if (index + prefix.Length > value.Length)
            {
                return false;
            }

            return string.CompareOrdinal(value, index, prefix, 0, prefix.Length) == 0;
        }

        private static void AppendSpan(StringBuilder builder, string cssClass, string value, int start, int length)
        {
            if (length <= 0)
            {
                return;
            }

            builder.Append("<span class=\"");
            builder.Append(cssClass);
            builder.Append("\">");
            AppendEscaped(builder, value, start, length);
            builder.Append("</span>");
        }

        private static void AppendEscaped(StringBuilder builder, string value, int start, int length)
        {
            int end = start + length;
            for (int i = start; i < end; i++)
            {
                AppendEscapedChar(builder, value[i]);
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
                case '\'':
                    builder.Append("&#39;");
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        private static string Escape(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length + 16);
            AppendEscaped(builder, value, 0, value.Length);
            return builder.ToString();
        }

        private static Dictionary<string, LanguageDefinition> CreateDefinitions()
        {
            Dictionary<string, LanguageDefinition> map =
                new Dictionary<string, LanguageDefinition>(StringComparer.OrdinalIgnoreCase);

            LanguageDefinition csharp = Make(
                "abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while async await var dynamic get set value yield partial where record init nameof",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, csharp, "csharp", "cs", "c#", "csx");

            LanguageDefinition javascript = Make(
                "break case catch class const continue debugger default delete do else export extends finally for function if import in instanceof let new return super switch this throw try typeof var void while with yield async await true false null undefined of static get set from as",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'', '`' });
            Add(map, javascript, "javascript", "js", "jsx", "mjs", "cjs");

            LanguageDefinition typescript = Make(
                "break case catch class const continue debugger default delete do else export extends finally for function if import in instanceof let new return super switch this throw try typeof var void while with yield async await true false null undefined of static get set from as interface type enum implements private public protected readonly declare namespace abstract any boolean number string symbol unknown never",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'', '`' });
            Add(map, typescript, "typescript", "ts", "tsx");

            LanguageDefinition java = Make(
                "abstract assert boolean break byte case catch char class const continue default do double else enum extends final finally float for goto if implements import instanceof int interface long native new package private protected public return short static strictfp super switch synchronized this throw throws transient try void volatile while true false null var record sealed permits",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, java, "java");

            LanguageDefinition kotlin = Make(
                "package import class interface fun object val var if else when for while do return break continue try catch finally throw is as in out by constructor companion data enum sealed override open private protected public internal lateinit this super true false null",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, kotlin, "kotlin", "kt", "kts");

            LanguageDefinition c = Make(
                "auto break case char const continue default do double else enum extern float for goto if inline int long register restrict return short signed sizeof static struct switch typedef union unsigned void volatile while true false",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, c, "c", "h");

            LanguageDefinition cpp = Make(
                "alignas alignof and and_eq asm auto bitand bitor bool break case catch char char16_t char32_t class compl const constexpr const_cast continue decltype default delete do double dynamic_cast else enum explicit export extern false float for friend goto if inline int long mutable namespace new noexcept not not_eq nullptr operator or or_eq private protected public register reinterpret_cast return short signed sizeof static static_assert static_cast struct switch template this throw true try typedef typeid typename union unsigned using virtual void volatile wchar_t while xor xor_eq",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, cpp, "cpp", "c++", "cc", "cxx", "hpp", "hh", "hxx");

            LanguageDefinition go = Make(
                "break case chan const continue default defer else fallthrough for func go goto if import interface map package range return select struct switch type var true false nil iota",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'', '`' });
            Add(map, go, "go", "golang");

            LanguageDefinition rust = Make(
                "as break const continue crate else enum extern false fn for if impl in let loop match mod move mut pub ref return self Self static struct super trait true type unsafe use where while async await dyn union",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, rust, "rust", "rs");

            LanguageDefinition swift = Make(
                "class deinit enum extension func import init inout internal let operator private protocol public static struct subscript typealias var break case continue default defer do else fallthrough for guard if in repeat return switch where while as catch false is nil rethrows super self Self throw throws true try associatedtype fileprivate open some any",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"' });
            Add(map, swift, "swift");

            LanguageDefinition php = Make(
                "abstract and array as break callable case catch class clone const continue declare default do echo else elseif empty enddeclare endfor endforeach endif endswitch endwhile eval exit extends final finally fn for foreach function global goto if implements include include_once instanceof insteadof interface isset list match namespace new or print private protected public readonly require require_once return static switch throw trait try unset use var while xor yield true false null",
                new string[] { "//", "#" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, php, "php");

            LanguageDefinition ruby = Make(
                "alias and begin break case class def defined do else elsif end ensure false for if in module next nil not or redo rescue retry return self super then true undef unless until when while yield require attr_accessor attr_reader attr_writer include extend",
                new string[] { "#" },
                new string[0],
                new string[0],
                new char[] { '"', '\'' });
            Add(map, ruby, "ruby", "rb");

            LanguageDefinition python = Make(
                "and as assert async await break class continue def del elif else except False finally for from global if import in is lambda None nonlocal not or pass raise return True try while with yield match case",
                new string[] { "#" },
                new string[0],
                new string[0],
                new char[] { '"', '\'' });
            python.TripleQuotes = true;
            Add(map, python, "python", "py", "pyw");

            LanguageDefinition shell = Make(
                "if then else elif fi for while in do done case esac function select until time coproc break continue return export local readonly declare typeset unset shift source alias echo printf cd exit test true false",
                new string[] { "#" },
                new string[0],
                new string[0],
                new char[] { '"', '\'', '`' });
            Add(map, shell, "bash", "sh", "shell", "zsh");

            LanguageDefinition powershell = Make(
                "begin break catch class continue data define do dynamicparam else elseif end enum exit filter finally for foreach from function if in param process return switch throw trap try until using var while workflow parallel sequence true false null",
                new string[] { "#" },
                new string[] { "<#" },
                new string[] { "#>" },
                new char[] { '"', '\'' });
            Add(map, powershell, "powershell", "ps1", "psm1");

            LanguageDefinition sql = Make(
                "select from where insert update delete create alter drop table view index join inner left right outer on group by order having union all distinct as and or not null is in between like limit offset values into set primary key foreign references default check unique constraint database grant revoke procedure function begin end declare if else case when then exists count sum avg min max top",
                new string[] { "--" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            sql.IgnoreCase = true;
            Add(map, sql, "sql");

            LanguageDefinition vb = Make(
                "addhandler addressof alias and andalso as boolean byref byte byval call case catch cbool cbyte cchar cdate cdbl cdec char cint class clng cobj csbyte cshort csng cstr ctype date decimal declare default delegate dim directcast do double each else elseif end enum erase error event exit false finally for friend function get gettype global goto if implements imports in inherits integer interface is isnot let lib like long loop me mod module mustinherit mustoverride mybase myclass namespace narrowing new next not nothing notinheritable notoverridable object of on operator option optional or orelse overloads overridable overrides paramarray partial private property protected public raiseevent readonly redim rem removehandler resume return sbyte select set shadows shared short single static step stop string structure sub synclock then throw true try trycast typeof uinteger ulong ushort using variant wend when while widen with withevents writeonly xor",
                new string[] { "'" },
                new string[0],
                new string[0],
                new char[] { '"' });
            vb.IgnoreCase = true;
            vb.DoubledQuotes = true;
            Add(map, vb, "vb", "vbnet", "visualbasic");

            LanguageDefinition fsharp = Make(
                "abstract and as assert base begin class default delegate do done downcast downto elif else end exception extern false finally fixed for fun function global if in inherit inline interface internal lazy let match member module mutable namespace new not null of open or override private public rec return select static struct then to true try type upcast use val void when while with yield",
                new string[] { "//" },
                new string[] { "(*" },
                new string[] { "*)" },
                new char[] { '"' });
            Add(map, fsharp, "fsharp", "f#", "fs");

            LanguageDefinition json = Make(
                "true false null",
                new string[0],
                new string[0],
                new string[0],
                new char[] { '"' });
            Add(map, json, "json", "jsonc");

            LanguageDefinition yaml = Make(
                "true false null yes no on off",
                new string[] { "#" },
                new string[0],
                new string[0],
                new char[] { '"', '\'' });
            Add(map, yaml, "yaml", "yml");

            LanguageDefinition toml = Make(
                "true false",
                new string[] { "#" },
                new string[0],
                new string[0],
                new char[] { '"', '\'' });
            Add(map, toml, "toml");

            LanguageDefinition ini = Make(
                "",
                new string[] { ";", "#" },
                new string[0],
                new string[0],
                new char[] { '"', '\'' });
            Add(map, ini, "ini");

            LanguageDefinition css = Make(
                "",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, css, "css", "scss", "sass", "less");

            LanguageDefinition lua = Make(
                "and break do else elseif end false for function goto if in local nil not or repeat return then true until while",
                new string[] { "--" },
                new string[] { "--[[" },
                new string[] { "]]" },
                new char[] { '"', '\'' });
            Add(map, lua, "lua");

            LanguageDefinition r = Make(
                "if else repeat while function for in next break TRUE FALSE NULL Inf NaN NA NA_integer_ NA_real_ NA_character_ library require",
                new string[] { "#" },
                new string[0],
                new string[0],
                new char[] { '"', '\'' });
            Add(map, r, "r");

            LanguageDefinition dart = Make(
                "abstract as assert async await break case catch class const continue covariant default deferred do dynamic else enum export extends extension external factory false final finally for Function get hide if implements import in interface is late library mixin new null on operator part required rethrow return set show static super switch sync this throw true try typedef var void while with yield",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, dart, "dart");

            LanguageDefinition groovy = Make(
                "abstract as assert boolean break byte case catch char class const continue def default do double else enum extends false final finally float for goto if implements import in instanceof int interface long native new null package private protected public return short static strictfp super switch synchronized this throw throws transient true try void volatile while trait",
                new string[] { "//" },
                new string[] { "/*" },
                new string[] { "*/" },
                new char[] { '"', '\'' });
            Add(map, groovy, "groovy", "gradle");

            LanguageDefinition perl = Make(
                "my our local sub if elsif else unless while until for foreach do last next redo return package use require BEGIN END and or not eq ne lt gt le ge cmp",
                new string[] { "#" },
                new string[0],
                new string[0],
                new char[] { '"', '\'' });
            Add(map, perl, "perl", "pl", "pm");

            LanguageDefinition bat = Make(
                "if else exist errorlevel goto call set echo rem for in do not defined off on enabledelayedexpansion",
                new string[] { "::", "REM ", "rem " },
                new string[0],
                new string[0],
                new char[] { '"' });
            bat.IgnoreCase = true;
            Add(map, bat, "bat", "batch", "cmd");

            LanguageDefinition markup = new LanguageDefinition();
            markup.Markup = true;
            Add(map, markup, "html", "htm", "xml", "xaml", "svg", "csproj", "vbproj", "props", "targets", "config", "xhtml");

            LanguageDefinition diff = new LanguageDefinition();
            diff.Diff = true;
            Add(map, diff, "diff", "patch");

            return map;
        }

        private static LanguageDefinition Make(
            string keywords,
            string[] lineComments,
            string[] blockOpen,
            string[] blockClose,
            char[] quotes)
        {
            LanguageDefinition definition = new LanguageDefinition();
            if (!string.IsNullOrEmpty(keywords))
            {
                string[] pieces = keywords.Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string piece in pieces)
                {
                    definition.Keywords.Add(piece);
                }
            }

            definition.LineComments = lineComments;
            definition.BlockCommentOpen = blockOpen;
            definition.BlockCommentClose = blockClose;
            definition.Quotes = quotes;
            return definition;
        }

        private static void Add(Dictionary<string, LanguageDefinition> map, LanguageDefinition definition, params string[] names)
        {
            foreach (string name in names)
            {
                map[name] = definition;
            }
        }
    }
}
