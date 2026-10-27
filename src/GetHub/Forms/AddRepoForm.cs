using System;
using System.Drawing;
using System.Windows.Forms;
using GetHub.Services;
using GetHub.Utilities;

namespace GetHub.Forms
{
    public class AddRepoForm : Form
    {
        private readonly TextBox _urlBox;
        private readonly TextBox _branchBox;
        private readonly Label _errorLabel;

        public RepoReference Reference { get; private set; }

        public AddRepoForm()
        {
            Text = "Add repository";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(580, 344);
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
            title.Text = "Add a GitHub repository";
            title.Font = new Font("Segoe UI Semibold", 16F, FontStyle.Regular);
            title.ForeColor = UiTheme.Text;
            title.AutoSize = true;
            title.Location = new Point(26, 20);
            Controls.Add(title);
            UiTheme.FitLabel(title);

            Label subtitle = new Label();
            subtitle.Text = "Paste a public repository address. Private repositories need a token in Settings.";
            subtitle.Font = UiTheme.BodyFont;
            subtitle.ForeColor = UiTheme.Muted;
            subtitle.AutoSize = false;
            subtitle.Location = new Point(26, 52);
            subtitle.Size = new Size(528, 32);
            Controls.Add(subtitle);

            Label urlLabel = new Label();
            urlLabel.Text = "Repository URL";
            urlLabel.Font = UiTheme.BodyBold;
            urlLabel.ForeColor = UiTheme.Text;
            urlLabel.AutoSize = true;
            urlLabel.Location = new Point(26, 96);
            Controls.Add(urlLabel);
            UiTheme.FitLabel(urlLabel);

            _urlBox = new TextBox();
            _urlBox.Location = new Point(26, 116);
            _urlBox.Size = new Size(528, 28);
            _urlBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _urlBox.BorderStyle = BorderStyle.FixedSingle;
            _urlBox.TabIndex = 0;
            _urlBox.TextChanged += delegate { _errorLabel.Text = string.Empty; };
            Controls.Add(_urlBox);

            Label branchLabel = new Label();
            branchLabel.Text = "Branch (optional)";
            branchLabel.Font = UiTheme.BodyBold;
            branchLabel.ForeColor = UiTheme.Text;
            branchLabel.AutoSize = true;
            branchLabel.Location = new Point(26, 156);
            Controls.Add(branchLabel);
            UiTheme.FitLabel(branchLabel);

            _branchBox = new TextBox();
            _branchBox.Location = new Point(26, 176);
            _branchBox.Size = new Size(250, 28);
            _branchBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
            _branchBox.BorderStyle = BorderStyle.FixedSingle;
            _branchBox.TabIndex = 1;
            _branchBox.TextChanged += delegate { _errorLabel.Text = string.Empty; };
            Controls.Add(_branchBox);

            Label branchHint = new Label();
            branchHint.Text = "Leave blank to use the repository's default branch.";
            branchHint.Font = UiTheme.SmallFont;
            branchHint.ForeColor = UiTheme.Faint;
            branchHint.AutoSize = false;
            branchHint.Location = new Point(288, 180);
            branchHint.Size = new Size(266, 34);
            Controls.Add(branchHint);

            Label tip = new Label();
            tip.Text = "Examples: https://github.com/dotnet/runtime  ·  owner/repository  ·  git@github.com:owner/repository.git";
            tip.Font = UiTheme.SmallFont;
            tip.ForeColor = UiTheme.Muted;
            tip.AutoSize = false;
            tip.Location = new Point(26, 218);
            tip.Size = new Size(528, 22);
            Controls.Add(tip);

            _errorLabel = new Label();
            _errorLabel.Text = string.Empty;
            _errorLabel.Font = UiTheme.SmallFont;
            _errorLabel.ForeColor = UiTheme.Red;
            _errorLabel.AutoSize = false;
            _errorLabel.Location = new Point(26, 246);
            _errorLabel.Size = new Size(528, 22);
            Controls.Add(_errorLabel);

            Button cancel = UiTheme.MakeButton("Cancel", ButtonStyle.Secondary);
            cancel.Location = new Point(368, 292);
            cancel.Size = new Size(80, 32);
            cancel.DialogResult = DialogResult.Cancel;
            Controls.Add(cancel);

            Button download = UiTheme.MakeButton("Download", ButtonStyle.Primary);
            download.Location = new Point(458, 292);
            download.Size = new Size(96, 32);
            download.TabIndex = 2;
            download.Click += DownloadClicked;
            Controls.Add(download);

            AcceptButton = download;
            CancelButton = cancel;
            Shown += FormShown;
            Paint += FormPaint;

            UiTheme.SetCueBanner(_urlBox, "https://github.com/owner/repository");
            UiTheme.SetCueBanner(_branchBox, "main");
            ResumeLayout(false);
        }

        private void DownloadClicked(object sender, EventArgs e)
        {
            try
            {
                Reference = GitHubDownloader.ParseReference(_urlBox.Text, _branchBox.Text);
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                _errorLabel.Text = ex.Message;
                _urlBox.Focus();
                _urlBox.SelectAll();
            }
        }

        private void FormShown(object sender, EventArgs e)
        {
            _urlBox.Focus();
        }

        private void FormPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(UiTheme.Border))
            {
                e.Graphics.DrawLine(pen, 26, 278, ClientSize.Width - 26, 278);
            }
        }
    }
}
