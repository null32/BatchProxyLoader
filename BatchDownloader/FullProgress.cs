using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BatchDownloader
{
    public class FullProgress
    {
        public int TotalFiles { get; }
        public IEnumerable<int> FilesPerLoader { get; }
        public long TotalBytes { get; }
        public long CurrentBytes { get; }
        public IEnumerable<LoaderProgress> Detailed { get; }
        public FullProgress(List<LoaderQueue> q)
        {
            FilesPerLoader = q.Select(e => e.Count);
            TotalFiles = FilesPerLoader.Sum();
            TotalBytes = q.Sum(e => e.TotalRemain);
            CurrentBytes = q.Sum(e => e.CurrentProgress);
            Detailed = q.Select(e => e.Progress);
        }

        public override string ToString()
        {
            return $"Files: {TotalFiles} in [{string.Join(", ", FilesPerLoader)}] | " +
                (TotalBytes == 0 ? "100% completed | " : $"{(int)((double)CurrentBytes / TotalBytes * 100)}% completed | ") +
                (TotalBytes == 0 ? "" : $"[{CurrentBytes}/{TotalBytes}] bytes") +
                $"{string.Join("", Detailed.Select(e => "\n\t" + e))}";
        }
    }
}
