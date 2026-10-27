namespace GetHub.Models
{
    public class DownloadProgress
    {
        public string Status { get; set; }
        public long BytesReceived { get; set; }
        public long TotalBytes { get; set; }
        public bool Indeterminate { get; set; }

        public DownloadProgress()
        {
            Status = string.Empty;
        }

        public static DownloadProgress Busy(string status)
        {
            DownloadProgress progress = new DownloadProgress();
            progress.Status = status;
            progress.Indeterminate = true;
            return progress;
        }

        public static DownloadProgress Bytes(string status, long received, long total)
        {
            DownloadProgress progress = new DownloadProgress();
            progress.Status = status;
            progress.BytesReceived = received;
            progress.TotalBytes = total;
            progress.Indeterminate = total <= 0;
            return progress;
        }

        public int GetPercent()
        {
            if (TotalBytes <= 0)
            {
                return 0;
            }

            long percent = (BytesReceived * 100L) / TotalBytes;
            if (percent < 0)
            {
                return 0;
            }

            if (percent > 100)
            {
                return 100;
            }

            return (int)percent;
        }
    }
}
