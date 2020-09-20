namespace BatchDownloader
{
    public class WorkerProgress
    {
        public string Name { get; }
        public long TotalBytes { get; }
        public long CurrentBytes { get; }
        public WorkerProgress(ProxyWorker queue)
        {
            Name = queue.DisplayName;
            CurrentBytes = queue.BytesDownloaded;
            TotalBytes = queue.BytesTotal;
        }

        public override string ToString()
        {
            return $"{Name} | " +
                (TotalBytes == 0 ? "100% completed | " : $"{(int)((double)CurrentBytes / TotalBytes * 100)}% completed | ") +
                (TotalBytes == 0 ? "" : $"[{CurrentBytes}/{TotalBytes}] bytes");
        }
    }
}
