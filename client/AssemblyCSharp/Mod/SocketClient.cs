using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using LitJson;
using Mod.ModHelper;
using static AssemblyCSharp.Mod.LoadData;

namespace AssemblyCSharp.Mod
{
    /// <summary>
    /// TCP NDJSON Client sạch trên 127.0.0.1 dùng LitJson thuần .NET 3.5 (tương thích 100% Unity 5.6 Mono).
    /// </summary>
    public static class SocketClient
    {
        private static readonly object SendLock = new object();
        private static readonly Queue<string> Outbox = new Queue<string>();
        private static readonly AutoResetEvent SendSignal = new AutoResetEvent(false);

        private static volatile bool _started;
        private static volatile bool _stopping;
        private static volatile bool _connected;
        private static TcpClient _client;
        private static StreamWriter _writer;

        public static bool IsConnected => _connected;

        public static void Stop()
        {
            _stopping = true;
            lock (SendLock)
            {
                _connected = false;
                try { if (_writer != null) _writer.Close(); } catch { }
                try { if (_client != null) _client.Close(); } catch { }
                _writer = null;
                _client = null;
            }
            try { SendSignal.Set(); } catch { }
        }

        public static void Start()
        {
            if (_started) return;
            _started = true;
            _stopping = false;

            var t = new Thread(ConnectionLoop) { IsBackground = true, Name = "MX_SocketLoop" };
            t.Start();

            var s = new Thread(SendLoop) { IsBackground = true, Name = "MX_SocketSend" };
            s.Start();
        }

        private static void ConnectionLoop()
        {
            while (!_stopping)
            {
                try
                {
                    if (string.IsNullOrEmpty(Account.accountID))
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    int port = Account.socketPort > 0 ? Account.socketPort : 20268;
                    var cli = new TcpClient();
                    cli.NoDelay = true;
                    cli.Connect("127.0.0.1", port);

                    var stream = cli.GetStream();
                    var reader = new StreamReader(stream, Encoding.UTF8);
                    var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

                    lock (SendLock)
                    {
                        _client = cli;
                        _writer = writer;
                        _connected = true;
                    }

                    NetProfileMap.MarkSessionBound(mSystem.currentTimeMillis());
                    SendHello();

                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        HandleCommandLine(line);
                    }
                }
                catch
                {
                }
                finally
                {
                    lock (SendLock)
                    {
                        _connected = false;
                        try { if (_writer != null) _writer.Close(); } catch { }
                        try { if (_client != null) _client.Close(); } catch { }
                        _writer = null;
                        _client = null;
                    }
                }
                Thread.Sleep(2500);
            }
        }

        private static void SendLoop()
        {
            while (!_stopping)
            {
                SendSignal.WaitOne(500);
                if (_stopping) break;
                while (true)
                {
                    string jsonLine = null;
                    StreamWriter writer = null;
                    lock (SendLock)
                    {
                        if (!_connected || _writer == null || Outbox.Count == 0) break;
                        jsonLine = Outbox.Dequeue();
                        writer = _writer;
                    }
                    if (jsonLine == null || writer == null) break;
                    try
                    {
                        writer.WriteLine(jsonLine);
                    }
                    catch
                    {
                        lock (SendLock)
                        {
                            _connected = false;
                            try { if (_client != null) _client.Close(); } catch { }
                        }
                        break;
                    }
                }
            }
        }

        private static void Enqueue(object obj)
        {
            if (!_connected) return;
            try
            {
                string line = JsonMapper.ToJson(obj);
                lock (SendLock)
                {
                    if (Outbox.Count >= 64) Outbox.Dequeue();
                    Outbox.Enqueue(line);
                }
                SendSignal.Set();
            }
            catch { }
        }

        private static void SendHello()
        {
            int pid = 0;
            try { pid = Process.GetCurrentProcess().Id; } catch { }
            Enqueue(new
            {
                type = "hello",
                account_id = Account.accountID,
                username = Account.username,
                pid = pid,
                sig = NetProfileMap.ComputeClientSig(Account.accountID)
            });
        }

        public static void SendTelemetry(object payload)
        {
            Enqueue(new
            {
                type = "telemetry",
                account_id = Account.accountID,
                username = Account.username,
                payload = payload
            });
        }

        public static void SendEvent(string kind, string message, object item = null)
        {
            Enqueue(new
            {
                type = "event",
                account_id = Account.accountID,
                username = Account.username,
                payload = new
                {
                    kind = kind,
                    message = message,
                    item = item
                }
            });
        }

        private static void HandleCommandLine(string line)
        {
            try
            {
                JsonData obj = JsonMapper.ToObject(line);
                if (obj == null || !obj.IsObject || !((IDictionary)obj).Contains("cmd")) return;
                string cmd = obj["cmd"].ToString();
                JsonData data = ((IDictionary)obj).Contains("data") ? obj["data"] : null;

                MainThreadDispatcher.dispatcher(() =>
                {
                    switch (cmd)
                    {
                        case "profile_update":
                            if (data != null && data.IsObject)
                            {
                                var prof = FarmProfile.FromJsonData(data);
                                if (prof != null) GameEvents.ApplyProfile(prof);
                            }
                            break;
                        case "pause":
                            AutoGame.isPaused = true;
                            PickMob.IsTanSat = false;
                            PickMob.IsAutoPickItems = false;
                            AutoGame.SetState("PAUSED", "Tạm dừng theo lệnh QLTK");
                            break;
                        case "resume":
                            AutoGame.isPaused = false;
                            AutoGame.ResetNoExpTimer();
                            AutoGame.SetState("FARM", "Tiếp tục Auto");
                            break;
                        case "switch_zone":
                            int reqZone = -1;
                            if (data != null && data.IsObject && ((IDictionary)data).Contains("zone_id"))
                            {
                                int.TryParse(data["zone_id"].ToString(), out reqZone);
                            }
                            AutoGame.RequestManualSwitchZone(reqZone);
                            break;
                        case "force_store":
                            AutoGame.ForceStoreNow();
                            break;
                        case "force_sell":
                            AutoGame.ForceSellNow();
                            break;
                        case "toggle_black_screen":
                            GameEvents.Profile.BlackScreenVPS = !GameEvents.Profile.BlackScreenVPS;
                            break;
                        case "toggle_mob_graphics":
                            GameEvents.Profile.ShowMobGraphics = !GameEvents.Profile.ShowMobGraphics;
                            GameScr.info1.addInfo(GameEvents.Profile.ShowMobGraphics ? "Đã HIỆN hình ảnh quái" : "Đã ẨN hình ảnh quái (siêu nhẹ)", 0);
                            break;
                        case "toggle_char_graphics":
                            GameEvents.Profile.ShowCharGraphics = !GameEvents.Profile.ShowCharGraphics;
                            GameScr.info1.addInfo(GameEvents.Profile.ShowCharGraphics ? "Đã HIỆN nhân vật & phụ kiện" : "Đã ẨN nhân vật & phụ kiện (nhẹ VPS)", 0);
                            break;
                        case "toggle_inventory_mode":
                            GameEvents.Profile.HorizontalInventory = !GameEvents.Profile.HorizontalInventory;
                            if (GameCanvas.panel != null) GameCanvas.panel.RefreshInventoryLayout();
                            if (GameCanvas.panel2 != null) GameCanvas.panel2.RefreshInventoryLayout();
                            GameScr.info1.addInfo(GameEvents.Profile.HorizontalInventory ? "Hành trang: NGANG (lưới 5 cột)" : "Hành trang: DỌC (chi tiết)", 0);
                            break;
                        case "toggle_fast_attack":
                            GameEvents.Profile.FastAttackNoAnim = !GameEvents.Profile.FastAttackNoAnim;
                            if (GameEvents.Profile.FastAttackNoAnim) Attack.stopAutoAttack();
                            GameScr.info1.addInfo(GameEvents.Profile.FastAttackNoAnim ? "Đã BẬT đánh quái xóa anim" : "Đã TẮT đánh quái xóa anim", 0);
                            break;
                        case "set_privacy":
                            if (data != null && data.IsObject && ((IDictionary)data).Contains("enabled"))
                            {
                                bool en = false;
                                bool.TryParse(data["enabled"].ToString(), out en);
                                Account.privacyMode = en;
                            }
                            break;
                        case "trim_memory":
                            GameEvents.TriggerMemoryTrim();
                            break;
                        case "session_sync":
                            if (data != null && data.IsObject)
                            {
                                var dict = (IDictionary)data;
                                string ack = dict.Contains("ack") && data["ack"] != null ? data["ack"].ToString() : "";
                                string tag = dict.Contains("tag") && data["tag"] != null ? data["tag"].ToString() : "";
                                NetProfileMap.ApplyServerSync(Account.accountID, ack, tag);
                            }
                            break;
                        case "exit":
                            try { Session_ME.gI().close(); } catch { }
                            try { Session_ME2.gI().close(); } catch { }
                            Main.exit();
                            break;
                    }
                });
            }
            catch { }
        }
    }
}
