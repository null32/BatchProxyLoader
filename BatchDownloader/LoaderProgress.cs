using System.Collections.Generic;
using System.Linq;

namespace BatchDownloader
{
    public class LoaderProgress
    {
        public int TotalFiles { get; }
        public long TotalBytes { get; }
        public long DownloadedBytes { get; }
        public IEnumerable<WorkerProgress> Detailed { get; }
        public LoaderProgress(Loader loader)
        {
            TotalFiles = loader.QueueCount;
            TotalBytes = loader.Workers.Sum(e => e.BytesTotal);
            DownloadedBytes = loader.Workers.Sum(e => e.BytesDownloaded);
            Detailed = loader.Workers.Select(e => e.Progress);
        }

        public override string ToString()
        {
            return $"Files: {TotalFiles} | " +
                (TotalBytes == 0 ? "100% completed | " : $"{(int)((double)DownloadedBytes / TotalBytes * 100)}% completed | ") +
                (TotalBytes == 0 ? "" : $"[{DownloadedBytes}/{TotalBytes}] bytes") +
                $"{string.Join("", Detailed.Select(e => "\n\t" + e))}";
        }
    }
}
