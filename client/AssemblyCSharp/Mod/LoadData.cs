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
        public static TcpClient ProxyFaker(ProxyType proxyType, string targetHost, int targetPort, string proxyHost, int proxyPort, string user, string password)
        {
            ProxyClientFactory factory = new ProxyClientFactory();
            IProxyClient proxyClient = string.IsNullOrEmpty(user)
                ? factory.CreateProxyClient(proxyType, proxyHost, proxyPort)
                : factory.CreateProxyClient(proxyType, proxyHost, proxyPort, user, password ?? string.Empty);

            proxyClient.ReceiveTimeout = 60000;
            proxyClient.SendTimeout = 60000;
            return proxyClient.CreateConnection(targetHost, targetPort);
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
