using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Forms
{
    // Sign-in dialog for GitHub. GetHub uses a personal access token because it
    // ships without an OAuth client id; the token is validated against /user
    // before it is stored, so a typo never replaces a working sign-in.
    public class SignInForm : Form
    {
        private readonly RepoStore _store;
        private readonly TextBox _tokenBox;
        private readonly CheckBox _showToken;
        private readonly Label _statusLabel;
        private readonly Button _signInButton;
        private readonly Button _cancelButton;
        private readonly LinkLabel _tokenLink;
        private bool _busy;

        public SignInForm(RepoStore store)
        {
            _store = store;
            Text = "Sign in to GitHub";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 412);
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
            title.Text = "Sign in to GitHub";
            title.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Regular);
            title.ForeColor = UiTheme.Text;
            title.AutoSize = true;
            title.Location = new Point(26, 20);
            Controls.Add(title);
            UiTheme.FitLabel(title);

            Label intro = new Label();
            intro.Text = "Signing in lets GetHub read this repository's issues, comments and project boards " +
                "and keep a copy for offline reading. The token is stored in your Windows profile.";
            intro.Font = UiTheme.BodyFont;
            intro.ForeColor = UiTheme.Muted;
            intro.AutoSize = false;
            intro.Location = new Point(26, 58);
            intro.Size = new Size(508, 52);
            Controls.Add(intro);

            Label tokenLabel = new Label();
            tokenLabel.Text = "Personal access token";
            tokenLabel.Font = UiTheme.BodyBold;
            tokenLabel.ForeColor = UiTheme.Text;
            tokenLabel.AutoSize = true;
            tokenLabel.Location = new Point(26, 122);
            Controls.Add(tokenLabel);
            UiTheme.FitLabel(tokenLabel);

            _tokenBox = new TextBox();
            _tokenBox.Location = new Point(26, 144);
            _tokenBox.Size = new Size(508, 28);
            _tokenBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _tokenBox.BorderStyle = BorderStyle.FixedSingle;
            _tokenBox.UseSystemPasswordChar = true;
            _tokenBox.MaxLength = 255;
            Controls.Add(_tokenBox);

            _showToken = new CheckBox();
            _showToken.Text = "Show token";
            _showToken.Font = UiTheme.SmallFont;
            _showToken.ForeColor = UiTheme.Muted;
            _showToken.AutoSize = true;
            _showToken.Location = new Point(26, 180);
            _showToken.CheckedChanged += delegate
            {
                _tokenBox.UseSystemPasswordChar = !_showToken.Checked;
            };
            Controls.Add(_showToken);

            _tokenLink = new LinkLabel();
            _tokenLink.Text = "Create a token with the repo and read:project scopes";
            _tokenLink.Font = UiTheme.SmallFont;
            _tokenLink.LinkColor = UiTheme.Accent;
            _tokenLink.ActiveLinkColor = UiTheme.Accent;
            _tokenLink.AutoSize = true;
            _tokenLink.Location = new Point(26, 210);
            _tokenLink.LinkClicked += delegate
            {
                UiTheme.OpenUrl(GitHubAuthService.BuildTokenUrl());
            };
            Controls.Add(_tokenLink);
            UiTheme.FitLabel(_tokenLink);

            _statusLabel = new Label();
            _statusLabel.Text = string.Empty;
            _statusLabel.Font = UiTheme.SmallFont;
            _statusLabel.ForeColor = UiTheme.Muted;
            _statusLabel.AutoSize = false;
            _statusLabel.Location = new Point(26, 244);
            _statusLabel.Size = new Size(508, 72);
            Controls.Add(_statusLabel);

            _cancelButton = UiTheme.MakeButton("Cancel", ButtonStyle.Secondary);
            _cancelButton.Location = new Point(368, 352);
            _cancelButton.Size = new Size(80, 32);
            _cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(_cancelButton);

            _signInButton = UiTheme.MakeButton("Sign in", ButtonStyle.Primary);
            _signInButton.Location = new Point(458, 352);
            _signInButton.Size = new Size(76, 32);
            _signInButton.Click += SignInClicked;
            Controls.Add(_signInButton);

            AcceptButton = _signInButton;
            CancelButton = _cancelButton;
            Shown += delegate { _tokenBox.Focus(); };
            Paint += FormPaint;

            UiTheme.SetCueBanner(_tokenBox, "ghp_... or github_pat_...");
            GitHubAccount existing = store.Settings == null ? null : store.Settings.GetAccount();
            if (existing != null && existing.IsAuthenticated)
            {
                _statusLabel.Text = "Currently signed in as @" + existing.Login +
                    ". Enter a new token to replace it.";
            }
            else
            {
                _statusLabel.Text = "GetHub never sends the token anywhere except api.github.com.";
            }

            ResumeLayout(false);
        }

        public GitHubAccount Account { get; private set; }

        public bool SignedIn
        {
            get { return Account != null && Account.IsAuthenticated; }
        }

        private async void SignInClicked(object sender, EventArgs e)
        {
            if (_busy)
            {
                return;
            }

            string token = _tokenBox.Text == null ? string.Empty : _tokenBox.Text.Trim();
            if (token.Length == 0)
            {
                ShowStatus("Enter a GitHub personal access token.", true);
                _tokenBox.Focus();
                return;
            }

            SetBusy(true);
            ShowStatus("Checking the token with GitHub...", false);
            try
            {
                GitHubAuthService service = new GitHubAuthService(token);
                GitHubAccount account = await service.SignInAsync(CancellationToken.None);
                if (IsDisposed)
                {
                    return;
                }

                _store.Settings.GitHubToken = token;
                _store.Settings.SetAccount(account);
                try
                {
                    _store.SaveSettings();
                }
                catch (Exception ex)
                {
                    ShowStatus("The token is valid, but the settings could not be saved: " + ex.Message, true);
                    return;
                }

                Account = account;
                if (!GitHubAuthService.CanReadProjects(account))
                {
                    MessageBox.Show(
                        this,
                        "Signed in as @" + account.Login + ".\n\n" +
                        "This token does not include the read:project scope, so the Projects tab will not load. " +
                        "Issues work normally. Sign in again with a token that has read:project to read project boards.",
                        "Sign in to GitHub",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                DialogResult = DialogResult.OK;
                Close();
            }
            catch (GitHubException ex)
            {
                ShowStatus(ex.Message, true);
            }
            catch (Exception ex)
            {
                ShowStatus("Could not sign in: " + ex.Message, true);
            }
            finally
            {
                if (!IsDisposed)
                {
                    SetBusy(false);
                }
            }
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _signInButton.Enabled = !busy;
            _cancelButton.Enabled = !busy;
            _tokenBox.Enabled = !busy;
            _showToken.Enabled = !busy;
            _tokenLink.Enabled = !busy;
            _signInButton.Text = busy ? "Checking..." : "Sign in";
            UseWaitCursor = busy;
        }

        private void ShowStatus(string text, bool isError)
        {
            _statusLabel.Text = text ?? string.Empty;
            _statusLabel.ForeColor = isError ? UiTheme.Red : UiTheme.Muted;
        }

        private void FormPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(UiTheme.Border))
            {
                e.Graphics.DrawLine(pen, 26, 332, ClientSize.Width - 26, 332);
            }
        }
    }
}
