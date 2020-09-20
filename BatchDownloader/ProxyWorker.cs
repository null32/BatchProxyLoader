using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BatchDownloader
{
    public class ProxyWorker
    {
        public HttpClient HttpClient { get; private set; }
        public WebProxy WebProxy { get; private set; }
        public Queue<LoadItem> Items { get; private set; }
        public Task CurrentTask { get; private set; }
        public long BytesDownloaded { get; set; }
        public long BytesTotal
        {
            get
            {
                return current?.FileSize ?? 0;
            }
        }
        public string DisplayName
        {
            get
            {
                return WebProxy.Address.ToString();
            }
        }
        public WorkerProgress Progress
        {
            get
            {
                return new WorkerProgress(this);
            }
        }

        private bool isLoading;
        private LoadItem current;

        public ProxyWorker(HttpClient cl, WebProxy proxy, Queue<LoadItem> items)
        {
            HttpClient = cl;
            WebProxy = proxy;
            Items = items;
            isLoading = false;
        }

        public void WakeUp()
        {
            var t = StartLoading();
            if (!t.IsCompleted)
            {
                CurrentTask = t;
            }
        }

        private async Task StartLoading()
        {
            if (isLoading)
            {
                return;
            }
            isLoading = true;
            while (true)
            {
                lock (Items)
                {
                    if (Items.Count == 0)
                    {
                        break;
                    }
                    current = Items.Dequeue();
                }

                var resp = await HttpClient.GetAsync(current.Url, HttpCompletionOption.ResponseHeadersRead);
                if (current.FileSize == 0)
                {
                    current.FileSize = resp.Content.Headers.ContentLength ?? 0;
                }

                var inStream = await resp.Content.ReadAsStreamAsync();
                var outStream = File.OpenWrite(current.SavePath);
                var buffer = new byte[4096];
                BytesDownloaded = 0;

                while (true)
                {
                    var sz = inStream.Read(buffer, 0, buffer.Length);
                    if (sz == 0)
                    {
                        break;
                    }

                    outStream.Write(buffer, 0, sz);
                    BytesDownloaded += sz;
                }

                BytesDownloaded = 0;
                inStream.Close();
                outStream.Close();
            }
            isLoading = false;
            current = null;
        }

        public override string ToString()
        {
            return Progress.ToString();
        }
    }
}
