using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace BatchDownloader
{
    public class LoaderQueue
    {
        private HttpClient httpClient;
        private WebProxy webProxy;
        private bool isLoading;
        private LoadItem current;

        public LoaderQueue(HttpClient cl, WebProxy proxy)
        {
            httpClient = cl;
            webProxy = proxy;
            isLoading = false;
            Items = new Queue<LoadItem>();
        }

        public Task CurrentTask { get; private set; }
        public Queue<LoadItem> Items { get; private set; }
        public long CurrentProgress { get; set; }
        public long CurrentTotal
        {
            get
            {
                return current?.FileSize ?? 0;
            }
        }
        public long TotalRemain
        {
            get
            {
                return Items.Sum(e => e.FileSize) + (current?.FileSize ?? 0);
            }
        }
        public int Count
        {
            get
            {
                return Items.Count + (current is null ? 0 : 1);
            }
        }
        public string DisplayName
        {
            get
            {
                return webProxy.Address.ToString();
            }
        }
        public LoaderProgress Progress
        {
            get
            {
                return new LoaderProgress(this);
            }
        }

        public void Add(LoadItem item)
        {
            Items.Enqueue(item);
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
            while (Items.Count > 0)
            {
                current = Items.Dequeue();
                var resp = await httpClient.GetAsync(current.Url, HttpCompletionOption.ResponseHeadersRead);
                if (current.FileSize == 0)
                {
                    current.FileSize = resp.Content.Headers.ContentLength ?? 0;
                }

                var inStream = await resp.Content.ReadAsStreamAsync();
                var outStream = File.OpenWrite(current.SavePath);
                var buffer = new byte[4096];
                CurrentProgress = 0;

                while (true)
                {
                    var sz = inStream.Read(buffer, 0, buffer.Length);
                    if (sz == 0)
                    {
                        break;
                    }

                    outStream.Write(buffer, 0, sz);
                    CurrentProgress += sz;
                }

                CurrentProgress = 0;
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
