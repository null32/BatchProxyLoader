using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security;
using System.Threading.Tasks;

namespace BatchDownloader
{
    public class Loader
    {
        public List<ProxyWorker> Workers { get; private set; }
        public List<WebProxy> Proxies
        {
            get
            {
                return Workers.Select(e => e.WebProxy).ToList();
            }
        }
        public List<HttpClient> HttpClients
        {
            get
            {
                return Workers.Select(e => e.HttpClient).ToList();
            }
        }
        public bool FileSizePrecache { get; set; }
        public bool IsInProgress
        {
            get
            {
                return Workers.Any(e => !e.CurrentTask.IsCompleted);
            }
        }
        public int QueueCount
        {
            get
            {
                 return loadItems.Count;
            }
        }
        public Task DownloadTask
        {
            get
            {
                return Task.WhenAll(Workers.Select(e => e.CurrentTask));
            }
        }
        public LoaderProgress Progress
        {
            get
            {
                return new LoaderProgress(this);
            }
        }

        private readonly Random random;
        private readonly Queue<LoadItem> loadItems;
        private readonly string userAgent;

        public Loader() : this("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:68.0) Gecko/20100101 Firefox/68.0") { }
        public Loader(string userAgent)
        {
            Workers = new List<ProxyWorker>();
            FileSizePrecache = true;
            random = new Random();
            loadItems = new Queue<LoadItem>();

            if (!string.IsNullOrEmpty(userAgent))
            {
                this.userAgent = userAgent;
            }
        }

        public WebProxy AddProxy(string address)
        {
            return AddProxy(address, null);
        }
        public WebProxy AddProxy(string address, string userName, string passWord)
        {
            return AddProxy(address, new NetworkCredential(userName, passWord));
        }
        public WebProxy AddProxy(string address, string userName, SecureString passWord)
        {
            return AddProxy(address, new NetworkCredential(userName, passWord));
        }
        public WebProxy AddProxy(string address, ICredentials creds = null)
        {
            var proxy = new WebProxy(address);
            proxy.Credentials = creds;

            var hclientHandler = new HttpClientHandler();
            hclientHandler.Proxy = proxy;
            hclientHandler.UseProxy = true;

            var hclient = new HttpClient(hclientHandler);
            hclient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            Workers.Add(new ProxyWorker(hclient, proxy, loadItems));

            return proxy;
        }

        public void RemoveProxy(int index)
        {
            if (index >= 0 && index < Proxies.Count)
            {
                Workers.RemoveAt(index);
            }
        }

        public async Task<long> GetUrlFileSize(string url)
        {
            return await GetUrlFileSize(new Uri(url));
        }
        public async Task<long> GetUrlFileSize(Uri url)
        {
            var req = new HttpRequestMessage(HttpMethod.Head, url);
            var resp = await HttpClients[random.Next(HttpClients.Count)].SendAsync(req);
            return resp.Content.Headers.ContentLength ?? -1;
        }

        public async Task<LoadItem> DownloadFile(string url, string savePath)
        {
            return await DownloadFile(new Uri(url), savePath);
        }
        public async Task<LoadItem> DownloadFile(Uri url, string savePath)
        {
            LoadItem li;
            if (FileSizePrecache)
            {
                var sz = await GetUrlFileSize(url);
                if (sz == -1)
                {
                    throw new Exception("Failed to get size of file via HEAD");
                }
                li = new LoadItem() { FileSize = sz, Url = url, SavePath = savePath };
            }
            else
            {
                li = new LoadItem() { Url = url, SavePath = savePath };
            }

            lock (loadItems)
            {
                loadItems.Enqueue(li);
            }

            WakeUpLoaders();
            return li;
        }

        private void WakeUpLoaders()
        {
            foreach (var worker in Workers)
            {
                worker.WakeUp();
            }
        }

        public void WaitForDownload()
        {
            DownloadTask.Wait();
        }

        public override string ToString()
        {
            return Progress.ToString();
        }
    }
}
