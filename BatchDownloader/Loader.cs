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
        public Uri ProxyCheckUrl
        {
            get
            {
                return proxyCheckUrl;
            }
            set
            {
                if (value is null)
                {
                    throw new Exception("Uri can not be null");
                }
                proxyCheckUrl = value;
            }
        }
        private Uri proxyCheckUrl;
        public HttpMethod ProxyCheckMethod
        {
            get
            {
                return proxyCheckMethod;
            }
            set
            {
                if (!(value is null) || !allowedMethods.Contains(value))
                {
                    throw new Exception("Method is null or not allowed");
                }
                proxyCheckMethod = value;
            }
        }
        private HttpMethod proxyCheckMethod;
        public bool ProxyCheckOnAdd { get; set; }
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
        private readonly List<HttpMethod> allowedMethods = new List<HttpMethod> { HttpMethod.Get, HttpMethod.Head };

        public Loader() : this("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:68.0) Gecko/20100101 Firefox/68.0") { }
        public Loader(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                throw new ArgumentException("Empty user agent", "userAgent");
            }

            Workers = new List<ProxyWorker>();
            FileSizePrecache = false;
            ProxyCheckOnAdd = true;
            random = new Random();
            loadItems = new Queue<LoadItem>();
            this.userAgent = userAgent;
            proxyCheckUrl = new Uri("https://google.com/");
            proxyCheckMethod = HttpMethod.Get;
        }

        public Task<bool> AddProxy(string address)
            => AddProxy(address, null);
        public Task<bool> AddProxy(string address, string userName, string passWord)
            => AddProxy(address, new NetworkCredential(userName, passWord));
        public Task<bool> AddProxy(string address, string userName, SecureString passWord)
            => AddProxy(address, new NetworkCredential(userName, passWord));
        public async Task<bool> AddProxy(string address, ICredentials creds = null)
        {
            var proxy = new WebProxy(address);
            proxy.Credentials = creds;

            var hclientHandler = new HttpClientHandler();
            hclientHandler.Proxy = proxy;
            hclientHandler.UseProxy = true;

            var hclient = new HttpClient(hclientHandler);
            hclient.DefaultRequestHeaders.Add("User-Agent", userAgent);

            var worker = new ProxyWorker(hclient, proxy, loadItems);
            if (ProxyCheckOnAdd)
            {
                var isProxyOk = await worker.CheckConnection(proxyCheckUrl, proxyCheckMethod);
                if (isProxyOk)
                {
                    Workers.Add(worker);
                    return true;
                }
                return false;
            }

            Workers.Add(worker);

            return true;
        }

        public void RemoveProxy(int index)
        {
            if (index >= 0 && index < Proxies.Count)
            {
                Workers.RemoveAt(index);
            }
        }

        public Task<bool> CheckProxy(int index) =>
            CheckProxy(index, proxyCheckUrl, HttpMethod.Get);
        public Task<bool> CheckProxy(int index, string url) =>
            CheckProxy(index, new Uri(url), HttpMethod.Get);
        public Task<bool> CheckProxy(int index, Uri url) =>
            CheckProxy(index, url, HttpMethod.Get);
        public Task<bool> CheckProxy(int index, string url, HttpMethod method) =>
            CheckProxy(index, new Uri(url), method);
        public Task<bool> CheckProxy(int index, Uri url, HttpMethod method)
        {
            if (!allowedMethods.Contains(method))
            {
                throw new ArgumentException("Method not allowed", "method");
            }
            if (index < 0 || index > Workers.Count)
            {
                throw new ArgumentException("Invalid worker index", "index");
            }

            return Workers[index].CheckConnection(url, method);
        }

        public Task<IEnumerable<bool>> CheckProxies() =>
            CheckProxies(proxyCheckUrl);
        public Task<IEnumerable<bool>> CheckProxies(string url) =>
            CheckProxies(new Uri(url));
        public async Task<IEnumerable<bool>> CheckProxies(Uri url)
        {
             return await Task.WhenAll(Enumerable.Range(0, Workers.Count).Select(e => CheckProxy(e, url)));
        }

        public Task<long> GetUrlFileSize(string url) =>
            GetUrlFileSize(new Uri(url));
        public async Task<long> GetUrlFileSize(Uri url)
        {
            var req = new HttpRequestMessage(HttpMethod.Head, url);
            var resp = await HttpClients[random.Next(HttpClients.Count)].SendAsync(req);
            return resp.Content.Headers.ContentLength ?? -1;
        }

        public Task<LoadItem> DownloadFile(string url, string savePath)
            => DownloadFile(new Uri(url), savePath);
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
