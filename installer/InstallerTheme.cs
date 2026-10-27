using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GetHub.Installer
{
    // Setup and uninstall share the app's dark / light palette. The palette is
    // read once from the Windows app theme so the installer matches the desktop
    // and still looks native next to GetHub itself.
    internal static class InstallerTheme
    {
        private static readonly bool Light = ReadLightPreference();
        private static Icon _icon;
        private static Image _logo;

        public static bool IsLight
        {
            get { return Light; }
        }

        public static Color Canvas
        {
            get { return Light ? Color.White : Hex("#0d1117"); }
        }

        public static Color Surface
        {
            get { return Light ? Hex("#f6f8fa") : Hex("#161b22"); }
        }

        public static Color Field
        {
            get { return Light ? Color.White : Hex("#010409"); }
        }

        public static Color Border
        {
            get { return Light ? Hex("#d0d7de") : Hex("#30363d"); }
        }

        public static Color Text
        {
            get { return Light ? Hex("#1f2328") : Hex("#e6edf3"); }
        }

        public static Color Muted
        {
            get { return Light ? Hex("#656d76") : Hex("#8b949e"); }
        }

        public static Color Faint
        {
            get { return Light ? Hex("#8c959f") : Hex("#6e7681"); }
        }

        public static Color Accent
        {
            get { return Hex("#2f81f7"); }
        }

        public static Color Green
        {
            get { return Hex("#238636"); }
        }

        public static Color Red
        {
            get { return Hex("#d1242f"); }
        }

        public static Color Warning
        {
            get { return Light ? Hex("#9a6700") : Hex("#d29922"); }
        }

        public static readonly Font BodyFont =
            new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font BodyBold =
            new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font SmallFont =
            new Font("Segoe UI", 8.25F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font TitleFont =
            new Font("Segoe UI Semibold", 17F, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font HeadingFont =
            new Font("Segoe UI Semibold", 15F, FontStyle.Regular, GraphicsUnit.Point);

        public static Icon AppIcon
        {
            get
            {
                if (_icon == null)
                {
                    try
                    {
                        _icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                    }
                    catch
                    {
                    }
                }

                return _icon;
            }
        }

        public static Image Logo
        {
            get
            {
                if (_logo == null)
                {
                    try
                    {
                        using (Stream stream = Assembly.GetExecutingAssembly()
                            .GetManifestResourceStream(InstallerInfo.LogoResource))
                        {
                            if (stream != null)
                            {
                                _logo = Image.FromStream(stream);
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                return _logo;
            }
        }

        public static Button MakeButton(string text, bool primary)
        {
            Button button = new Button();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.Font = BodyFont;
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.TabStop = true;
            button.Height = 34;
            button.Width = 100;

            if (primary)
            {
                button.BackColor = Green;
                button.ForeColor = Color.White;
                button.FlatAppearance.BorderColor = Hex("#2ea043");
                button.FlatAppearance.MouseOverBackColor = Hex("#2c974b");
                button.FlatAppearance.MouseDownBackColor = Green;
            }
            else
            {
                button.BackColor = Light ? Color.White : Surface;
                button.ForeColor = Text;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Light ? Hex("#f3f4f6") : Hex("#30363d");
                button.FlatAppearance.MouseDownBackColor = Light ? Hex("#eaeef2") : Hex("#282e33");
            }

            return button;
        }

        public static Button MakeDangerButton(string text)
        {
            Button button = MakeButton(text, false);
            button.ForeColor = Red;
            button.FlatAppearance.MouseOverBackColor =
                Light ? Hex("#ffebe9") : Hex("#f8514920");
            button.FlatAppearance.MouseDownBackColor =
                Light ? Hex("#ffcecb") : Hex("#f8514935");
            return button;
        }

        public static TextBox MakeTextBox()
        {
            TextBox box = new TextBox();
            box.Font = new Font("Segoe UI", 9.75F, FontStyle.Regular, GraphicsUnit.Point);
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = Field;
            box.ForeColor = Text;
            return box;
        }

        public static CheckBox MakeCheckBox(string text, bool isChecked)
        {
            CheckBox box = new CheckBox();
            box.Text = text;
            box.Checked = isChecked;
            box.Font = BodyFont;
            box.ForeColor = Text;
            box.BackColor = Color.Transparent;
            box.AutoSize = false;
            box.Height = 22;
            box.Cursor = Cursors.Hand;
            return box;
        }

        // Label.AutoSize measures a little narrower than the text it draws, which
        // clips the last characters. Sizing from TextRenderer avoids that.
        public static Label MakeLabel(string text, Font font, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = font;
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.AutoSize = true;
            return label;
        }

        public static void Fit(Label label)
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

        public static void DrawSeparator(Graphics graphics, int left, int right, int y)
        {
            using (Pen pen = new Pen(Border))
            {
                graphics.DrawLine(pen, left, y, right, y);
            }
        }

        public static Color Hex(string value)
        {
            return ColorTranslator.FromHtml(value);
        }

        private static bool ReadLightPreference()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    if (value is int)
                    {
                        return (int)value != 0;
                    }
                }
            }
            catch
            {
            }

            // GetHub's own default is dark, so the installer follows it.
            return false;
        }
    }
}
