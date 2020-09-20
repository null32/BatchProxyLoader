using BatchDownloader;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace DebugApp
{
    class Program
    {
        static void Main(string[] args)
        {
            new Program().AsyncMain().GetAwaiter().GetResult();
        }

        private async Task AsyncMain()
        {
            var loader = new Loader();
            loader.AddProxy("http://10.8.8.1:3128", "proxyuser", "dota2pudge");
            loader.AddProxy("http://10.8.10.1:3128", "proxyuser", "w@k3_m3_up_1ns1d3");

            LoaderQueue q;
            q = await loader.DownloadFile("https://skycolor.space/cstrike/MaxPayne - Texes.wad", "D:\\Users\\Pavel\\test_01");
            Console.WriteLine(q);
            q = await loader.DownloadFile("https://skycolor.space/cstrike/MaxPayne - Texes.wad", "D:\\Users\\Pavel\\test_02");
            Console.WriteLine(q);
            q = await loader.DownloadFile("https://skycolor.space/cstrike/MaxPayne - Texes.wad", "D:\\Users\\Pavel\\test_03");
            Console.WriteLine(q);
            q = await loader.DownloadFile("https://skycolor.space/cstrike/MaxPayne - Texes.wad", "D:\\Users\\Pavel\\test_04");
            Console.WriteLine(q);

            while (loader.IsInProgress)
            {
                Console.WriteLine(loader.Progress);
                Thread.Sleep(1000);
            }

            Console.ReadLine();
        }
    }
}
