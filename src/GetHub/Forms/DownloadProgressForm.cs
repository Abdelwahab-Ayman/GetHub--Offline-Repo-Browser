using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using GetHub.Models;
using GetHub.Utilities;

namespace GetHub.Forms
{
    public class DownloadProgressForm : Form
    {
        private readonly Label _statusLabel;
        private readonly Label _detailLabel;
        private readonly ProgressBar _progressBar;
        private readonly Button _cancelButton;
        private Func<IProgress<DownloadProgress>, CancellationToken, Task> _worker;
        private CancellationTokenSource _cancellation;
        private bool _started;
        private bool _busy;
        private bool _allowClose;
        private bool _succeeded;

        public Exception Error { get; private set; }
        public bool WasCancelled { get; private set; }

        public DownloadProgressForm(string title, string subtitle)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(500, 236);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Icon = AppAssets.AppIcon;
            BackColor = UiTheme.Surface;
            Font = UiTheme.BodyFont;
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96F, 96F);
            SuspendLayout();

            Label titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.Font = new Font("Segoe UI Semibold", 14F, FontStyle.Regular);
            titleLabel.ForeColor = UiTheme.Text;
            titleLabel.AutoSize = false;
            titleLabel.Location = new Point(22, 18);
            titleLabel.Size = new Size(456, 28);
            Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = subtitle;
            subtitleLabel.Font = UiTheme.BodyFont;
            subtitleLabel.ForeColor = UiTheme.Muted;
            subtitleLabel.AutoSize = false;
            subtitleLabel.Location = new Point(22, 48);
            subtitleLabel.Size = new Size(456, 22);
            subtitleLabel.AutoEllipsis = true;
            Controls.Add(subtitleLabel);

            _statusLabel = new Label();
            _statusLabel.Text = "Preparing...";
            _statusLabel.Font = UiTheme.BodyFont;
            _statusLabel.ForeColor = UiTheme.Text;
            _statusLabel.AutoSize = false;
            _statusLabel.Location = new Point(22, 86);
            _statusLabel.Size = new Size(456, 22);
            _statusLabel.AutoEllipsis = true;
            Controls.Add(_statusLabel);

            _progressBar = new ProgressBar();
            _progressBar.Location = new Point(22, 112);
            _progressBar.Size = new Size(456, 16);
            _progressBar.Style = ProgressBarStyle.Marquee;
            _progressBar.MarqueeAnimationSpeed = 30;
            Controls.Add(_progressBar);

            _detailLabel = new Label();
            _detailLabel.Text = string.Empty;
            _detailLabel.Font = UiTheme.SmallFont;
            _detailLabel.ForeColor = UiTheme.Faint;
            _detailLabel.AutoSize = false;
            _detailLabel.Location = new Point(22, 136);
            _detailLabel.Size = new Size(456, 18);
            Controls.Add(_detailLabel);

            _cancelButton = UiTheme.MakeButton("Cancel", ButtonStyle.Secondary);
            _cancelButton.Location = new Point(384, 184);
            _cancelButton.Size = new Size(94, 32);
            _cancelButton.Click += delegate { RequestCancel(); };
            Controls.Add(_cancelButton);

            CancelButton = _cancelButton;
            Shown += delegate { StartWork(); };
            Paint += FormPaint;
            ResumeLayout(false);
        }

        public bool Run(
            IWin32Window owner,
            Func<IProgress<DownloadProgress>, CancellationToken, Task> worker)
        {
            if (worker == null)
            {
                throw new ArgumentNullException("worker");
            }

            _worker = worker;
            if (owner != null)
            {
                ShowDialog(owner);
            }
            else
            {
                ShowDialog();
            }

            return _succeeded;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && _busy)
            {
                e.Cancel = true;
                RequestCancel();
                return;
            }

            base.OnFormClosing(e);
        }

        private async void StartWork()
        {
            if (_started || _worker == null)
            {
                return;
            }

            _started = true;
            _busy = true;
            _cancellation = new CancellationTokenSource();
            Progress<DownloadProgress> progress = new Progress<DownloadProgress>(ApplyProgress);

            try
            {
                ApplyProgress(DownloadProgress.Busy("Contacting GitHub..."));
                await _worker(progress, _cancellation.Token);
                _succeeded = true;
                _allowClose = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (OperationCanceledException)
            {
                WasCancelled = true;
                _allowClose = true;
                DialogResult = DialogResult.Cancel;
                Close();
            }
            catch (Exception ex)
            {
                Error = ex;
                _allowClose = true;
                DialogResult = DialogResult.Abort;
                Close();
            }
            finally
            {
                _busy = false;
                if (_cancellation != null)
                {
                    _cancellation.Dispose();
                    _cancellation = null;
                }
            }
        }

        private void ApplyProgress(DownloadProgress progress)
        {
            if (progress == null)
            {
                return;
            }

            _statusLabel.Text = string.IsNullOrEmpty(progress.Status) ? "Working..." : progress.Status;
            if (progress.Indeterminate)
            {
                _progressBar.Style = ProgressBarStyle.Marquee;
                _progressBar.MarqueeAnimationSpeed = 30;
                _detailLabel.Text = progress.BytesReceived > 0
                    ? Display.HumanSize(progress.BytesReceived) + " downloaded"
                    : string.Empty;
                return;
            }

            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = progress.GetPercent();
            if (progress.TotalBytes > 0)
            {
                _detailLabel.Text = progress.GetPercent().ToString() + "%  ·  " +
                    Display.HumanSize(progress.BytesReceived) + " of " +
                    Display.HumanSize(progress.TotalBytes);
            }
            else
            {
                _detailLabel.Text = Display.HumanSize(progress.BytesReceived);
            }
        }

        private void RequestCancel()
        {
            if (_cancellation == null || _cancellation.IsCancellationRequested)
            {
                return;
            }

            _cancelButton.Enabled = false;
            _statusLabel.Text = "Cancelling...";
            try
            {
                _cancellation.Cancel();
            }
            catch
            {
            }
        }

        private void FormPaint(object sender, PaintEventArgs e)
        {
            using (Pen pen = new Pen(UiTheme.Border))
            {
                e.Graphics.DrawLine(pen, 22, 170, ClientSize.Width - 22, 170);
            }
        }
    }
}
