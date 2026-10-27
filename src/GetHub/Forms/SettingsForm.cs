using System;
using System.Drawing;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Forms
{
    public class SettingsForm : Form
    {
        private readonly RepoStore _store;
        private readonly ComboBox _themeBox;
        private readonly Label _accountStatus;
        private readonly Label _accountDetail;
        private readonly Button _signInButton;
        private readonly Button _signOutButton;
        private readonly LinkLabel _profileLink;

        public SettingsForm(RepoStore store)
        {
            _store = store;
            Text = "Settings";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 448);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Icon = AppAssets.AppIcon;
            BackColor = UiTheme.Surface;
            Font = UiTheme.BodyFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            SuspendLayout();

            Label title = new Label();
            title.Text = "Settings";
            title.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Regular);
            title.ForeColor = UiTheme.Text;
            title.AutoSize = true;
            title.Location = new Point(26, 20);
            Controls.Add(title);
            UiTheme.FitLabel(title);

            Label themeLabel = new Label();
            themeLabel.Text = "Theme";
            themeLabel.Font = UiTheme.BodyBold;
            themeLabel.ForeColor = UiTheme.Text;
            themeLabel.AutoSize = true;
            themeLabel.Location = new Point(26, 68);
            Controls.Add(themeLabel);
            UiTheme.FitLabel(themeLabel);

            _themeBox = new ComboBox();
            _themeBox.Location = new Point(26, 88);
            _themeBox.Size = new Size(200, 24);
            _themeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _themeBox.Items.Add("Dark");
            _themeBox.Items.Add("Light");
            string currentTheme = store.Settings != null && string.Equals(store.Settings.Theme, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
            _themeBox.SelectedItem = currentTheme;
            Controls.Add(_themeBox);

            Label accountLabel = new Label();
            accountLabel.Text = "GitHub account";
            accountLabel.Font = UiTheme.BodyBold;
            accountLabel.ForeColor = UiTheme.Text;
            accountLabel.AutoSize = true;
            accountLabel.Location = new Point(26, 136);
            Controls.Add(accountLabel);
            UiTheme.FitLabel(accountLabel);

            _accountStatus = new Label();
            _accountStatus.Font = UiTheme.BodyBold;
            _accountStatus.AutoSize = false;
            _accountStatus.Location = new Point(26, 160);
            _accountStatus.Size = new Size(508, 20);
            _accountStatus.TextAlign = ContentAlignment.MiddleLeft;
            _accountStatus.AutoEllipsis = true;
            Controls.Add(_accountStatus);

            _accountDetail = new Label();
            _accountDetail.Font = UiTheme.SmallFont;
            _accountDetail.ForeColor = UiTheme.Muted;
            _accountDetail.AutoSize = false;
            _accountDetail.Location = new Point(26, 182);
            _accountDetail.Size = new Size(508, 46);
            Controls.Add(_accountDetail);

            _signInButton = UiTheme.MakeButton("Sign in...", ButtonStyle.Primary);
            _signInButton.Location = new Point(26, 234);
            _signInButton.Size = new Size(112, 30);
            _signInButton.Click += SignInClicked;
            Controls.Add(_signInButton);

            _signOutButton = UiTheme.MakeButton("Sign out", ButtonStyle.Danger);
            _signOutButton.Location = new Point(26, 234);
            _signOutButton.Size = new Size(96, 30);
            _signOutButton.Click += SignOutClicked;
            Controls.Add(_signOutButton);

            _profileLink = new LinkLabel();
            _profileLink.Text = "Open profile on GitHub";
            _profileLink.Font = UiTheme.SmallFont;
            _profileLink.LinkColor = UiTheme.Accent;
            _profileLink.ActiveLinkColor = UiTheme.Accent;
            _profileLink.AutoSize = true;
            _profileLink.Location = new Point(134, 241);
            _profileLink.LinkClicked += delegate
            {
                GitHubAccount account = _store.Settings.GetAccount();
                if (!string.IsNullOrEmpty(account.HtmlUrl))
                {
                    UiTheme.OpenUrl(account.HtmlUrl);
                }
            };
            Controls.Add(_profileLink);
            UiTheme.FitLabel(_profileLink);

            Label hint = new Label();
            hint.Text = "Signing in lets GetHub show issues, comments and project boards in this window. " +
                "A token with the repo and read:project scopes is enough; it is stored in your Windows profile.";
            hint.Font = UiTheme.SmallFont;
            hint.ForeColor = UiTheme.Faint;
            hint.AutoSize = false;
            hint.Location = new Point(26, 274);
            hint.Size = new Size(508, 36);
            Controls.Add(hint);

            Label dataLabel = new Label();
            dataLabel.Text = "Local data";
            dataLabel.Font = UiTheme.BodyBold;
            dataLabel.ForeColor = UiTheme.Text;
            dataLabel.AutoSize = true;
            dataLabel.Location = new Point(26, 330);
            Controls.Add(dataLabel);
            UiTheme.FitLabel(dataLabel);

            TextBox dataBox = new TextBox();
            dataBox.Location = new Point(26, 352);
            dataBox.Size = new Size(390, 24);
            dataBox.Font = UiTheme.SmallFont;
            dataBox.BorderStyle = BorderStyle.FixedSingle;
            dataBox.ReadOnly = true;
            dataBox.BackColor = UiTheme.Canvas;
            dataBox.Text = store.RootPath;
            Controls.Add(dataBox);

            Button openFolder = UiTheme.MakeButton("Open folder", ButtonStyle.Secondary);
            openFolder.Location = new Point(424, 350);
            openFolder.Size = new Size(110, 28);
            openFolder.Click += delegate { UiTheme.OpenFolder(_store.RootPath); };
            Controls.Add(openFolder);

            Button cancel = UiTheme.MakeButton("Cancel", ButtonStyle.Secondary);
            cancel.Location = new Point(368, 392);
            cancel.Size = new Size(80, 32);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            Button save = UiTheme.MakeButton("Save", ButtonStyle.Primary);
            save.Location = new Point(458, 392);
            save.Size = new Size(76, 32);
            save.Click += SaveClicked;
            Controls.Add(save);

            AcceptButton = save;
            CancelButton = cancel;
            Shown += delegate { _themeBox.Focus(); };
            Paint += FormPaint;

            UpdateAccount();
            ResumeLayout(false);
        }

        // True when the dialog signed in, signed out, or replaced the token.
        public bool AccountChanged { get; private set; }

        private void SignInClicked(object sender, EventArgs e)
        {
            using (SignInForm dialog = new SignInForm(_store))
            {
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    AccountChanged = true;
                    UpdateAccount();
                }
            }
        }

        private void SignOutClicked(object sender, EventArgs e)
        {
            GitHubAccount account = _store.Settings.GetAccount();
            string name = account.IsAuthenticated ? "@" + account.Login : "this account";
            DialogResult answer = MessageBox.Show(
                this,
                "Sign out of " + name + "?\n\nDownloaded issues and project boards stay on this computer for offline reading.",
                "Sign out",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2);
            if (answer != DialogResult.Yes)
            {
                return;
            }

            _store.Settings.ClearAccount();
            try
            {
                _store.SaveSettings();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "The account could not be signed out.\n\n" + ex.Message,
                    "Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return;
            }

            AccountChanged = true;
            UpdateAccount();
        }

        private void UpdateAccount()
        {
            GitHubAccount account = _store.Settings.GetAccount();
            bool signedIn = account.IsAuthenticated || _store.Settings.IsSignedIn;
            if (signedIn && account.IsAuthenticated)
            {
                _accountStatus.Text = "Signed in as " + account.DisplayName + " (@" + account.Login + ")";
                _accountStatus.ForeColor = UiTheme.Green;
                string scopes = _store.Settings.GitHubScopes;
                _accountDetail.Text = string.IsNullOrEmpty(scopes)
                    ? "GitHub did not report classic scopes for this token. If it is fine-grained, make sure it can read issues and projects."
                    : "Token scopes: " + scopes + ". Issues and project boards are cached for offline reading.";
                _signInButton.Visible = false;
                _signOutButton.Visible = true;
                _profileLink.Visible = !string.IsNullOrEmpty(account.HtmlUrl);
            }
            else if (signedIn)
            {
                _accountStatus.Text = "Signed in (account details not refreshed yet)";
                _accountStatus.ForeColor = UiTheme.Green;
                _accountDetail.Text = "Choose Sign in and verify the token again to refresh the account name and scopes.";
                _signInButton.Visible = true;
                _signInButton.Text = "Verify token...";
                _signOutButton.Visible = true;
                _profileLink.Visible = false;
            }
            else
            {
                _accountStatus.Text = "Not signed in";
                _accountStatus.ForeColor = UiTheme.Muted;
                _accountDetail.Text = "Sign in to read this repository's issues, comments and project boards inside GetHub. " +
                    "Cached copies remain available without an account.";
                _signInButton.Visible = true;
                _signInButton.Text = "Sign in...";
                _signOutButton.Visible = false;
                _profileLink.Visible = false;
            }
        }

        private void SaveClicked(object sender, EventArgs e)
        {
            if (_themeBox != null && _themeBox.SelectedItem != null)
            {
                _store.Settings.Theme = _themeBox.SelectedItem.ToString();
            }
            else if (_store.Settings.Theme == null)
            {
                _store.Settings.Theme = "Dark";
            }

            try
            {
                UiTheme.CurrentTheme = string.Equals(_store.Settings.Theme, "Light", StringComparison.OrdinalIgnoreCase)
                    ? AppTheme.Light
                    : AppTheme.Dark;
                _store.SaveSettings();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    "The settings could not be saved.\n\n" + ex.Message,
                    "Settings",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void FormPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(UiTheme.Border))
            {
                e.Graphics.DrawLine(pen, 26, 124, ClientSize.Width - 26, 124);
                e.Graphics.DrawLine(pen, 26, 316, ClientSize.Width - 26, 316);
            }
        }
    }
}
