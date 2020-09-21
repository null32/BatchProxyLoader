# BatchProxyLoader
Balancer for downloading via http proxies.

Purpose is to split file downloads between multiple proxy connections to maximize the download speed.

## Use case
If you have the following conditions:
1. A bunch of resources that needs to be downloaded to target via proxies.
2. Multiple proxies with download speed less then target.

## C# example
```csharp
var loader = new Loader();
// Using user:password authentication
loader.AddProxy("http://1.2.3.4:3128", "username", "password");
// No authentication
loader.AddProxy("http://someproxy.test:3128");
// Downloading files
loader.DownloadFile("https://somedomen.com/somefile_1.bin", "C:\\some_file_1.bin");
loader.DownloadFile("https://somedomen.com/somefile_2.bin", "C:\\some_file_2.bin");
loader.DownloadFile("https://somedomen.com/somefile_3.bin", "C:\\some_file_3.bin");
// Reporting progress while waiting for completion
while (loader.IsInProgress)
{
    Console.WriteLine(loader.Progress);
    Thread.Sleep(1000);
}
// Waiting for completion synced way
loader.WaitForDownload();
// Waiting for completion async way
await loader.DownloadTask;
```

## PowerShell 7 cli
[batch-loader.ps1](./batch-loader.ps1) contains a very simple script that allows library usage from powershell console.

**SECURITY CONCERN**: Script saves & loads added proxies in plaintext file.