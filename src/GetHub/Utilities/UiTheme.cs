using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GetHub.Utilities
{
    public enum ButtonStyle
    {
        Primary,
        Secondary,
        DarkSecondary,
        Danger
    }

    public enum AppTheme
    {
        Dark,
        Light
    }

    public static class UiTheme
    {
        private static AppTheme _currentTheme = AppTheme.Dark;

        public static AppTheme CurrentTheme
        {
            get { return _currentTheme; }
            set { _currentTheme = value; }
        }

        public static bool IsLight
        {
            get { return _currentTheme == AppTheme.Light; }
        }

        public static Color Canvas
        {
            get { return IsLight ? Color.White : ColorTranslator.FromHtml("#0d1117"); }
        }

        public static Color Surface
        {
            get { return IsLight ? ColorTranslator.FromHtml("#f6f8fa") : ColorTranslator.FromHtml("#161b22"); }
        }

        public static Color Border
        {
            get { return IsLight ? ColorTranslator.FromHtml("#d0d7de") : ColorTranslator.FromHtml("#30363d"); }
        }

        public static Color Text
        {
            get { return IsLight ? ColorTranslator.FromHtml("#1f2328") : ColorTranslator.FromHtml("#e6edf3"); }
        }

        public static Color Muted
        {
            get { return IsLight ? ColorTranslator.FromHtml("#656d76") : ColorTranslator.FromHtml("#8b949e"); }
        }

        public static Color Faint
        {
            get { return IsLight ? ColorTranslator.FromHtml("#8c959f") : ColorTranslator.FromHtml("#6e7681"); }
        }

        public static Color Accent
        {
            get { return ColorTranslator.FromHtml("#2f81f7"); }
        }

        public static Color Green
        {
            get { return ColorTranslator.FromHtml("#238636"); }
        }

        public static Color Red
        {
            get { return ColorTranslator.FromHtml("#d1242f"); }
        }

        public static Color DarkBar
        {
            get { return IsLight ? ColorTranslator.FromHtml("#f6f8fa") : ColorTranslator.FromHtml("#010409"); }
        }

        public static Color DarkBarText
        {
            get { return IsLight ? ColorTranslator.FromHtml("#1f2328") : ColorTranslator.FromHtml("#e6edf3"); }
        }

        public static Color DarkBarMuted
        {
            get { return IsLight ? ColorTranslator.FromHtml("#656d76") : ColorTranslator.FromHtml("#8b949e"); }
        }

        public static Color Selection
        {
            get { return IsLight ? ColorTranslator.FromHtml("#0969da26") : ColorTranslator.FromHtml("#1f6feb33"); }
        }

        public static Color ChipBackground
        {
            get { return IsLight ? ColorTranslator.FromHtml("#ddf4ff") : ColorTranslator.FromHtml("#121d2f"); }
        }

        public static Color HoverBackground
        {
            get { return IsLight ? ColorTranslator.FromHtml("#f3f4f6") : ColorTranslator.FromHtml("#21262d"); }
        }

        // Hover/press colours for buttons that sit directly on the DarkBar surface.
        // In light mode the bar is near-white (#f6f8fa) so we need a clearly darker step;
        // in dark mode the same HoverBackground shade works fine.
        public static Color DarkBarHover
        {
            get { return IsLight ? ColorTranslator.FromHtml("#e8ebee") : ColorTranslator.FromHtml("#21262d"); }
        }

        public static Color DarkBarPress
        {
            get { return IsLight ? ColorTranslator.FromHtml("#d8dde3") : ColorTranslator.FromHtml("#30363d"); }
        }

        public static readonly Font BodyFont = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font BodyBold = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font SmallFont = new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font HeadingFont = new Font("Segoe UI Semibold", 17F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font CardTitleFont = new Font("Segoe UI Semibold", 10F, FontStyle.Regular, GraphicsUnit.Point);

        public static Button MakeButton(string text, ButtonStyle style)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Font = BodyFont;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.TabStop = true;
            button.Height = 32;

            switch (style)
            {
                case ButtonStyle.Primary:
                    button.BackColor = Green;
                    button.ForeColor = Color.White;
                    button.FlatAppearance.BorderColor = ColorTranslator.FromHtml("#2ea043");
                    button.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#2c974b");
                    button.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#238636");
                    break;
                case ButtonStyle.DarkSecondary:
                    if (IsLight)
                    {
                        button.BackColor = Color.White;
                        button.ForeColor = Text;
                        button.FlatAppearance.BorderColor = Border;
                        button.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#f3f4f6");
                        button.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#eaeef2");
                    }
                    else
                    {
                        button.BackColor = Surface;
                        button.ForeColor = Text;
                        button.FlatAppearance.BorderColor = Border;
                        button.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#30363d");
                        button.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#282e33");
                    }
                    break;
                case ButtonStyle.Danger:
                    button.BackColor = IsLight ? Color.White : Surface;
                    button.ForeColor = Red;
                    button.FlatAppearance.BorderColor = Border;
                    button.FlatAppearance.MouseOverBackColor = IsLight ? ColorTranslator.FromHtml("#ffebe9") : ColorTranslator.FromHtml("#f8514920");
                    button.FlatAppearance.MouseDownBackColor = IsLight ? ColorTranslator.FromHtml("#ffcecb") : ColorTranslator.FromHtml("#f8514935");
                    break;
                default:
                    if (IsLight)
                    {
                        button.BackColor = Color.White;
                        button.ForeColor = Text;
                        button.FlatAppearance.BorderColor = Border;
                        button.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#f3f4f6");
                        button.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#eaeef2");
                    }
                    else
                    {
                        button.BackColor = Surface;
                        button.ForeColor = Text;
                        button.FlatAppearance.BorderColor = Border;
                        button.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#30363d");
                        button.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#282e33");
                    }
                    break;
            }

            return button;
        }

        // Creates a secondary-style button that blends into the top bar (DarkBar) background
        // on both dark and light themes. The background matches DarkBar exactly and hover/press
        // colours are legible against it on either theme.
        public static Button MakeDarkBarButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = IsLight
                ? ColorTranslator.FromHtml("#d0d7de")
                : ColorTranslator.FromHtml("#30363d");
            button.Font = BodyFont;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.TabStop = true;
            button.Height = 32;
            button.BackColor = DarkBar;
            button.ForeColor = DarkBarText;
            button.FlatAppearance.MouseOverBackColor = DarkBarHover;
            button.FlatAppearance.MouseDownBackColor = DarkBarPress;
            return button;
        }

        public static Label MakeLabel(string text, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.AutoSize = true;
            label.BackColor = Color.Transparent;
            return label;
        }

        // Label.AutoSize measures a few pixels narrower than the text it actually draws
        // (measured 174px for text that needs 181px), which clips the last characters.
        // Sizing from TextRenderer keeps every label fully readable.
        public static void FitLabel(Label label)
        {
            if (label == null || label.Text == null)
            {
                return;
            }

            int width = TextRenderer.MeasureText(
                label.Text,
                label.Font,
                new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.SingleLine).Width;
            int height = Math.Max(label.Font.Height + 2, label.PreferredHeight);
            label.AutoSize = false;
            label.Width = width + 2;
            label.Height = height;
        }

        public static void SetCueBanner(TextBox box, string text)
        {
            try
            {
                SendMessage(box.Handle, 0x1501, IntPtr.Zero, text);
            }
            catch
            {
            }
        }

        public static void OpenFolder(string path)
        {
            try
            {
                if (System.IO.Directory.Exists(path))
                {
                    System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
                }
            }
            catch
            {
            }
        }

        public static void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }

        public static void OpenFile(string path)
        {
            try
            {
                if (System.IO.File.Exists(path))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
            }
            catch
            {
            }
        }

        public static string Ellipsize(Graphics graphics, string text, Font font, int width)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            if (TextRenderer.MeasureText(graphics, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width <= width)
            {
                return text;
            }

            string value = text;
            while (value.Length > 1)
            {
                value = value.Substring(0, value.Length - 1);
                if (TextRenderer.MeasureText(graphics, value + "...", font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding).Width <= width)
                {
                    return value + "...";
                }
            }

            return "...";
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);
    }
}
