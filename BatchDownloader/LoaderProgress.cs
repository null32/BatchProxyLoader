using System;
using System.Collections.Generic;
using System.Text;

namespace BatchDownloader
{
    public class LoaderProgress
    {
        public string Name { get; }
        public int TotalFiles { get; }
        public long TotalBytes { get; }
        public long CurrentBytes { get; }
        public LoaderProgress(LoaderQueue queue)
        {
            Name = queue.DisplayName;
            TotalFiles = queue.Count;
            CurrentBytes = queue.CurrentProgress;
            TotalBytes = queue.CurrentTotal;
        }

        public override string ToString()
        {
            return $"{Name} | " +
                $"{TotalFiles} file(s) to download | " +
                (TotalBytes == 0 ? "100% completed | " : $"{(int)((double)CurrentBytes / TotalBytes * 100)}% completed | ") +
                (TotalBytes == 0 ? "" : $"[{CurrentBytes}/{TotalBytes}] bytes");
        }
    }
}
