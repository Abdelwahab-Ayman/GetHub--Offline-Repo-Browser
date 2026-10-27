using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace GetHub.Installer
{
    // Small confirmation window for Uninstall.exe. It lives in the install folder
    // so Windows can launch it from Settings > Apps, and it offers to keep the
    // offline repository cache and GitHub sign-in.
    public sealed class UninstallForm : Form
    {
        private readonly string _directory;
        private readonly CheckBox _removeData;
        private readonly Label _dataWarning;
        private readonly Label _status;
        private readonly Button _uninstallButton;
        private readonly Button _cancelButton;
        private bool _busy;

        public UninstallForm(string directory)
        {
            _directory = InstallerInfo.Normalize(directory);
            Text = "Uninstall GetHub";
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(480, 330);
            BackColor = InstallerTheme.Canvas;
            ForeColor = InstallerTheme.Text;
            Font = InstallerTheme.BodyFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            Icon = InstallerTheme.AppIcon;
            DoubleBuffered = true;
            SuspendLayout();

            Panel body = new Panel();
            body.Dock = DockStyle.Fill;
            body.BackColor = InstallerTheme.Canvas;
            Controls.Add(body);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Bottom;
            footer.Height = 64;
            footer.BackColor = InstallerTheme.Surface;
            footer.Paint += delegate(object sender, PaintEventArgs e)
            {
                InstallerTheme.DrawSeparator(e.Graphics, 0, ((Control)sender).ClientSize.Width, 0);
            };
            Controls.Add(footer);

            Label title = InstallerTheme.MakeLabel(
                "Uninstall GetHub",
                InstallerTheme.HeadingFont,
                InstallerTheme.Text);
            title.Location = new Point(24, 20);
            body.Controls.Add(title);
            InstallerTheme.Fit(title);

            Label intro = new Label();
            intro.Text = "This removes GetHub from this Windows account. Your settings, GitHub " +
                "sign-in and downloaded repositories are kept unless you delete them below.";
            intro.Font = InstallerTheme.BodyFont;
            intro.ForeColor = InstallerTheme.Muted;
            intro.AutoSize = false;
            intro.Bounds = new Rectangle(24, 60, 432, 52);
            body.Controls.Add(intro);

            Label location = new Label();
            location.Text = _directory;
            location.Font = InstallerTheme.SmallFont;
            location.ForeColor = InstallerTheme.Faint;
            location.AutoSize = false;
            location.AutoEllipsis = true;
            location.Bounds = new Rectangle(24, 120, 432, 18);
            body.Controls.Add(location);

            _removeData = InstallerTheme.MakeCheckBox(
                "Also delete my settings and downloaded repositories",
                false);
            _removeData.Bounds = new Rectangle(24, 150, 432, 24);
            _removeData.CheckedChanged += delegate
            {
                _dataWarning.Visible = _removeData.Checked;
            };
            body.Controls.Add(_removeData);

            _dataWarning = new Label();
            _dataWarning.Text = "This deletes " + InstallerInfo.DataDirectory +
                " and cannot be undone.";
            _dataWarning.Font = InstallerTheme.SmallFont;
            _dataWarning.ForeColor = InstallerTheme.Warning;
            _dataWarning.AutoSize = false;
            _dataWarning.Bounds = new Rectangle(24, 178, 432, 32);
            _dataWarning.Visible = false;
            body.Controls.Add(_dataWarning);

            _status = new Label();
            _status.Text = string.Empty;
            _status.Font = InstallerTheme.SmallFont;
            _status.ForeColor = InstallerTheme.Muted;
            _status.AutoSize = false;
            _status.Bounds = new Rectangle(24, 218, 432, 22);
            body.Controls.Add(_status);

            _cancelButton = InstallerTheme.MakeButton("Cancel", false);
            _cancelButton.Bounds = new Rectangle(252, 15, 96, 34);
            _cancelButton.Click += delegate { Close(); };
            footer.Controls.Add(_cancelButton);

            _uninstallButton = InstallerTheme.MakeDangerButton("Uninstall");
            _uninstallButton.Bounds = new Rectangle(356, 15, 100, 34);
            _uninstallButton.Click += UninstallClicked;
            footer.Controls.Add(_uninstallButton);

            AcceptButton = _uninstallButton;
            CancelButton = _cancelButton;

            if (!UninstallEngine.Exists(_directory))
            {
                _uninstallButton.Enabled = false;
                _status.ForeColor = InstallerTheme.Red;
                _status.Text = "GetHub was not found in this folder.";
            }

            ResumeLayout(false);
        }

        public int ExitCode { get; private set; }

        private void UninstallClicked(object sender, EventArgs e)
        {
            if (_busy)
            {
                return;
            }

            _busy = true;
            SetBusy(true);
            _status.ForeColor = InstallerTheme.Muted;
            _status.Text = "Removing GetHub...";
            Application.DoEvents();

            UninstallResult result = UninstallEngine.Run(_directory, _removeData.Checked, this, true);
            if (!result.Removed)
            {
                _busy = false;
                SetBusy(false);
                _status.ForeColor = InstallerTheme.Red;
                _status.Text = result.Warnings.Count > 0
                    ? result.Warnings[0]
                    : "Uninstall was cancelled.";
                return;
            }

            if (result.Warnings.Count > 0)
            {
                MessageBox.Show(
                    this,
                    "GetHub was removed, but some items need attention:\r\n\r\n" +
                    string.Join("\r\n", result.Warnings.ToArray()),
                    "Uninstall GetHub",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            ExitCode = 0;
            Close();
        }

        private void SetBusy(bool busy)
        {
            _uninstallButton.Enabled = !busy;
            _cancelButton.Enabled = !busy;
            _removeData.Enabled = !busy;
            UseWaitCursor = busy;
        }
    }
}
