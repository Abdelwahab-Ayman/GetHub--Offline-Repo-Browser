using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace GetHub.Installer
{
    // The setup window. One fixed-size page collects the install options, then
    // the body switches to a short success page. Buttons stay in the footer so
    // the two pages feel like one window.
    public sealed class SetupForm : Form
    {
        private TextBox _pathBox;
        private Button _browseButton;
        private CheckBox _desktopBox;
        private CheckBox _startMenuBox;
        private CheckBox _launchBox;
        private readonly Button _installButton;
        private readonly Button _cancelButton;
        private readonly Button _openFolderButton;
        private readonly Button _finishButton;
        private Label _statusLabel;
        private ProgressBar _progress;
        private readonly Panel _installPage;
        private readonly Panel _successPage;
        private Label _successTitle;
        private Label _successVersion;
        private Label _successPath;
        private Label _successWarnings;
        private CheckBadge _badge;
        private readonly ToolTip _pathTip;

        private bool _busy;
        private bool _installed;
        private bool _wasInstalled;
        private string _installedDirectory;
        private bool _launched;

        public SetupForm()
        {
            Text = "GetHub Setup";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(560, 512);
            BackColor = InstallerTheme.Canvas;
            ForeColor = InstallerTheme.Text;
            Font = InstallerTheme.BodyFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Icon = InstallerTheme.AppIcon;
            DoubleBuffered = true;
            SuspendLayout();

            Panel header = BuildHeader();

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 64;
            footer.BackColor = InstallerTheme.Surface;
            footer.Paint += FooterPaint;

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = InstallerTheme.Canvas;

            // Docking order matters: the filling body is added first so the
            // header and footer can claim their edges before it is measured.
            Controls.Add(body);
            Controls.Add(header);
            Controls.Add(footer);

            _pathTip = new ToolTip();

            _installPage = BuildInstallPage();
            _successPage = BuildSuccessPage();
            body.Controls.Add(_installPage);
            body.Controls.Add(_successPage);

            _installButton = InstallerTheme.MakeButton("Install", true);
            _installButton.Bounds = new Rectangle(436, 15, 100, 34);
            _installButton.Click += InstallClicked;
            footer.Controls.Add(_installButton);

            _cancelButton = InstallerTheme.MakeButton("Cancel", false);
            _cancelButton.Bounds = new Rectangle(330, 15, 94, 34);
            _cancelButton.Click += delegate { Close(); };
            footer.Controls.Add(_cancelButton);

            _openFolderButton = InstallerTheme.MakeButton("Open folder", false);
            _openFolderButton.Bounds = new Rectangle(304, 15, 120, 34);
            _openFolderButton.Visible = false;
            _openFolderButton.Click += delegate
            {
                InstallEngine.OpenFolder(_installedDirectory);
            };
            footer.Controls.Add(_openFolderButton);

            _finishButton = InstallerTheme.MakeButton("Finish", true);
            _finishButton.Bounds = new Rectangle(436, 15, 100, 34);
            _finishButton.Visible = false;
            _finishButton.Click += delegate { Close(); };
            footer.Controls.Add(_finishButton);

            AcceptButton = _installButton;
            CancelButton = _cancelButton;
            FormClosing += SetupFormClosing;
            Shown += delegate { _pathBox.Focus(); };

            ApplyInstalledState();
            ResumeLayout(false);
        }

        public int ExitCode { get; private set; }

        private Panel BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 92;
            header.BackColor = InstallerTheme.Surface;
            header.Paint += HeaderPaint;

            int textLeft = 80;
            Image logo = InstallerTheme.Logo;
            if (logo != null)
            {
                PictureBox badge = new PictureBox();
                badge.Image = logo;
                badge.SizeMode = PictureBoxSizeMode.Zoom;
                badge.BackColor = InstallerTheme.Surface;
                badge.Bounds = new Rectangle(24, 24, 44, 44);
                badge.TabStop = false;
                header.Controls.Add(badge);
            }
            else
            {
                textLeft = 24;
            }

            Label title = InstallerTheme.MakeLabel("GetHub", InstallerTheme.TitleFont, InstallerTheme.Text);
            title.Location = new Point(textLeft, 19);
            header.Controls.Add(title);
            InstallerTheme.Fit(title);

            Label subtitle = InstallerTheme.MakeLabel(
                "Setup - version " + InstallerInfo.Version,
                InstallerTheme.SmallFont,
                InstallerTheme.Muted);
            subtitle.Location = new Point(textLeft + 1, 50);
            header.Controls.Add(subtitle);
            InstallerTheme.Fit(subtitle);

            return header;
        }

        private Panel BuildInstallPage()
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = InstallerTheme.Canvas;
            page.Paint += InstallPagePaint;

            Label intro = new Label();
            intro.Text = "Installs GetHub for your Windows account so you can download repositories " +
                "and read them offline. No administrator rights are needed.";
            intro.Font = InstallerTheme.BodyFont;
            intro.ForeColor = InstallerTheme.Muted;
            intro.AutoSize = false;
            intro.Bounds = new Rectangle(24, 20, 512, 40);
            page.Controls.Add(intro);

            Label locationLabel = InstallerTheme.MakeLabel(
                "Install location",
                InstallerTheme.BodyBold,
                InstallerTheme.Text);
            locationLabel.Location = new Point(24, 72);
            page.Controls.Add(locationLabel);
            InstallerTheme.Fit(locationLabel);

            _pathBox = InstallerTheme.MakeTextBox();
            _pathBox.MaxLength = 240;
            _pathBox.Bounds = new Rectangle(
                24,
                92 + (32 - _pathBox.PreferredHeight) / 2,
                400,
                _pathBox.PreferredHeight);
            page.Controls.Add(_pathBox);

            _browseButton = InstallerTheme.MakeButton("Browse...", false);
            _browseButton.Bounds = new Rectangle(434, 92, 102, 32);
            _browseButton.Click += BrowseClicked;
            page.Controls.Add(_browseButton);

            Label hint = new Label();
            hint.Text = "Settings and downloaded repositories are kept when you update.";
            hint.Font = InstallerTheme.SmallFont;
            hint.ForeColor = InstallerTheme.Faint;
            hint.AutoSize = false;
            hint.Bounds = new Rectangle(24, 134, 512, 18);
            page.Controls.Add(hint);

            _desktopBox = InstallerTheme.MakeCheckBox("Create a desktop shortcut", true);
            _desktopBox.Bounds = new Rectangle(24, 176, 512, 22);
            page.Controls.Add(_desktopBox);

            _startMenuBox = InstallerTheme.MakeCheckBox("Add GetHub to the Start menu", true);
            _startMenuBox.Bounds = new Rectangle(24, 202, 512, 22);
            page.Controls.Add(_startMenuBox);

            _launchBox = InstallerTheme.MakeCheckBox("Launch GetHub when setup finishes", true);
            _launchBox.Bounds = new Rectangle(24, 228, 512, 22);
            page.Controls.Add(_launchBox);

            _statusLabel = new Label();
            _statusLabel.Text = string.Empty;
            _statusLabel.Font = InstallerTheme.SmallFont;
            _statusLabel.ForeColor = InstallerTheme.Muted;
            _statusLabel.AutoSize = false;
            _statusLabel.Bounds = new Rectangle(24, 258, 512, 38);
            page.Controls.Add(_statusLabel);

            _progress = new ProgressBar();
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.MarqueeAnimationSpeed = 30;
            _progress.Bounds = new Rectangle(24, 306, 512, 10);
            _progress.Visible = false;
            page.Controls.Add(_progress);

            return page;
        }

        private Panel BuildSuccessPage()
        {
            Panel page = new Panel();
            page.Dock = DockStyle.Fill;
            page.BackColor = InstallerTheme.Canvas;
            page.Visible = false;
            page.Paint += SuccessPagePaint;

            _badge = new CheckBadge();
            _badge.Bounds = new Rectangle(24, 24, 48, 48);
            page.Controls.Add(_badge);

            _successTitle = InstallerTheme.MakeLabel(
                "GetHub is installed",
                InstallerTheme.HeadingFont,
                InstallerTheme.Text);
            _successTitle.Location = new Point(88, 24);
            page.Controls.Add(_successTitle);
            InstallerTheme.Fit(_successTitle);

            _successVersion = InstallerTheme.MakeLabel(
                "Version " + InstallerInfo.Version + " for your Windows account",
                InstallerTheme.SmallFont,
                InstallerTheme.Muted);
            _successVersion.Location = new Point(89, 55);
            page.Controls.Add(_successVersion);
            InstallerTheme.Fit(_successVersion);

            Label locationLabel = InstallerTheme.MakeLabel(
                "Install location",
                InstallerTheme.BodyBold,
                InstallerTheme.Text);
            locationLabel.Location = new Point(24, 122);
            page.Controls.Add(locationLabel);
            InstallerTheme.Fit(locationLabel);

            _successPath = new Label();
            _successPath.Text = string.Empty;
            _successPath.Font = InstallerTheme.SmallFont;
            _successPath.ForeColor = InstallerTheme.Muted;
            _successPath.AutoSize = false;
            _successPath.Bounds = new Rectangle(24, 146, 512, 36);
            page.Controls.Add(_successPath);

            Label note = new Label();
            note.Text = "Your settings, GitHub sign-in and downloaded repositories are kept in " +
                InstallerInfo.DataDirectory + ".";
            note.Font = InstallerTheme.SmallFont;
            note.ForeColor = InstallerTheme.Muted;
            note.AutoSize = false;
            note.Bounds = new Rectangle(24, 192, 512, 36);
            page.Controls.Add(note);

            Label hint = new Label();
            hint.Text = "Remove GetHub any time from Settings > Apps.";
            hint.Font = InstallerTheme.SmallFont;
            hint.ForeColor = InstallerTheme.Faint;
            hint.AutoSize = false;
            hint.Bounds = new Rectangle(24, 238, 512, 18);
            page.Controls.Add(hint);

            _successWarnings = new Label();
            _successWarnings.Text = string.Empty;
            _successWarnings.Font = InstallerTheme.SmallFont;
            _successWarnings.ForeColor = InstallerTheme.Warning;
            _successWarnings.AutoSize = false;
            _successWarnings.Bounds = new Rectangle(24, 262, 512, 48);
            _successWarnings.Visible = false;
            page.Controls.Add(_successWarnings);

            return page;
        }

        private void ApplyInstalledState()
        {
            string existing = InstallerInfo.FindInstalledDirectory();
            _wasInstalled = existing != null;
            _installed = _wasInstalled;
            _pathBox.Text = existing != null ? existing : InstallerInfo.DefaultInstallDirectory;

            if (existing != null)
            {
                InstallManifest manifest = InstallManifest.Load(InstallerInfo.ManifestPath(existing));
                if (manifest != null)
                {
                    _desktopBox.Checked = manifest.HasShortcutNamed(InstallerInfo.ProductName + ".lnk")
                        && File.Exists(Shortcut.DesktopPath());
                    _startMenuBox.Checked = File.Exists(Shortcut.StartMenuPath());
                }

                _installButton.Text = "Update";
                _statusLabel.Text = "GetHub " + InstallerInfo.Version +
                    " is already installed. Setup replaces the program files and keeps your " +
                    "settings and downloads.";
                _statusLabel.ForeColor = InstallerTheme.Muted;
            }
        }

        private void BrowseClicked(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Choose the folder where GetHub should be installed";
                dialog.ShowNewFolderButton = true;
                string current = InstallerInfo.Normalize(_pathBox.Text);
                if (Directory.Exists(current))
                {
                    dialog.SelectedPath = current;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    _pathBox.Text = dialog.SelectedPath;
                }
            }
        }

        private async void InstallClicked(object sender, EventArgs e)
        {
            if (_busy)
            {
                return;
            }

            string error;
            if (!InstallerInfo.ValidateDirectory(_pathBox.Text, out error))
            {
                ShowStatus(error, true);
                _pathBox.Focus();
                return;
            }

            string directory = InstallerInfo.Normalize(_pathBox.Text);
            if (!RunningApp.CloseAll(this, true))
            {
                return;
            }

            _wasInstalled = _wasInstalled || InstallerInfo.FindInstalledDirectory() != null;
            SetBusy(true);
            ShowStatus("Installing GetHub...", false);

            try
            {
                InstallOptions options = new InstallOptions();
                options.Directory = directory;
                options.DesktopShortcut = _desktopBox.Checked;
                options.StartMenuShortcut = _startMenuBox.Checked;

                InstallResult result = await Task.Run(delegate
                {
                    return InstallEngine.Install(options);
                });

                if (IsDisposed)
                {
                    return;
                }

                SetBusy(false);
                _installedDirectory = result.InstallDirectory;
                _installed = true;
                _launched = false;
                ShowSuccess(result);
            }
            catch (Exception ex)
            {
                if (IsDisposed)
                {
                    return;
                }

                SetBusy(false);
                ShowStatus(Describe(ex), true);
                ExitCode = 1;
            }
        }

        private void ShowSuccess(InstallResult result)
        {
            _successTitle.Text = _wasInstalled ? "GetHub is updated" : "GetHub is installed";
            InstallerTheme.Fit(_successTitle);
            _successPath.Text = result.InstallDirectory;
            _pathTip.SetToolTip(_successPath, result.InstallDirectory);

            if (result.Warnings.Count > 0)
            {
                _successWarnings.Text = string.Join("\r\n", result.Warnings.ToArray());
                _successWarnings.Visible = true;
            }

            _installPage.Visible = false;
            _successPage.Visible = true;
            _successPage.BringToFront();

            _installButton.Visible = false;
            _cancelButton.Visible = false;
            _openFolderButton.Visible = true;
            _finishButton.Visible = true;
            AcceptButton = _finishButton;
            CancelButton = null;
            _finishButton.Focus();
            ExitCode = 0;
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _pathBox.Enabled = !busy;
            _browseButton.Enabled = !busy;
            _desktopBox.Enabled = !busy;
            _startMenuBox.Enabled = !busy;
            _launchBox.Enabled = !busy;
            _installButton.Enabled = !busy;
            _cancelButton.Enabled = !busy;
            _progress.Visible = busy;
            _installButton.Text = busy
                ? "Installing..."
                : (_installed ? "Update" : "Install");
            UseWaitCursor = busy;
        }

        private void ShowStatus(string text, bool isError)
        {
            _statusLabel.Text = text ?? string.Empty;
            _statusLabel.ForeColor = isError ? InstallerTheme.Red : InstallerTheme.Muted;
        }

        private void SetupFormClosing(object sender, FormClosingEventArgs e)
        {
            if (_busy && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                return;
            }

            if (_installed && !_launched && _launchBox.Checked)
            {
                _launched = true;
                InstallEngine.Launch(_installedDirectory);
            }
        }

        private static string Describe(Exception exception)
        {
            if (exception == null)
            {
                return "Setup could not install GetHub.";
            }

            if (exception is InvalidOperationException)
            {
                return exception.Message;
            }

            return "Setup could not install GetHub: " + exception.Message;
        }

        private void HeaderPaint(object sender, PaintEventArgs e)
        {
            InstallerTheme.DrawSeparator(
                e.Graphics,
                0,
                ((Control)sender).ClientSize.Width,
                ((Control)sender).ClientSize.Height - 1);
        }

        private void FooterPaint(object sender, PaintEventArgs e)
        {
            InstallerTheme.DrawSeparator(e.Graphics, 0, ((Control)sender).ClientSize.Width, 0);
        }

        private void InstallPagePaint(object sender, PaintEventArgs e)
        {
            InstallerTheme.DrawSeparator(
                e.Graphics,
                24,
                ((Control)sender).ClientSize.Width - 24,
                161);
        }

        private void SuccessPagePaint(object sender, PaintEventArgs e)
        {
            InstallerTheme.DrawSeparator(
                e.Graphics,
                24,
                ((Control)sender).ClientSize.Width - 24,
                104);
        }
    }
}
