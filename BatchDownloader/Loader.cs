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
        public List<WebProxy> Proxies { get; private set; }
        public List<HttpClient> HttpClients { get; private set; }
        public bool FileSizeBasedRoundRobin { get; set; }

        private readonly string userAgent;
        private readonly Random random;
        private readonly List<LoaderQueue> queue;

        public Loader() : this("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:68.0) Gecko/20100101 Firefox/68.0") { }
        public Loader(string userAgent)
        {
            Proxies = new List<WebProxy>();
            HttpClients = new List<HttpClient>();
            random = new Random();
            FileSizeBasedRoundRobin = true;
            queue = new List<LoaderQueue>();

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

            Proxies.Add(proxy);

            var hclientHandler = new HttpClientHandler();
            hclientHandler.Proxy = proxy;
            hclientHandler.UseProxy = true;

            var hclient = new HttpClient(hclientHandler);
            hclient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            HttpClients.Add(hclient);
            queue.Add(new LoaderQueue(hclient, proxy));

            return proxy;
        }

        public void RemoveProxy(int index)
        {
            if (index >= 0 && index < Proxies.Count)
            {
                Proxies.RemoveAt(index);
                HttpClients.RemoveAt(index);
                queue.RemoveAt(index);
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

        public async Task<LoaderQueue> DownloadFile(string url, string savePath)
        {
            return await DownloadFile(new Uri(url), savePath);
        }
        public async Task<LoaderQueue> DownloadFile(Uri url, string savePath)
        {
            LoaderQueue q;
            if (FileSizeBasedRoundRobin)
            {
                var sz = await GetUrlFileSize(url);
                if (sz == -1)
                {
                    throw new Exception("Failed to get size of file via HEAD");
                }
                q = queue.OrderBy(e => e.TotalRemain).First();
                q.Add(new LoadItem() { FileSize = sz, Url = url, SavePath = savePath });
            }
            else
            {
                q = queue.OrderBy(e => e.Items.Count).First();
                q.Add(new LoadItem() { Url = url, SavePath = savePath });
            }

            return q;
        }

        public void WaitForDownload()
        {
            Task.WaitAll(queue.Select(e => e.CurrentTask).ToArray());
        }
        public bool IsInProgress
        {
            get
            {
                return queue.Any(e => !e.CurrentTask.IsCompleted);
            }
        }
        public FullProgress Progress
        {
            get
            {
                return new FullProgress(queue);
            }
        }

        public override string ToString()
        {
            return Progress.ToString();
        }
    }
}
