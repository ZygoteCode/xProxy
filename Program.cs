using LegitHttpClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

public class Program
{
    public static ResourceSemaphore httpSemaphore, socks4Semaphore, socks5Semaphore, invalidSemaphore;
    public static string httpProxies = "", socks4Proxies = "", socks5Proxies = "", invalidProxies = "";
    public static int http, socks4, socks5, invalid, finished;

    public static void Main()
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
        Console.Title = "xProxy";
        start: string path = "";

        while (!System.IO.File.Exists(path) || !System.IO.Path.GetExtension(path).ToLower().Equals(".txt"))
        {
            Logger.LogInfo("Please, insert the path of the proxies file to check here: ");
            path = Console.ReadLine();

            if (path.StartsWith("\""))
            {
                path = path.Substring(1);
            }

            if (path.EndsWith("\""))
            {
                path = path.Substring(0, path.Length - 1);
            }

            if (!System.IO.File.Exists(path))
            {
                Logger.LogError("The specified file does not exist.");
            }
            else if (!System.IO.Path.GetExtension(path).ToLower().Equals(".txt"))
            {
                Logger.LogError("The specified file has not a valid extension (*.txt).");
            }
        }

        string[] lines = System.IO.File.ReadAllLines(path);

        if (lines.Length == 0)
        {
            Logger.LogError("The specified file has no content.");
            goto start;
        }

        List<ProxyConnection> proxies = new List<ProxyConnection>();

        foreach (string line in lines)
        {
            int colons = 0;

            foreach (char c in line.ToCharArray())
            {
                if (c.Equals(':'))
                {
                    colons++;
                }
            }

            string[] splitted = line.Split(':');

            if (splitted[0].Trim().Replace('\t'.ToString(), "") == "" || splitted[1].Trim().Replace('\t'.ToString(), "") == "")
            {
                continue;
            }

            if (colons == 1)
            {
                UriHostNameType checkedHostName = Uri.CheckHostName(splitted[0]);

                if (checkedHostName == UriHostNameType.Dns || checkedHostName == UriHostNameType.IPv4)
                {
                    if (Microsoft.VisualBasic.Information.IsNumeric(splitted[1]))
                    {
                        uint port = uint.Parse(splitted[1]);

                        if (port >= 0 && port <= 65535)
                        {
                            proxies.Add(new ProxyConnection(splitted[0], port));
                        }
                    }
                }
            }
            else if (colons == 3)
            {
                if (splitted[2].Trim().Replace('\t'.ToString(), "") == "" || splitted[3].Trim().Replace('\t'.ToString(), "") == "")
                {
                    continue;
                }

                UriHostNameType checkedHostName = Uri.CheckHostName(splitted[0]);

                if (checkedHostName == UriHostNameType.Dns || checkedHostName == UriHostNameType.IPv4)
                {
                    if (Microsoft.VisualBasic.Information.IsNumeric(splitted[1]))
                    {
                        uint port = uint.Parse(splitted[1]);

                        if (port >= 0 && port <= 65535)
                        {
                            proxies.Add(new ProxyConnection(splitted[0], port, splitted[2], splitted[3]));
                        }
                    }
                }
            }
        }

        if (proxies.Count == 0)
        {
            Logger.LogError("No valid proxies to check are in this file.");
            goto start;
        }

        httpSemaphore = new ResourceSemaphore();
        socks4Semaphore = new ResourceSemaphore();
        socks5Semaphore = new ResourceSemaphore();
        invalidSemaphore = new ResourceSemaphore();

        Logger.LogInfo("Checking all the provided proxies, please wait a while.");

        foreach (ProxyConnection proxy in proxies)
        {
            Thread.Sleep(10);
            Thread thread = new Thread(() => CheckProxy(proxy));
            thread.Priority = ThreadPriority.Highest;
            thread.Start();
        }

        while (finished != proxies.Count)
        {
            Thread.Sleep(10);
        }

        if (!System.IO.Directory.Exists("result"))
        {
            System.IO.Directory.CreateDirectory("result");
        }
        else
        {
            System.IO.Directory.Delete("result", true);
            System.IO.Directory.CreateDirectory("result");
        }

        System.IO.File.WriteAllText("result\\http.txt", httpProxies);
        System.IO.File.WriteAllText("result\\socks4.txt", socks4Proxies);
        System.IO.File.WriteAllText("result\\socks5.txt", socks5Proxies);
        System.IO.File.WriteAllText("result\\invalid.txt", invalidProxies);

        Logger.LogSuccess("Succesfully checked all proxies.");
        Logger.LogWarning("Valid HTTP proxies: " + http + ". Saved to result\\http.txt.");
        Logger.LogWarning("Valid SOCKS4 proxies: " + socks4 + ". Saved to result\\socks4.txt.");
        Logger.LogWarning("Valid SOCKS5 proxies: " + socks5 + ". Saved to result\\socks5.txt.");
        Logger.LogWarning("Totally invalid proxies: " + invalid + ". Saved to result\\invalid.txt.");
        Logger.LogInfo("Press ENTER to exit from the program.");

        Console.ReadLine();
    }

    public static void CheckProxy(ProxyConnection proxy)
    {
        bool proxyHttpValid = false, proxySocks4Valid = false, proxySocks5Valid = false;

        {
            IAsyncResult result;
            Action action = () =>
            {
                try
                {
                    HttpClient client = new HttpClient();
                    client.ConnectTo("ip4.seeip.org", true, 443, $"http://{proxy.Hostname}:{proxy.Port}", proxy.Username == null ? "" : proxy.Username, proxy.Password == null ? "" : proxy.Password);

                    HttpRequest request = new HttpRequest();
                    request.URI = "/";
                    request.Method = HttpMethod.GET;
                    request.Version = HttpVersion.HTTP_11;

                    request.Headers.Add(new HttpHeader() { Name = "Host", Value = "ip4.seeip.org" });

                    string ip = GetCleanIP(Encoding.UTF8.GetString(client.Send(request).Body));
                    System.Net.IPAddress ipAddress = null;

                    try
                    {
                        ipAddress = System.Net.IPAddress.Parse(ip);
                    }
                    catch
                    {

                    }

                    if (ipAddress != null)
                    {
                        proxyHttpValid = true;
                    }
                }
                catch
                {

                }
            };

            result = action.BeginInvoke(null, null);
            result.AsyncWaitHandle.WaitOne(3000);
        }

        {
            IAsyncResult result;
            Action action = () =>
            {
                try
                {
                    HttpClient client = new HttpClient();
                    client.ConnectTo("ip4.seeip.org", true, 443, $"socks4://{proxy.Hostname}:{proxy.Port}", proxy.Username == null ? "" : proxy.Username, proxy.Password == null ? "" : proxy.Password);

                    HttpRequest request = new HttpRequest();
                    request.URI = "/";
                    request.Method = HttpMethod.GET;
                    request.Version = HttpVersion.HTTP_11;

                    request.Headers.Add(new HttpHeader() { Name = "Host", Value = "ip4.seeip.org" });

                    string ip = GetCleanIP(Encoding.UTF8.GetString(client.Send(request).Body));
                    System.Net.IPAddress ipAddress = null;

                    try
                    {
                        ipAddress = System.Net.IPAddress.Parse(ip);
                    }
                    catch
                    {

                    }

                    if (ipAddress != null)
                    {
                        proxySocks4Valid = true;
                    }
                }
                catch
                {

                }
            };

            result = action.BeginInvoke(null, null);
            result.AsyncWaitHandle.WaitOne(3000);
        }

        {
            IAsyncResult result;
            Action action = () =>
            {
                try
                {
                    HttpClient client = new HttpClient();
                    client.ConnectTo("ip4.seeip.org", true, 443, $"socks5://{proxy.Hostname}:{proxy.Port}", proxy.Username == null ? "" : proxy.Username, proxy.Password == null ? "" : proxy.Password);

                    HttpRequest request = new HttpRequest();
                    request.URI = "/";
                    request.Method = HttpMethod.GET;
                    request.Version = HttpVersion.HTTP_11;

                    request.Headers.Add(new HttpHeader() { Name = "Host", Value = "ip4.seeip.org" });

                    string ip = GetCleanIP(Encoding.UTF8.GetString(client.Send(request).Body));
                    System.Net.IPAddress ipAddress = null;

                    try
                    {
                        ipAddress = System.Net.IPAddress.Parse(ip);
                    }
                    catch
                    {

                    }

                    if (ipAddress != null)
                    {
                        proxySocks5Valid = true;
                    }
                }
                catch
                {

                }
            };

            result = action.BeginInvoke(null, null);
            result.AsyncWaitHandle.WaitOne(3000);
        }
        
        if (!proxyHttpValid && !proxySocks4Valid && !proxySocks5Valid)
        {
            checkAgain: while (invalidSemaphore.IsResourceNotAvailable())
            {
                Thread.Sleep(100);
            }

            if (invalidSemaphore.IsResourceAvailable())
            {
                invalidSemaphore.LockResource();

                if (invalidProxies == "")
                {
                    invalidProxies = proxy.ToString();
                }
                else
                {
                    invalidProxies += "\r\n" + proxy.ToString();
                }

                invalidSemaphore.UnlockResource();
            }
            else
            {
                goto checkAgain;
            }

            invalid++;
        }
        else
        {
            if (proxyHttpValid)
            {
                checkAgain: while (httpSemaphore.IsResourceNotAvailable())
                {
                    Thread.Sleep(100);
                }

                if (httpSemaphore.IsResourceAvailable())
                {
                    httpSemaphore.LockResource();

                    if (httpProxies == "")
                    {
                        httpProxies = proxy.ToString();
                    }
                    else
                    {
                        httpProxies += "\r\n" + proxy.ToString();
                    }

                    httpSemaphore.UnlockResource();
                }
                else
                {
                    goto checkAgain;
                }

                http++;
            }

            if (proxySocks4Valid)
            {
                checkAgain: while (socks4Semaphore.IsResourceNotAvailable())
                {
                    Thread.Sleep(100);
                }

                if (socks4Semaphore.IsResourceAvailable())
                {
                    socks4Semaphore.LockResource();

                    if (socks4Proxies == "")
                    {
                        socks4Proxies = proxy.ToString();
                    }
                    else
                    {
                        socks4Proxies += "\r\n" + proxy.ToString();
                    }

                    socks4Semaphore.UnlockResource();
                }
                else
                {
                    goto checkAgain;
                }

                socks4++;
            }

            if (proxySocks5Valid)
            {
                checkAgain: while (socks5Semaphore.IsResourceNotAvailable())
                {
                    Thread.Sleep(100);
                }

                if (socks5Semaphore.IsResourceAvailable())
                {
                    socks5Semaphore.LockResource();

                    if (socks5Proxies == "")
                    {
                        socks5Proxies = proxy.ToString();
                    }
                    else
                    {
                        socks5Proxies += "\r\n" + proxy.ToString();
                    }

                    socks5Semaphore.UnlockResource();
                }
                else
                {
                    goto checkAgain;
                }

                socks5++;
            }
        }

        finished++;
    }

    public static string GetCleanIP(string body)
    {
        int skips = 0;

        for (int i = 0; i < body.Length; i++)
        {
            if (!Microsoft.VisualBasic.Information.IsNumeric(body[i]))
            {
                skips++;
            }
            else
            {
                break;
            }
        }

        if (skips > 0)
        {
            body = body.Substring(skips);
        }

        skips = 0;

        for (int i = body.Length - 1; i >= 0; i--)
        {
            if (!Microsoft.VisualBasic.Information.IsNumeric(body[i]))
            {
                skips++;
            }
            else
            {
                break;
            }
        }

        if (skips > 0)
        {
            body = body.Substring(body.Length - skips);
        }

        return body;
    }
}