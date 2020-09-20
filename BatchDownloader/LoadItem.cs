using System;

namespace BatchDownloader
{
    public class LoadItem
    {
        public LoadItem() { }
        public Uri Url { get; set; }
        public string SavePath { get; set; }
        public long FileSize { get; set; } = 0;

        public override string ToString()
        {
            return $"{Url} -> {SavePath}; size: {(FileSize != 0 ? FileSize.ToString() : "?")}";
        }
    }
}
