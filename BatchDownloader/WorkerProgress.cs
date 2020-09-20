namespace BatchDownloader
{
    public class WorkerProgress
    {
        public string Name { get; }
        public long TotalBytes { get; }
        public long DownloadedBytes { get; }
        public WorkerProgress(ProxyWorker queue)
        {
            Name = queue.DisplayName;
            DownloadedBytes = queue.BytesDownloaded;
            TotalBytes = queue.BytesTotal;
        }

        public override string ToString()
        {
            return $"{Name} | " +
                (TotalBytes == 0 ? "100% completed | " : $"{(int)((double)DownloadedBytes / TotalBytes * 100)}% completed | ") +
                (TotalBytes == 0 ? "" : $"[{DownloadedBytes}/{TotalBytes}] bytes");
        }
    }
}
