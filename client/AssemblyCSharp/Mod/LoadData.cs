using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using LitJson;
using ProxyLib.Proxy;

namespace AssemblyCSharp.Mod
{
    public static class FakeProxy
    {
        private static TcpClient ApplySocketTimeouts(TcpClient cli)
        {
            if (cli != null)
            {
                cli.NoDelay = true;
                cli.ReceiveTimeout = 90000;
                cli.SendTimeout = 30000;
            }
            return cli;
        }

        public static TcpClient ProxyFaker(ProxyType proxyType, string targetHost, int targetPort, string proxyHost, int proxyPort, string user, string password)
        {
            ProxyClientFactory factory = new ProxyClientFactory();
            IProxyClient proxyClient = string.IsNullOrEmpty(user)
                ? factory.CreateProxyClient(proxyType, proxyHost, proxyPort)
                : factory.CreateProxyClient(proxyType, proxyHost, proxyPort, user, password ?? string.Empty);

            proxyClient.ReceiveTimeout = 20000;
            proxyClient.SendTimeout = 20000;
            return ApplySocketTimeouts(proxyClient.CreateConnection(targetHost, targetPort));
        }

        public static TcpClient ConnectThroughConfiguredProxy(string targetHost, int targetPort)
        {
            string raw = (LoadData.Account.proxy ?? "").Trim();
            if (string.IsNullOrEmpty(raw) || string.Equals(raw, "home", StringComparison.OrdinalIgnoreCase))
            {
                TcpClient direct = new TcpClient();
                IAsyncResult ar = direct.BeginConnect(targetHost, targetPort, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(15000, false))
                {
                    try { direct.Close(); } catch { }
                    throw new SocketException(10060);
                }
                direct.EndConnect(ar);
                return ApplySocketTimeouts(direct);
            }

            ProxyType proxyType = string.Equals(LoadData.Account.proxyType, "socks5", StringComparison.OrdinalIgnoreCase)
                ? ProxyType.Socks5
                : ProxyType.Http;

            char sep = ':';
            if (raw.IndexOf('|') >= 0) sep = '|';
            else if (raw.IndexOf(',') >= 0 && raw.IndexOf(':') < 0) sep = ',';

            // Giới hạn tối đa 4 phần để bảo toàn mật khẩu proxy chứa dấu ':', '|', ','
            string[] parts = raw.Split(new char[] { sep }, 4);
            if (parts.Length < 2)
            {
                throw new InvalidOperationException("Sai định dạng proxy: " + raw);
            }
            string proxyHost = parts[0].Trim();
            int proxyPort = int.Parse(parts[1].Trim());
            string username = parts.Length >= 3 ? parts[2].Trim() : string.Empty;
            string password = parts.Length >= 4 ? parts[3].Trim() : string.Empty;
            return ProxyFaker(proxyType, targetHost, targetPort, proxyHost, proxyPort, username, password);
        }
    }

    public static class LoadData
    {
        public class Account
        {
            public static string accountID = "";
            public static string username = "";
            public static string password = "";
            public static string server = "";
            public static int mapID = -1;
            public static string zoneMode = "empty";
            public static int zoneID = 0;
            public static int cxEnd = 0;
            public static int cyEnd = 0;
            public static string proxy = "home";
            public static string proxyType = "http";
            public static int socketPort = 20268;
            public static string profileFile = "";
            public static long titleExpiryMs = 0;
            public static bool privacyMode = false;
            public static bool headlessMode = false;
        }

        public static int Width = 380;
        public static int Height = 240;
        public static Dictionary<string, string> Arguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static void LoadProfileFromDisk()
        {
            try
            {
                if (!string.IsNullOrEmpty(Account.profileFile) && File.Exists(Account.profileFile))
                {
                    string json = File.ReadAllText(Account.profileFile);
                    JsonData jd = JsonMapper.ToObject(json);
                    var prof = FarmProfile.FromJsonData(jd);
                    if (prof != null)
                    {
                        GameEvents.ApplyProfile(prof);
                    }
                }
            }
            catch { }
        }

        public static void OnStartGame()
        {
            try
            {
                // Đảm bảo thư mục Data/rms luôn tồn tại trước khi GameCanvas() gọi Rms.clearAll()
                Directory.CreateDirectory("Data");
                Directory.CreateDirectory(Path.Combine("Data", "rms"));

                string[] args = Environment.GetCommandLineArgs();
                for (int i = 1; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "-nographics", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "-batchmode", StringComparison.OrdinalIgnoreCase))
                    {
                        Account.headlessMode = true;
                        continue;
                    }
                    if (i < args.Length - 1 && args[i].StartsWith("--"))
                    {
                        string key = args[i].Substring(2).Trim();
                        Arguments[key] = args[i + 1];
                        i++;
                    }
                }

                if (Arguments.ContainsKey("account-id")) Account.accountID = Arguments["account-id"].Trim();
                if (Arguments.ContainsKey("username")) Account.username = Arguments["username"].Trim();
                if (Arguments.ContainsKey("password")) Account.password = Arguments["password"].Trim();
                if (Arguments.ContainsKey("server")) Account.server = Arguments["server"].Trim();
                if (Arguments.ContainsKey("map")) int.TryParse(Arguments["map"].Trim(), out Account.mapID);
                if (Arguments.ContainsKey("zone-mode")) Account.zoneMode = Arguments["zone-mode"].Trim().ToLower();
                if (Arguments.ContainsKey("zone")) int.TryParse(Arguments["zone"].Trim(), out Account.zoneID);
                if (Arguments.ContainsKey("cx")) int.TryParse(Arguments["cx"].Trim(), out Account.cxEnd);
                if (Arguments.ContainsKey("cy")) int.TryParse(Arguments["cy"].Trim(), out Account.cyEnd);
                if (Arguments.ContainsKey("proxy")) Account.proxy = Arguments["proxy"].Trim();
                if (Arguments.ContainsKey("proxy-type")) Account.proxyType = Arguments["proxy-type"].Trim().ToLower();
                if (Arguments.ContainsKey("port")) int.TryParse(Arguments["port"].Trim(), out Account.socketPort);
                if (Arguments.ContainsKey("width")) int.TryParse(Arguments["width"].Trim(), out Width);
                if (Arguments.ContainsKey("height")) int.TryParse(Arguments["height"].Trim(), out Height);
                if (Arguments.ContainsKey("title-expiry")) long.TryParse(Arguments["title-expiry"].Trim(), out Account.titleExpiryMs);
                if (Arguments.ContainsKey("privacy"))
                {
                    string pv = Arguments["privacy"].Trim().ToLower();
                    Account.privacyMode = pv == "1" || pv == "true";
                }
                if (Arguments.ContainsKey("headless"))
                {
                    string hl = Arguments["headless"].Trim().ToLower();
                    if (hl == "1" || hl == "true") Account.headlessMode = true;
                }
                try
                {
                    if (UnityEngine.SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        Account.headlessMode = true;
                    }
                }
                catch { }
                if (Arguments.ContainsKey("profile-file")) Account.profileFile = Arguments["profile-file"].Trim();

                if (string.IsNullOrEmpty(Account.accountID) && !string.IsNullOrEmpty(Account.username))
                {
                    Account.accountID = Account.username;
                }

                LoadProfileFromDisk();
                SocketClient.Start();
            }
            catch { }
        }
    }
}
