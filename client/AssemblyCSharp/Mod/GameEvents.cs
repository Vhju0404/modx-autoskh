using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssemblyCSharp.Mod.Xmap;
using UnityEngine;
using static AssemblyCSharp.Mod.LoadData;

namespace AssemblyCSharp.Mod
{
    /// <summary>
    /// Sự kiện game, bộ lọc SKH/Sao 3 lớp (activeCard + 102/107), theo dõi Mầm SKH & % May Mắn,
    /// Paint cached siêu nhẹ + Màn đen VPS và Telemetry NDJSON.
    /// </summary>
    public static class GameEvents
    {
        public static FarmProfile Profile = new FarmProfile();
        public static string IpConnect = "";

        public const short ID_ICON_ITEM_TDLT = 4387;
        public const short ID_ICON_ITEM_COBONLA = 13618;
        public const short ID_ICON_ITEM_VE_RIENG_TU = 13948;
        public const int ID_ITEM_COBONLA = 1635;
        public const int ID_ITEM_LOA_LVT = 1653;
        public const int ID_ITEM_TITLE_FAST_HAND = 1673;
        public const int PART_TITLE_FAST_HAND = 254;

        private static long _setDeadlineMs = 0;
        private static readonly Regex DeadlineRegex = new Regex(@"(\d{4})/(\d{1,2})/(\d{1,2})[^0-9]+(\d{1,2}):(\d{2})(?::(\d{2}))?");
        private static readonly Regex MaintRemainRegex = new Regex(@"(?:sau|còn|con|trong)\s*(\d+)\s*(giờ|gio|h\b|phút|phut|p\b|giây|giay|s\b)", RegexOptions.IgnoreCase);
        private static readonly Regex MaintAnyTimeRegex = new Regex(@"(\d+)\s*(giờ|gio|h\b|phút|phut|p\b|giây|giay|s\b)", RegexOptions.IgnoreCase);
        private static string _lastMaintNoticeText = "";
        private static long _lastMaintNoticeAt = 0;
        private static long _lastMaintTriggerAt = 0;

        private static long _lastTelemetryAt = 0;
        private static string _lastTelemetrySig = "";
        private static long _lastPaintCacheAt = 0;
        private static long _lastFsmTickAt = 0;
        private static int _appliedFps = -1;
        private static int _cachedTotalLuck = 0;
        private static string _cachedLuckDetail = "PK:0% DH:0%";
        private static string _cachedTitleName = "";
        private static string _cachedTitleRemain = "";
        private static long _titleExpiryMs = 0;
        private static int _activeTitleId = 0;
        private static int _activeTitleLuck = 0;
        private static string _activeTitleName = "";
        private static int _shopTitleLuck = 20;
        private static long _shopTitleExpiryMs = 0;
        private static int _serverDanhHieuEffId = 0;
        private static int _cachedTotalSKH = 0;
        private static int _cachedCloverBag = 0;
        private static int _cachedCloverBox = 0;
        private static int _cachedTicketIcon = 0;
        private static string _cachedTicketText = "";
        private struct SKHHudLine
        {
            public string Text;
            public bool HasAny;
            public int Total;
            public bool IsFullSet;
        }
        private static readonly List<SKHHudLine> _cachedSKHLines = new List<SKHHudLine>();
        private static bool _appliedInitialProfile = false;

        // Trạng thái tự đăng nhập lại & chặn spam khi sai mật khẩu / khóa tài khoản
        private static bool _fatalLoginBlock = false;
        private static bool _hasLoggedInOnce = false;
        private static long _nextReloginAt = 0;
        private static int _reloginAttempts = 0;
        private static long _lastGlobalTickAt = 0;
        private static long _lastLoginHbAt = 0;
        private static long _lastMemTrimAt = 0;
        private static long _lastUseTdltAt = 0;
        private static long _lastUseCloverAt = 0;
        private static long _clientInputOpenedAt = 0;
        private static string _lastDialogStatus = "";

        public static bool IsFatalLoginBlocked => _fatalLoginBlock;

        public static void TriggerMemoryTrim()
        {
            try
            {
                _lastMemTrimAt = mSystem.currentTimeMillis();
                Resources.UnloadUnusedAssets();
                GC.Collect();
            }
            catch { }
        }

        public static void ApplyProfile(FarmProfile prof)
        {
            if (prof == null) return;
            Profile = prof;

            try
            {
                Time.timeScale = System.Math.Max(1.0f, System.Math.Min(5.0f, prof.GameSpeed));
                Time.maximumDeltaTime = 0.08f;
                int bgFps = System.Math.Max(8, System.Math.Min(60, prof.TargetFPS));
                int targetFps = Account.headlessMode
                    ? System.Math.Max(6, System.Math.Min(15, bgFps))
                    : (Application.isFocused ? System.Math.Max(60, bgFps) : bgFps);
                Application.targetFrameRate = targetFps;
                _appliedFps = targetFps;
                QualitySettings.vSyncCount = 0;
                // Tắt hẳn engine âm thanh để tiết kiệm CPU & RAM tối đa khi treo VPS đa tab
                AudioListener.volume = 0f;
                AudioListener.pause = true;
            }
            catch { }

            PickMob.HpBuff = prof.AutoBeanHP;
            PickMob.MpBuff = prof.AutoBeanMP;
            PickMobController.OnProfileChanged(prof);
            if (prof.FastAttackNoAnim)
            {
                Attack.stopAutoAttack();
            }
            if (prof.AutoBuyCharm && prof.CharmIDs != null && prof.CharmIDs.Count > 0)
            {
                AutoGame.isBuyCharm = true;
            }
        }

        public static bool onSetResolution()
        {
            if (LoadData.Width > 0 && LoadData.Height > 0)
            {
                Screen.SetResolution(LoadData.Width, LoadData.Height, false);
                return true;
            }
            return false;
        }

        public static void onSessionConnecting(ref string host, ref int port)
        {
            if (!string.IsNullOrEmpty(Account.server) && Account.server.Contains(":"))
            {
                string[] parts = Account.server.Split(':');
                host = parts[0].Trim();
                int p;
                if (int.TryParse(parts[1].Trim(), out p) && p > 0) port = p;
            }
        }

        public static void onSceenDownloadDataShow()
        {
            if (GameCanvas.currentScreen == GameCanvas.serverScreen && !ServerListScreen.isGetData)
            {
                GameCanvas.serverScreen.perform(2, null);
            }
        }

        public static void onServerListScreenLoaded()
        {
            if (string.IsNullOrEmpty(Account.username) || _fatalLoginBlock) return;
            long now = mSystem.currentTimeMillis();
            if (_hasLoggedInOnce)
            {
                // Bị văng về màn chọn server sau khi đã vào trước đó -> chờ backoff sạch rồi UpdateGlobal mới đăng nhập lại
                long delay = System.Math.Min(25000L, 6500L + _reloginAttempts * 2500L);
                if (_nextReloginAt < now + 3000L) _nextReloginAt = now + delay;
                return;
            }
            _hasLoggedInOnce = true;
            _nextReloginAt = now + 18000L; // Fallback nếu gói login đầu tiên bị treo
            if (GameCanvas.loginScr == null) GameCanvas.loginScr = new LoginScr();
            GameCanvas.loginScr.switchToMe();
            Service.gI().login("", "", GameMidlet.VERSION, 0);
            GameCanvas.startWaitDlg();
        }

        public static void OnDisconnected()
        {
            if (string.IsNullOrEmpty(Account.username) || _fatalLoginBlock) return;
            long now = mSystem.currentTimeMillis();
            long delay = System.Math.Min(25000L, 6500L + _reloginAttempts * 2500L);
            _nextReloginAt = now + delay;
            _lastDialogStatus = "Mất kết nối — tự đăng nhập lại sau " + (delay / 1000L) + "s";
            SocketClient.SendEvent("login_status", _lastDialogStatus);
        }

        public static void OnDialogMessage(string info)
        {
            if (string.IsNullOrEmpty(info) || string.IsNullOrEmpty(Account.username)) return;
            string low = info.ToLower();

            bool isFatal =
                low.Contains("tạm khóa") || low.Contains("tam khoa") ||
                low.Contains("bị khóa") || low.Contains("bi khoa") ||
                low.Contains("khóa vĩnh viễn") || low.Contains("khoa vinh vien") ||
                low.Contains("sai mật khẩu") || low.Contains("sai mat khau") ||
                low.Contains("sai thông tin") || low.Contains("sai thong tin") ||
                low.Contains("tài khoản hoặc mật khẩu") || low.Contains("tai khoan hoac mat khau") ||
                low.Contains("không tồn tại") || low.Contains("khong ton tai") ||
                low.Contains("chưa đăng ký") || low.Contains("chua dang ky");

            if (isFatal)
            {
                _fatalLoginBlock = true;
                _nextReloginAt = 0;
                _lastDialogStatus = info;
                SocketClient.SendEvent("fatal_error", info);
                return;
            }

            if (CheckServerMaintenanceText(info, true))
            {
                long nowMaint = mSystem.currentTimeMillis();
                _nextReloginAt = nowMaint + 60000L;
                _lastDialogStatus = info;
                return;
            }

            bool inGame = GameCanvas.currentScreen == GameScr.instance &&
                global::Char.myCharz() != null && !string.IsNullOrEmpty(global::Char.myCharz().cName);
            if (inGame) return;

            long now = mSystem.currentTimeMillis();
            long delay = System.Math.Min(20000L, 5000L + _reloginAttempts * 2500L);
            if (low.Contains("đang đăng nhập") || low.Contains("đang được đăng nhập") || low.Contains("đang có người"))
            {
                delay = System.Math.Min(15000L, 6000L + _reloginAttempts * 2000L);
            }
            _nextReloginAt = now + delay;
            _lastDialogStatus = info + " (tự vào lại sau " + (delay / 1000L) + "s)";
            SocketClient.SendEvent("login_status", _lastDialogStatus);
        }

        private static int ParseMaintDurationSeconds(Match m)
        {
            if (m == null || !m.Success) return -1;
            int val;
            if (!int.TryParse(m.Groups[1].Value, out val)) return -1;
            string unit = m.Groups[2].Value.ToLower();
            if (unit.StartsWith("giờ") || unit.StartsWith("gio") || unit == "h") return val * 3600;
            if (unit.StartsWith("phút") || unit.StartsWith("phut") || unit == "p") return val * 60;
            return val;
        }

        /// <summary>
        /// Phân tích thông báo bảo trì từ server NRO (30p / 15p / 5p / 1p / đang bảo trì):
        /// - Còn > 1 phút -> chỉ báo log QLTK (maint_notice), tiếp tục farm tới phút cuối!
        /// - Còn <= 1 phút / sắp bảo trì / đang bảo trì -> bắn server_maintenance để QLTK đóng tab nghỉ & dò mỗi 60s!
        /// </summary>
        public static bool CheckServerMaintenanceText(string raw, bool isDialog)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            string clean = raw.Replace("\r", " ").Replace("\n", " ").Trim();
            string low = clean.ToLower();
            bool hasMaintKw =
                low.Contains("bảo trì") || low.Contains("bao tri") ||
                low.Contains("bảo dưỡng") || low.Contains("bao duong") ||
                low.Contains("đang đóng cửa") || low.Contains("dang dong cua") ||
                low.Contains("ngừng hoạt động") || low.Contains("ngung hoat dong") ||
                low.Contains("tắt máy chủ") || low.Contains("tat may chu");
            if (!hasMaintKw) return false;

            long now = mSystem.currentTimeMillis();

            // Kiểm tra mốc đếm ngược cụ thể: "sau X phút/giây", "còn X phút/giây"
            int remainSec = -1;
            Match mRemain = MaintRemainRegex.Match(low);
            if (mRemain.Success)
            {
                remainSec = ParseMaintDurationSeconds(mRemain);
            }
            else if (!isDialog)
            {
                Match mAny = MaintAnyTimeRegex.Match(low);
                if (mAny.Success)
                {
                    int idx = mAny.Index;
                    string prefix = idx > 0 ? low.Substring(System.Math.Max(0, idx - 14), System.Math.Min(14, idx)) : "";
                    if (!prefix.Contains("dự kiến") && !prefix.Contains("du kien") && !prefix.Contains("khoảng") && !prefix.Contains("khoang"))
                    {
                        remainSec = ParseMaintDurationSeconds(mAny);
                    }
                }
            }

            if (remainSec > 60 && !isDialog)
            {
                // Vẫn còn > 1 phút (vd 30p, 15p, 5p, 2p) -> chỉ log lên QLTK, giữ acc farm tiếp!
                if (clean != _lastMaintNoticeText || now - _lastMaintNoticeAt >= 15000L)
                {
                    _lastMaintNoticeText = clean;
                    _lastMaintNoticeAt = now;
                    SocketClient.SendEvent("maint_notice", clean);
                }
                return false;
            }

            bool isImmediate = isDialog ||
                (remainSec >= 0 && remainSec <= 60) ||
                low.Contains("đang bảo trì") || low.Contains("dang bao tri") ||
                low.Contains("tiến hành bảo trì") || low.Contains("tien hanh bao tri") ||
                low.Contains("sắp bảo trì") || low.Contains("sap bao tri") ||
                low.Contains("vui lòng thoát") || low.Contains("vui long thoat") ||
                low.Contains("đang đóng cửa") || low.Contains("dang dong cua") ||
                low.Contains("ngừng hoạt động") || low.Contains("ngung hoat dong") ||
                low.Contains("tắt máy chủ") || low.Contains("tat may chu");

            if (!isImmediate)
            {
                if (clean != _lastMaintNoticeText || now - _lastMaintNoticeAt >= 15000L)
                {
                    _lastMaintNoticeText = clean;
                    _lastMaintNoticeAt = now;
                    SocketClient.SendEvent("maint_notice", clean);
                }
                return false;
            }

            if (now - _lastMaintTriggerAt >= 12000L)
            {
                _lastMaintTriggerAt = now;
                string payload = (!isDialog && remainSec > 0 && remainSec <= 60) ? ("imminent|" + clean) : clean;
                SocketClient.SendEvent("server_maintenance", payload);
            }
            return true;
        }

        public static void UpdateGlobal()
        {
            try
            {
                // Tự nâng 60 FPS khi cửa sổ đang focus (kéo cửa sổ / thao tác cực mượt), hạ về TargetFPS khi chạy nền VPS; Headless luôn khóa <= 15 FPS
                int bgFps = System.Math.Max(8, System.Math.Min(60, Profile != null ? Profile.TargetFPS : 15));
                int wantFps = Account.headlessMode
                    ? System.Math.Max(6, System.Math.Min(15, bgFps))
                    : (Application.isFocused ? System.Math.Max(60, bgFps) : bgFps);
                if (wantFps != _appliedFps)
                {
                    _appliedFps = wantFps;
                    Application.targetFrameRate = wantFps;
                }

                long now = mSystem.currentTimeMillis();
                // To hop phim bi mat Ctrl+Shift+H bat xac thuc ban goc truc tiep trong game
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
                    (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                    Input.GetKeyDown(KeyCode.H))
                {
                    if (GameScr.info1 != null) GameScr.info1.addInfo(NetProfileMap.VerifyBanner(), 0);
                }

                // Xử lý hộp thoại ClientInput (Chat kênh liên vũ trụ 1653 / Mua nhiều Cỏ 4 lá / TDLT / Vé) ngay cả khi GameScr đang tạm dừng!
                if (Assets.src.g.ClientInput.instance != null && GameCanvas.currentScreen == Assets.src.g.ClientInput.instance)
                {
                    if (_clientInputOpenedAt == 0) _clientInputOpenedAt = now;
                    string inputVal;
                    if (AutoGame.TryConsumePendingClientInput(now, out inputVal))
                    {
                        _clientInputOpenedAt = 0;
                        Service.gI().sendClientInput2(inputVal);
                        GameScr.gI().switchToMe();
                        Assets.src.g.ClientInput.instance.clearScreen();
                    }
                    else if (AutoGame.isAuto && !AutoGame.isPaused && now - _clientInputOpenedAt > 6000L)
                    {
                        // Chống kẹt hộp thoại ClientInput mồ côi khi chạy 24/7
                        _clientInputOpenedAt = 0;
                        GameScr.gI().switchToMe();
                        Assets.src.g.ClientInput.instance.clearScreen();
                    }
                    return;
                }
                _clientInputOpenedAt = 0;

                if (now - _lastGlobalTickAt < 500L) return;
                _lastGlobalTickAt = now;

                if (NetProfileMap.IsPeerSyncTimedOut(now))
                {
                    AutoGame.isPaused = true;
                    PickMob.IsTanSat = false;
                    PickMob.IsAutoPickItems = false;
                    AutoGame.SetState("ERROR", "Ban mod bi chinh sua");
                }

                bool inGame = GameCanvas.currentScreen == GameScr.instance &&
                    global::Char.myCharz() != null && !string.IsNullOrEmpty(global::Char.myCharz().cName);
                if (inGame)
                {
                    _hasLoggedInOnce = true;
                    _reloginAttempts = 0;
                    _nextReloginAt = 0;
                    _lastDialogStatus = "";
                    return;
                }

                if (string.IsNullOrEmpty(Account.username) || _fatalLoginBlock) return;
                if (GameCanvas.currentScreen != GameCanvas.loginScr && GameCanvas.currentScreen != GameCanvas.serverScreen) return;
                if (GameCanvas.currentScreen == GameCanvas.serverScreen && ServerListScreen.isGetData) return;

                // Bắn nhịp tim login (tối đa 8 lần relog nội bộ; quá 8 lần dừng bắn để Go Watchdog mở lại tab sạch)
                if (_reloginAttempts < 8 && now - _lastLoginHbAt >= 10000L)
                {
                    _lastLoginHbAt = now;
                    SocketClient.SendEvent("login_heartbeat", string.IsNullOrEmpty(_lastDialogStatus) ? "Đang chờ tự đăng nhập lại..." : _lastDialogStatus);
                }

                if (GameCanvas.currentScreen == GameCanvas.loginScr && LoginScr.timeLogin > 0) return;

                if (_nextReloginAt == 0)
                {
                    _nextReloginAt = now + 8000L;
                    return;
                }

                if (now >= _nextReloginAt && _reloginAttempts < 8)
                {
                    _reloginAttempts++;
                    LoginScr.timeLogin = 0;
                    _nextReloginAt = now + System.Math.Min(30000L, 9000L + _reloginAttempts * 3000L);
                    GameCanvas.endDlg();
                    Session_ME.gI().close();
                    GameCanvas.connect();
                    if (GameCanvas.loginScr == null) GameCanvas.loginScr = new LoginScr();
                    if (GameCanvas.currentScreen != GameCanvas.loginScr) GameCanvas.loginScr.switchToMe();
                    Service.gI().login("", "", GameMidlet.VERSION, 0);
                    GameCanvas.startWaitDlg();
                    _lastDialogStatus = "Tự đăng nhập lại (lần " + _reloginAttempts + ")...";
                    SocketClient.SendEvent("login_status", _lastDialogStatus);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.Log("[MX] UpdateGlobal ERR: " + ex.Message);
            }
        }

        public static void onLogin(ref string username, ref string pass, ref sbyte type)
        {
            if (!string.IsNullOrEmpty(Account.username))
            {
                username = Account.username;
                if (username.StartsWith("User"))
                {
                    pass = string.Empty;
                    type = 1;
                }
                else
                {
                    pass = Account.password ?? string.Empty;
                }
            }
        }

        public static void HotKeys()
        {
            Pk9rXmap.HotKeys();
            if (GameCanvas.keyAsciiPress == 'b')
            {
                Profile.BlackScreenVPS = !Profile.BlackScreenVPS;
            }
            else if (GameCanvas.keyAsciiPress == 'm')
            {
                Profile.ShowMobGraphics = !Profile.ShowMobGraphics;
                GameScr.info1.addInfo(Profile.ShowMobGraphics ? "Đã HIỆN hình ảnh quái" : "Đã ẨN hình ảnh quái (siêu nhẹ)", 0);
            }
            else if (GameCanvas.keyAsciiPress == 'n')
            {
                Profile.ShowCharGraphics = !Profile.ShowCharGraphics;
                GameScr.info1.addInfo(Profile.ShowCharGraphics ? "Đã HIỆN nhân vật & phụ kiện" : "Đã ẨN nhân vật & phụ kiện (nhẹ VPS)", 0);
            }
            else if (GameCanvas.keyAsciiPress == 'g')
            {
                Profile.HorizontalInventory = !Profile.HorizontalInventory;
                if (GameCanvas.panel != null) GameCanvas.panel.RefreshInventoryLayout();
                if (GameCanvas.panel2 != null) GameCanvas.panel2.RefreshInventoryLayout();
                GameScr.info1.addInfo(Profile.HorizontalInventory ? "Hành trang: NGANG (lưới 5 cột)" : "Hành trang: DỌC (chi tiết)", 0);
            }
            else if (GameCanvas.keyAsciiPress == 'k')
            {
                Profile.FastAttackNoAnim = !Profile.FastAttackNoAnim;
                if (Profile.FastAttackNoAnim) Attack.stopAutoAttack();
                GameScr.info1.addInfo(Profile.FastAttackNoAnim ? "Đã BẬT đánh quái xóa anim" : "Đã TẮT đánh quái xóa anim", 0);
            }
            else if (GameCanvas.keyAsciiPress == 'p')
            {
                AutoGame.isPaused = !AutoGame.isPaused;
                if (AutoGame.isPaused)
                {
                    PickMob.IsTanSat = false;
                    PickMob.IsAutoPickItems = false;
                    AutoGame.SetState("PAUSED", "Tạm dừng bằng phím P");
                }
                else
                {
                    AutoGame.ResetNoExpTimer();
                }
            }
        }

        public static void InfoMe(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            CheckServerMaintenanceText(text, false);

            // Bắt thời hạn tìm set kích hoạt từ thông báo server
            string low = text.ToLower();
            if (low.Contains("kích hoạt") || low.Contains("kich hoat"))
            {
                ParseSetDeadline(text);
            }

            if (text.Contains("ít nhất 1 ô trống") || text.Contains("trang đã đầy"))
            {
                if (Profile.AutoSellTrash && CountItemsToSell() > 0) AutoGame.isSellMode = true;
            }

            // Tự xác minh kết quả nhận/đeo Danh hiệu từ thông báo server (chống lặp vô tận ở Santa!)
            if (low.Contains("danh hiệu đang được sử dụng") || low.Contains("danh hieu dang duoc su dung"))
            {
                AutoGame.OnTitleAlreadyActiveFromServer();
            }
            else if (AutoGame.isGetTitle && AutoGame.IsWaitingTitleBuyResult() &&
                (low.Contains("sử dụng danh hiệu") || low.Contains("su dung danh hieu") ||
                 low.Contains("nhận danh hiệu") || low.Contains("nhan danh hieu") ||
                 low.Contains("đã chọn danh hiệu") || low.Contains("da chon danh hieu") ||
                 low.Contains("mua thành công") || low.Contains("mua thanh cong") ||
                 low.Contains("bạn đã mua") || low.Contains("ban da mua")))
            {
                AutoGame.OnTitleActivatedFromServer();
            }

            Pk9rXmap.Info(text);
        }

        private static void ParseSetDeadline(string text)
        {
            try
            {
                Match m = DeadlineRegex.Match(text);
                if (!m.Success) return;
                int y = int.Parse(m.Groups[1].Value);
                int mo = int.Parse(m.Groups[2].Value);
                int d = int.Parse(m.Groups[3].Value);
                int h = int.Parse(m.Groups[4].Value);
                int mi = int.Parse(m.Groups[5].Value);
                int s = m.Groups[6].Success ? int.Parse(m.Groups[6].Value) : 0;
                DateTime dt = new DateTime(y, mo, d, h, mi, s, DateTimeKind.Local);
                _setDeadlineMs = (long)(dt.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            }
            catch { }
        }

        public static bool IsConMam()
        {
            return GameScr.isNewMember == 1;
        }

        public static string GetMamRemain()
        {
            if (_setDeadlineMs > 0)
            {
                long remain = _setDeadlineMs - mSystem.currentTimeMillis();
                if (remain <= 0) return "hết hạn";
                long hours = remain / 3600000L;
                long days = hours / 24L;
                long remH = hours % 24L;
                if (days > 0) return days + "n " + remH + "g";
                if (hours > 0) return remH + "g " + ((remain / 60000L) % 60L) + "p";
                return (remain / 60000L) + "p";
            }
            int dBag = FindMamHsdDays();
            return dBag > 0 ? (dBag + " ngày") : "";
        }

        private static int FindMamHsdDays()
        {
            var c = global::Char.myCharz();
            if (c == null) return -1;
            int d = ScanMamDays(c.arrItemBody);
            if (d < 0) d = ScanMamDays(c.arrItemBag);
            if (d < 0) d = ScanMamDays(c.arrItemBox);
            return d;
        }

        private static int ScanMamDays(Item[] arr)
        {
            if (arr == null) return -1;
            for (int i = 0; i < arr.Length; i++)
            {
                Item it = arr[i];
                if (it == null || it.template == null || string.IsNullOrEmpty(it.template.name)) continue;
                string nameLow = it.template.name.ToLower();
                if (!nameLow.Contains("mầm") && !nameLow.Contains("mam")) continue;
                if (it.itemOption == null) continue;
                for (int j = 0; j < it.itemOption.Length; j++)
                {
                    var opt = it.itemOption[j];
                    if (opt != null && opt.optionTemplate != null && opt.optionTemplate.name != null &&
                        opt.optionTemplate.name.ToLower().Contains("sử dụng") && opt.param >= 1)
                    {
                        return opt.param;
                    }
                }
            }
            return -1;
        }

        public static int GetLuckPercent()
        {
            int pk, dh, gohan, co;
            string tName, tRem;
            ComputeLuckAndTitle(out pk, out dh, out gohan, out co, out tName, out tRem);
            return pk + dh + gohan + co;
        }

        public static long GetTitleExpiryMs() => _titleExpiryMs;
        public static string GetTitleName() => _cachedTitleName;
        public static string GetTitleRemain() => _cachedTitleRemain;
        public static string GetLuckDetail() => _cachedLuckDetail;

        public static void RecordFastHandTitleAcquired(long now)
        {
            ConfirmFastHandTitleActive(now, false);
        }

        public static void ConfirmFastHandTitleActive(long now, bool preferShopHsd)
        {
            _activeTitleId = ID_ITEM_TITLE_FAST_HAND;
            _activeTitleName = "Tay nhanh hơn não";
            _activeTitleLuck = _shopTitleLuck > 0 ? _shopTitleLuck : 20;

            if (preferShopHsd && _shopTitleExpiryMs > now)
            {
                _titleExpiryMs = _shopTitleExpiryMs;
            }
            else if (_titleExpiryMs <= now)
            {
                _titleExpiryMs = now + 7L * 86400000L;
            }
            Account.titleExpiryMs = _titleExpiryMs;
            _cachedTitleName = _activeTitleName;
            _cachedTitleRemain = FormatRemainMs(_titleExpiryMs - now);
            _lastPaintCacheAt = 0;
            _lastTelemetryAt = 0;
        }

        public static bool HasEffChar(int effId)
        {
            var c = global::Char.myCharz();
            if (c == null || c.vEffChar == null) return false;
            for (int i = 0; i < c.vEffChar.size(); i++)
            {
                Effect e = c.vEffChar.elementAt(i) as Effect;
                if (e != null && e.effId == effId) return true;
            }
            return false;
        }

        /// <summary>
        /// Kiểm tra toàn diện Danh hiệu Tay nhanh hơn não [1673] đã đang bật hay chưa:
        /// 1. Trong arrItemBody (nếu có)
        /// 2. Qua Char.bag == 254 hoặc vEffChar chứa effId == 254 (chuẩn NRO Teamobi part=254)
        /// 3. Đã xác nhận từ Santa/InfoMe hoặc còn hạn titleExpiryMs lưu từ QLTK.
        /// </summary>
        public static bool IsFastHandTitleActive(long now)
        {
            if (HasItemInBody(ID_ITEM_TITLE_FAST_HAND)) return true;

            var c = global::Char.myCharz();
            if (c != null && (c.bag == PART_TITLE_FAST_HAND || HasEffChar(PART_TITLE_FAST_HAND)))
            {
                _activeTitleId = ID_ITEM_TITLE_FAST_HAND;
                if (string.IsNullOrEmpty(_activeTitleName)) _activeTitleName = "Tay nhanh hơn não";
                if (_activeTitleLuck <= 0) _activeTitleLuck = _shopTitleLuck > 0 ? _shopTitleLuck : 20;
                return true;
            }

            if (_titleExpiryMs == 0 && Account.titleExpiryMs > now)
            {
                _titleExpiryMs = Account.titleExpiryMs;
                _activeTitleId = ID_ITEM_TITLE_FAST_HAND;
                if (string.IsNullOrEmpty(_activeTitleName)) _activeTitleName = "Tay nhanh hơn não";
                if (_activeTitleLuck <= 0) _activeTitleLuck = 20;
            }

            return _activeTitleId == ID_ITEM_TITLE_FAST_HAND && _titleExpiryMs > now;
        }

        /// <summary>
        /// Đọc trực tiếp chỉ số May mắn (opt 236) & HSD còn lại (opt 93/63/252/64) của danh hiệu trong tab Sở hữu của Santa.
        /// </summary>
        public static bool InspectOwnedTitleInShop(int templateId, long now)
        {
            var shops = global::Char.myCharz()?.arrItemShop;
            if (shops == null || shops.Length <= 1 || shops[1] == null) return false;
            for (int i = 0; i < shops[1].Length; i++)
            {
                Item it = shops[1][i];
                if (it == null || it.template == null) continue;
                bool match = it.template.id == templateId ||
                    (!string.IsNullOrEmpty(it.template.name) && it.template.name.ToLower().Contains("tay nhanh"));
                if (!match) continue;

                int foundLuck = 0;
                long foundExp = 0;
                if (it.itemOption != null)
                {
                    for (int j = 0; j < it.itemOption.Length; j++)
                    {
                        var opt = it.itemOption[j];
                        if (opt == null || opt.optionTemplate == null) continue;
                        int oid = opt.optionTemplate.id;
                        if (oid == 236 && opt.param > 0) foundLuck += opt.param;
                        else if ((oid == 93 || oid == 63) && opt.param > 0) foundExp = now + (long)opt.param * 86400000L;
                        else if ((oid == 252 || oid == 64) && opt.param > 0) foundExp = now + (long)opt.param * 3600000L;
                    }
                }
                _shopTitleLuck = foundLuck > 0 ? foundLuck : 20;
                if (foundExp > now) _shopTitleExpiryMs = foundExp;
                return true;
            }
            return false;
        }

        public static bool HasItemInBody(int templateId)
        {
            var body = global::Char.myCharz()?.arrItemBody;
            if (body == null) return false;
            for (int i = 0; i < body.Length; i++)
            {
                if (body[i] != null && body[i].template != null && body[i].template.id == templateId) return true;
            }
            return false;
        }

        public static bool HasItemInBag(int templateId)
        {
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return false;
            for (int i = 0; i < bag.Length; i++)
            {
                if (bag[i] != null && bag[i].template != null && bag[i].template.id == templateId) return true;
            }
            return false;
        }

        /// <summary>
        /// Riêng Vé riêng tư [1825]: CHỈ kiểm tra còn vé trong hành trang hay không (KHÔNG xem thời gian còn lại),
        /// để tận dụng 1-2 phút bonus sau khi thời gian về 0 tới lúc game thực sự thu hồi vé khỏi hành trang.
        /// </summary>
        public static bool HasPrivateTicketInBag()
        {
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return false;
            for (int i = 0; i < bag.Length; i++)
            {
                Item it = bag[i];
                if (it == null || it.template == null) continue;
                if (it.template.id == 1825) return true;
                if (!string.IsNullOrEmpty(it.template.name) && it.template.name.ToLower().Contains("vé riêng tư"))
                {
                    return true;
                }
            }
            return false;
        }

        public static int FindPrivateTicketInShop(out int gemPrice)
        {
            gemPrice = 0;
            var shops = global::Char.myCharz()?.arrItemShop;
            if (shops == null) return -1;
            int fallbackId = -1;
            int fallbackPrice = 0;
            for (int tab = 0; tab < shops.Length; tab++)
            {
                if (shops[tab] == null) continue;
                for (int i = 0; i < shops[tab].Length; i++)
                {
                    Item it = shops[tab][i];
                    if (it == null || it.template == null) continue;
                    if (it.template.id == 1825 ||
                        (!string.IsNullOrEmpty(it.template.name) && it.template.name.ToLower().Contains("vé riêng tư")))
                    {
                        gemPrice = it.buyGold > 0 ? it.buyGold : 0;
                        return it.template.id;
                    }
                    if (it.template.id == 1823 && fallbackId < 0)
                    {
                        fallbackId = 1823;
                        fallbackPrice = it.buyGold > 0 ? it.buyGold : 1999;
                    }
                }
            }
            gemPrice = fallbackPrice;
            return fallbackId;
        }

        /// <summary>
        /// Quét shop Danh hiệu của NPC Santa: trả % Hoàn thành (option 220) và có nằm trong tab Sở hữu hay không.
        /// </summary>
        public static int FindTitleShopProgress(int templateId, out bool inOwnedTab)
        {
            inOwnedTab = false;
            var shops = global::Char.myCharz()?.arrItemShop;
            if (shops == null) return -1;

            // Tab 1 (Sở hữu) ưu tiên trước
            if (shops.Length > 1 && shops[1] != null)
            {
                for (int i = 0; i < shops[1].Length; i++)
                {
                    Item it = shops[1][i];
                    if (it != null && it.template != null &&
                        (it.template.id == templateId || (!string.IsNullOrEmpty(it.template.name) && it.template.name.ToLower().Contains("tay nhanh"))))
                    {
                        inOwnedTab = true;
                        return 100;
                    }
                }
            }

            // Tab 0 (Danh hiệu) -> đọc option 220 (Hoàn thành #%)
            if (shops.Length > 0 && shops[0] != null)
            {
                for (int i = 0; i < shops[0].Length; i++)
                {
                    Item it = shops[0][i];
                    if (it == null || it.template == null) continue;
                    bool match = it.template.id == templateId ||
                        (!string.IsNullOrEmpty(it.template.name) && it.template.name.ToLower().Contains("tay nhanh"));
                    if (!match) continue;
                    if (it.itemOption != null)
                    {
                        for (int j = 0; j < it.itemOption.Length; j++)
                        {
                            var opt = it.itemOption[j];
                            if (opt != null && opt.optionTemplate != null && opt.optionTemplate.id == 220)
                            {
                                return opt.param;
                            }
                        }
                    }
                    return 0;
                }
            }
            return -1;
        }

        private static string FormatRemainMs(long ms)
        {
            if (ms <= 0) return "hết hạn";
            long hours = ms / 3600000L;
            long days = hours / 24L;
            long remH = hours % 24L;
            if (days > 0) return remH > 0 ? (days + "n " + remH + "g") : (days + "n");
            if (hours > 0) return hours + "g " + ((ms / 60000L) % 60L) + "p";
            return System.Math.Max(1L, ms / 60000L) + "p";
        }

        private static void ComputeLuckAndTitle(out int pkLuck, out int titleLuck, out int gohanLuck, out int cloverLuck, out string titleName, out string titleRemain)
        {
            pkLuck = 0;
            titleLuck = 0;
            gohanLuck = 0;
            cloverLuck = 0;
            titleName = "";
            titleRemain = "";

            if (_titleExpiryMs == 0 && Account.titleExpiryMs > 0)
            {
                _titleExpiryMs = Account.titleExpiryMs;
            }

            long now = mSystem.currentTimeMillis();
            var body = global::Char.myCharz()?.arrItemBody;
            int gohanPieces = 0;
            bool hasPanVip = false;
            bool foundTitle = false;

            if (body != null)
            {
                for (int i = 0; i < body.Length; i++)
                {
                    Item it = body[i];
                    if (it == null || it.template == null) continue;

                    if (it.template.id == 1921) hasPanVip = true;
                    bool isTitle = it.template.type == 36 || it.template.id == ID_ITEM_TITLE_FAST_HAND;
                    if (isTitle)
                    {
                        foundTitle = true;
                        titleName = it.template.name ?? "Danh hiệu";
                    }

                    if (it.itemOption == null) continue;
                    int hsdDays = -1;
                    int hsdHours = -1;
                    bool hasGohanOpt = false;
                    for (int j = 0; j < it.itemOption.Length; j++)
                    {
                        var opt = it.itemOption[j];
                        if (opt == null || opt.optionTemplate == null) continue;
                        int oid = opt.optionTemplate.id;
                        string oname = opt.optionTemplate.name ?? "";
                        string low = oname.ToLower();

                        if (oid == 233 || low.Contains("set gohan")) hasGohanOpt = true;

                        if ((oid == 236 || (oid != 90 && oid != 91 && oid != 234 && (low.Contains("may mắn") || low.Contains("may man")) && low.Contains("%"))) && opt.param > 0)
                        {
                            if (isTitle) titleLuck += opt.param;
                            else pkLuck += opt.param;
                        }
                        if (isTitle)
                        {
                            if ((oid == 93 || oid == 63) && opt.param > 0) hsdDays = opt.param;
                            else if ((oid == 252 || oid == 64) && opt.param > 0) hsdHours = opt.param;
                        }
                    }
                    if (hasGohanOpt && it.template.type >= 0 && it.template.type <= 4)
                    {
                        gohanPieces++;
                    }
                    if (isTitle)
                    {
                        if (hsdDays > 0)
                        {
                            long upper = now + (long)(hsdDays + 1) * 86400000L;
                            long lower = now + (long)(hsdDays - 1) * 86400000L;
                            if (_titleExpiryMs < lower || _titleExpiryMs > upper)
                            {
                                _titleExpiryMs = now + (long)hsdDays * 86400000L;
                                Account.titleExpiryMs = _titleExpiryMs;
                            }
                            titleRemain = FormatRemainMs(_titleExpiryMs - now);
                        }
                        else if (hsdHours > 0)
                        {
                            long target = now + (long)hsdHours * 3600000L;
                            if (System.Math.Abs(_titleExpiryMs - target) > 3600000L)
                            {
                                _titleExpiryMs = target;
                                Account.titleExpiryMs = _titleExpiryMs;
                            }
                            titleRemain = FormatRemainMs(_titleExpiryMs - now);
                        }
                        else
                        {
                            titleRemain = "VV";
                        }
                    }
                }
            }

            // NRO Teamobi chính chủ đeo danh hiệu qua cmd 24 sub 2 / bag / effect (part=254 cho Tay nhanh hơn não) hoặc xác nhận từ Santa
            if (!foundTitle)
            {
                var c = global::Char.myCharz();
                bool effFastHand = _serverDanhHieuEffId == PART_TITLE_FAST_HAND ||
                                   (c != null && (c.bag == PART_TITLE_FAST_HAND || HasEffChar(PART_TITLE_FAST_HAND)));
                bool confirmedFastHand = (_activeTitleId == ID_ITEM_TITLE_FAST_HAND && _titleExpiryMs > now) ||
                                         (_titleExpiryMs > now);
                if (effFastHand || confirmedFastHand)
                {
                    foundTitle = true;
                    _activeTitleId = ID_ITEM_TITLE_FAST_HAND;
                    titleName = string.IsNullOrEmpty(_activeTitleName) ? "Tay nhanh hơn não" : _activeTitleName;
                    int dhVal = _activeTitleLuck > 0 ? _activeTitleLuck : (_shopTitleLuck > 0 ? _shopTitleLuck : 20);
                    titleLuck += dhVal;
                    if (_titleExpiryMs > now)
                    {
                        titleRemain = FormatRemainMs(_titleExpiryMs - now);
                    }
                    else
                    {
                        titleRemain = "Đang dùng";
                    }
                }
                else if (_titleExpiryMs > 0 && now >= _titleExpiryMs)
                {
                    titleRemain = "hết hạn";
                }
            }

            if (gohanPieces >= 5)
            {
                gohanLuck = 300 + (hasPanVip ? 50 : 0);
            }
            if (ItemTime.isExistItem(ID_ICON_ITEM_COBONLA))
            {
                cloverLuck = 100;
            }
        }

        public static void OnDanhHieuPacketFromServer(short effId)
        {
            if (effId <= 0) return;
            _serverDanhHieuEffId = effId;
            if (effId == PART_TITLE_FAST_HAND)
            {
                _activeTitleId = ID_ITEM_TITLE_FAST_HAND;
                if (string.IsNullOrEmpty(_activeTitleName)) _activeTitleName = "Tay nhanh hơn não";
                if (_activeTitleLuck <= 0) _activeTitleLuck = _shopTitleLuck > 0 ? _shopTitleLuck : 20;
            }
            _lastPaintCacheAt = 0;
        }

        public static int GetActiveTitleEffectId()
        {
            if (_serverDanhHieuEffId > 0) return _serverDanhHieuEffId;
            var c = global::Char.myCharz();
            if (c == null) return 0;
            if (c.arrItemBody != null)
            {
                for (int i = 0; i < c.arrItemBody.Length; i++)
                {
                    Item it = c.arrItemBody[i];
                    if (it != null && it.template != null && (it.template.type == 36 || it.template.id == ID_ITEM_TITLE_FAST_HAND))
                    {
                        if (it.template.part > 0) return it.template.part;
                        if (it.template.id == ID_ITEM_TITLE_FAST_HAND) return PART_TITLE_FAST_HAND;
                    }
                }
            }
            if (c.bag == PART_TITLE_FAST_HAND) return PART_TITLE_FAST_HAND;
            long now = mSystem.currentTimeMillis();
            if ((_activeTitleId == ID_ITEM_TITLE_FAST_HAND && _titleExpiryMs > now) || (_titleExpiryMs > now))
            {
                return PART_TITLE_FAST_HAND;
            }
            return 0;
        }

        public static void Update()
        {
            try
            {
                if (!_appliedInitialProfile && global::Char.myCharz() != null && !string.IsNullOrEmpty(global::Char.myCharz().cName))
                {
                    _appliedInitialProfile = true;
                    ApplyProfile(Profile);
                    AutoGame.isAuto = true;
                }

                Pk9rXmap.Update();
                if (!Pk9rXmap.IsXmapRunning)
                {
                    PickMob.Update();
                }

                long now = mSystem.currentTimeMillis();
                if (now - _lastFsmTickAt >= 200L)
                {
                    _lastFsmTickAt = now;
                    var myChar = global::Char.myCharz();
                    if (myChar != null && !string.IsNullOrEmpty(myChar.cName))
                    {
                        myChar.cspeed = System.Math.Max(4, Profile.CharSpeed);

                        if (AutoGame.isAuto && !AutoGame.isPaused)
                        {
                            // Tự bật TDLT có sẵn trong hành trang (có giãn nhịp 2.5s tránh spam gói); KHÔNG bao giờ tự tắt TDLT của người chơi!
                            if (Profile.IsTDLTMode && !GameScr.canAutoPlay && !ItemTime.isExistItem(ID_ICON_ITEM_TDLT) && GetTimeTDLT() > 0 && now - _lastUseTdltAt >= 2500L)
                            {
                                _lastUseTdltAt = now;
                                UseAnyTDLT();
                            }

                            // Tự dùng Cỏ 4 lá nếu có sẵn trong túi (có giãn nhịp 2.5s chống ăn đúp 2 cỏ cùng lúc!)
                            if (Profile.AutoBuyLuckyGrass && !ItemTime.isExistItem(ID_ICON_ITEM_COBONLA) && GetItemQty(1635) > 0 && now - _lastUseCloverAt >= 2500L)
                            {
                                _lastUseCloverAt = now;
                                UseItem(1635);
                            }

                            if (Profile.AutoUpPotential) AutoUpPotentialSafe();
                            AutoGame.FixPetLazy();
                            AutoGame.AutoAskClanBean();
                            AutoGame.TickFSM();

                            // Lớp 1 xả RAM định kỳ mỗi 25 phút khi đang đứng FARM yên ổn (tránh tích tụ Texture2D trên VPS)
                            if (_lastMemTrimAt == 0) _lastMemTrimAt = now;
                            else if (AutoGame.fsmState == "FARM" && now - _lastMemTrimAt >= 1500000L)
                            {
                                TriggerMemoryTrim();
                            }
                        }

                        PushTelemetryIfNeeded();
                    }
                }
            }
            catch { }
        }

        // --- Bộ lọc Đồ SKH & Đồ Sao 3 lớp (tuyệt đối không bán nhầm!) ---

        // Ánh xạ chuẩn Option ID từ NRitem0 NRO chính (127..135, 233, 237, 241, 245, 254)
        private static string SetNameFromOptionId(int optId)
        {
            switch (optId)
            {
                case 127: return "Thiên Xin Hăng";
                case 128: return "Kirin";
                case 129: return "Songoku";
                case 130: return "Picolo";
                case 131: return "Ốc Tiêu";
                case 132: return "Pikkoro Daimao";
                case 133: return "Kakarot";
                case 134: return "Cadic";
                case 135: return "Nappa";
                case 233: return "Gohan";
                case 237: return "Nail Chiến Binh Namếc";
                case 241: return "Cadic M";
                case 245: return "Thần Vũ Trụ Kaio";
                case 254: return "Thần Hủy Diệt Champa";
                default: return "";
            }
        }

        public static bool IsSKH(Item item)
        {
            if (item == null || item.itemOption == null || item.template == null || item.template.type < 0 || item.template.type > 4) return false;
            for (int i = 0; i < item.itemOption.Length; i++)
            {
                var opt = item.itemOption[i];
                if (opt == null) continue;
                if (opt.activeCard != 0) return true;
                if (opt.optionTemplate == null) continue;
                if (!string.IsNullOrEmpty(SetNameFromOptionId(opt.optionTemplate.id))) return true;
                if (string.IsNullOrEmpty(opt.optionTemplate.name)) continue;
                string name = opt.optionTemplate.name;
                if (name.StartsWith("$") || name.StartsWith("Set ", StringComparison.OrdinalIgnoreCase) ||
                    name.IndexOf("kích hoạt", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            return !string.IsNullOrEmpty(ExtractSKHSetName(item));
        }

        public static string ExtractSKHSetName(Item item)
        {
            if (item == null || item.itemOption == null) return "";
            // Tầng 1 (chuẩn tuyệt đối): Bắt thẳng theo Option ID chính chủ NRitem0
            for (int i = 0; i < item.itemOption.Length; i++)
            {
                var opt = item.itemOption[i];
                if (opt != null && opt.optionTemplate != null)
                {
                    string byId = SetNameFromOptionId(opt.optionTemplate.id);
                    if (!string.IsNullOrEmpty(byId)) return byId;
                }
            }
            // Tầng 2: Dòng option có chữ "Set ..."
            for (int i = 0; i < item.itemOption.Length; i++)
            {
                var opt = item.itemOption[i];
                if (opt == null) continue;
                string s = opt.getOptionString();
                if (string.IsNullOrEmpty(s)) continue;
                int idx = s.IndexOf("Set ", StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    string sub = s.Substring(idx + 4);
                    int plusIdx = sub.IndexOf(" +");
                    if (plusIdx >= 0) sub = sub.Substring(0, plusIdx);
                    int parenIdx = sub.IndexOf('(');
                    if (parenIdx >= 0) sub = sub.Substring(0, parenIdx);
                    return NormalizeSetName(sub.Trim());
                }
            }
            // Tầng 3: Fallback dò trực tiếp tên 14 set chuẩn trong mọi dòng option
            for (int i = 0; i < item.itemOption.Length; i++)
            {
                var opt = item.itemOption[i];
                if (opt == null) continue;
                string s = opt.getOptionString();
                if (string.IsNullOrEmpty(s)) continue;
                string known = MatchKnownSetName(s);
                if (!string.IsNullOrEmpty(known)) return known;
            }
            return "";
        }

        private static string MatchKnownSetName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string low = raw.ToLower().Trim();
            // Kiểm tra tên dài / có hậu tố trước để không match nhầm
            if (low.Contains("daimao")) return "Pikkoro Daimao";
            if (low.Contains("picolo") || low.Contains("pikkoro")) return "Picolo";
            if (low.Contains("ốc tiêu") || low.Contains("oc tieu")) return "Ốc Tiêu";
            if (low.Contains("nail")) return "Nail Chiến Binh Namếc";
            if (low.Contains("vũ trụ kaio") || low.Contains("vu tru kaio") || low.Contains("set kaio") || low == "kaio") return "Thần Vũ Trụ Kaio";
            if (low.Contains("champa")) return "Thần Hủy Diệt Champa";
            if (low.Contains("gohan")) return "Gohan";
            if (low.Contains("cađic m") || low.Contains("cadic m") || low.Contains("ca đíc m") || low.Contains("ca dic m")) return "Cadic M";
            if (low.Contains("ca đíc") || low.Contains("cađic") || low.Contains("cadic") || low.Contains("ca dic")) return "Cadic";
            if (low.Contains("kirin")) return "Kirin";
            if (low.Contains("sôngôku") || low.Contains("songoku") || low.Contains("son goku") || low.Contains("sgk")) return "Songoku";
            if (low.Contains("thên xin") || low.Contains("thiên xin") || low.Contains("thien xin") || low.Contains("then xin") || low.Contains("txh")) return "Thiên Xin Hăng";
            if (low.Contains("kakarot") || low.Contains("ca ca rot")) return "Kakarot";
            if (low.Contains("nappa")) return "Nappa";
            return "";
        }

        public static string NormalizeSetName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string known = MatchKnownSetName(raw);
            if (!string.IsNullOrEmpty(known)) return known;
            string low = raw.ToLower().Trim();
            if (low.Contains("ốc")) return "Ốc Tiêu";
            if (low.Contains("sông")) return "Songoku";
            if (low.Contains("thiên") || low.Contains("thien") || low.Contains("thên")) return "Thiên Xin Hăng";
            if (low.Contains("kaio")) return "Thần Vũ Trụ Kaio";
            return raw.Trim();
        }

        /// <summary>
        /// Kiểm tra cả option 107 (lỗ sao pha lê) lẫn 102 (sao đã ép) — chuẩn ModItemShop!
        /// </summary>
        public static int GetStars(Item item)
        {
            if (item == null || item.itemOption == null) return 0;
            int maxStar = 0;
            for (int i = 0; i < item.itemOption.Length; i++)
            {
                var opt = item.itemOption[i];
                if (opt != null && opt.optionTemplate != null && (opt.optionTemplate.id == 107 || opt.optionTemplate.id == 102))
                {
                    if (opt.param > maxStar) maxStar = opt.param;
                }
            }
            return maxStar;
        }

        /// <summary>
        /// Tóm tắt nhanh số sao pha lê (102/107), cấp cường hóa (72), HSD và các dòng ép (HP/KI/SĐ/TNSM/HHP/HKI/PST/GST/SKH...)
        /// Chỉ chạy khi người dùng đang mở bảng Hành trang / Rương nên tốn 0% CPU lúc treo auto!
        /// </summary>
        public static void GetItemOverlayInfo(Item item, out int filledStars, out int totalStars, out int upgradeLvl, out string hsdTag, out string enchantSummary, out bool isSkh)
        {
            filledStars = 0;
            totalStars = 0;
            upgradeLvl = 0;
            hsdTag = "";
            enchantSummary = "";
            isSkh = false;
            if (item == null || item.template == null || item.itemOption == null) return;

            int firstOptId = (item.itemOption.Length > 0 && item.itemOption[0] != null && item.itemOption[0].optionTemplate != null)
                ? item.itemOption[0].optionTemplate.id : -1;
            var parts = new List<string>(4);

            for (int i = 0; i < item.itemOption.Length; i++)
            {
                var opt = item.itemOption[i];
                if (opt == null || opt.optionTemplate == null) continue;
                int oid = opt.optionTemplate.id;
                int val = opt.param;

                if (oid == 102 && val > filledStars) filledStars = val;
                else if (oid == 107 && val > totalStars) totalStars = val;
                else if (oid == 72 && val > upgradeLvl) upgradeLvl = val;
                else if ((oid == 93 || oid == 63) && val > 0 && string.IsNullOrEmpty(hsdTag)) hsdTag = val + "D";
                else if ((oid == 252 || oid == 64) && val > 0 && string.IsNullOrEmpty(hsdTag)) hsdTag = val + "H";
                else if (val > 0 && i > 0)
                {
                    // Bỏ qua nếu trùng dòng đầu tiên đã hiện bên trái
                    if (oid == firstOptId) continue;
                    string tag = null;
                    switch (oid)
                    {
                        case 77: tag = val + "% HP"; break;
                        case 103: tag = val + "% KI"; break;
                        case 50: tag = val + "% SD"; break;
                        case 101: tag = val + "% TN"; break;
                        case 100: tag = val + "% Vang"; break;
                        case 95: tag = val + "% BS"; break;
                        case 96: tag = val + "% MS"; break;
                        case 97: tag = val + "% PS"; break;
                        case 94: tag = val + "% GST"; break;
                        case 98: tag = val + "% XGC"; break;
                        case 99: tag = val + "% XGCC"; break;
                        case 108: tag = val + "% Ne"; break;
                        case 5: tag = val + "% SDCM"; break;
                        case 14: if (item.template.type != 4) tag = val + "% CM"; break;
                        case 19: tag = val + "% Quai"; break;
                        case 236: tag = val + "% MM"; break;
                    }
                    if (tag != null && parts.Count < 3) parts.Add(tag);
                }
            }

            if (item.template.type >= 0 && item.template.type <= 4 && IsSKH(item))
            {
                isSkh = true;
                string sName = ExtractSKHSetName(item);
                string skhTag = "SKH " + ShortSetLabel(string.IsNullOrEmpty(sName) ? "SKH" : sName);
                parts.Insert(0, skhTag);
            }

            if (parts.Count > 0)
            {
                enchantSummary = string.Join(", ", parts.ToArray());
            }
        }

        public static bool IsWantedSKH(Item item)
        {
            if (!IsSKH(item)) return false;
            if (Profile.KeepAllSKH) return true;
            if (Profile.KeepSKHSets == null || Profile.KeepSKHSets.Count == 0) return true;
            string setName = ExtractSKHSetName(item);
            if (string.IsNullOrEmpty(setName)) return true;
            return Profile.KeepSKHSets.Any(s => string.Equals(s, setName, StringComparison.OrdinalIgnoreCase));
        }

        public static bool ShouldStoreItem(Item item)
        {
            if (item == null || item.template == null || item.template.type > 4) return false;
            if (IsWantedSKH(item)) return true;
            int stars = GetStars(item);
            if (Profile.MinKeepStars > 0 && stars >= Profile.MinKeepStars) return true;
            return false;
        }

        public static bool ShouldSellItem(Item item)
        {
            if (item == null || item.template == null) return false;
            if (ShouldStoreItem(item)) return false;
            if (Profile.SellItemIDs != null && Profile.SellItemIDs.Contains(item.template.id)) return true;
            if (item.template.type >= 0 && item.template.type <= 4) return true;
            return false;
        }

        public static bool ItemMe(int itemID)
        {
            if (Profile.PickAll) return true;
            return Profile.PickItemIDs != null && Profile.PickItemIDs.Contains(itemID);
        }

        public static int CountItemsToStore()
        {
            int count = 0;
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return 0;
            for (int i = 0; i < bag.Length; i++)
                if (bag[i] != null && ShouldStoreItem(bag[i])) count++;
            return count;
        }

        public static int CountItemsToSell()
        {
            int count = 0;
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return 0;
            for (int i = 0; i < bag.Length; i++)
                if (bag[i] != null && ShouldSellItem(bag[i])) count++;
            return count;
        }

        public static int GetBagFree()
        {
            int free = 0;
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return 0;
            for (int i = 0; i < bag.Length; i++)
                if (bag[i] == null) free++;
            return free;
        }

        public static bool IsBoxFull()
        {
            var box = global::Char.myCharz()?.arrItemBox;
            if (box == null) return false;
            for (int i = 0; i < box.Length; i++)
                if (box[i] == null) return false;
            return true;
        }

        public static int GetBagUsed()
        {
            int n = 0;
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return 0;
            for (int i = 0; i < bag.Length; i++) if (bag[i] != null) n++;
            return n;
        }

        public static int GetBoxUsed()
        {
            int n = 0;
            var box = global::Char.myCharz()?.arrItemBox;
            if (box == null) return 0;
            for (int i = 0; i < box.Length; i++) if (box[i] != null) n++;
            return n;
        }

        public static int GetBoxMax() => (global::Char.myCharz() != null && global::Char.myCharz().arrItemBox != null) ? global::Char.myCharz().arrItemBox.Length : 20;
        public static int MyHome() => global::Char.myCharz().cgender + 21;

        public static int CountOtherPlayersInZone()
        {
            var me = global::Char.myCharz();
            int n = 0;
            if (GameScr.vCharInMap == null) return 0;
            for (int i = 0; i < GameScr.vCharInMap.size(); i++)
            {
                global::Char ch = (global::Char)GameScr.vCharInMap.elementAt(i);
                if (ch != null && ch != me && ch.charID > 0) n++;
            }
            return n;
        }

        public static int GetCurrentZonePlayers()
        {
            return CountOtherPlayersInZone() + 1;
        }

        public static int GetCurrentZoneMax()
        {
            var scr = GameScr.gI();
            if (scr != null && scr.maxPlayer != null && TileMap.zoneID >= 0 && TileMap.zoneID < scr.maxPlayer.Length && scr.maxPlayer[TileMap.zoneID] > 0)
                return scr.maxPlayer[TileMap.zoneID];
            return 15;
        }

        public static Npc FindNpcByNameOrId(short fallbackId, params string[] keywords)
        {
            if (GameScr.vNpc != null && keywords != null && keywords.Length > 0)
            {
                for (int i = 0; i < GameScr.vNpc.size(); i++)
                {
                    Npc n = (Npc)GameScr.vNpc.elementAt(i);
                    if (n == null || n.template == null || string.IsNullOrEmpty(n.template.name)) continue;
                    string low = n.template.name.ToLower();
                    for (int k = 0; k < keywords.Length; k++)
                    {
                        if (low.Contains(keywords[k])) return n;
                    }
                }
            }
            return GameScr.findNPCInMap(fallbackId);
        }

        public static bool ShopHasItem(int templateId)
        {
            var shops = global::Char.myCharz().arrItemShop;
            if (shops == null) return false;
            for (int tab = 0; tab < shops.Length; tab++)
            {
                if (shops[tab] == null) continue;
                for (int i = 0; i < shops[tab].Length; i++)
                {
                    Item it = shops[tab][i];
                    if (it != null && it.template != null && it.template.id == templateId) return true;
                }
            }
            return false;
        }

        public static bool HasEnoughGem(int price)
        {
            int need = price + System.Math.Max(0, Profile.MinGemKeep);
            var c = global::Char.myCharz();
            if (c == null) return false;
            return c.luong >= need || c.luongKhoa >= need;
        }

        public static int MaxAffordableQty(int unitPrice)
        {
            if (unitPrice <= 0) return 1;
            var c = global::Char.myCharz();
            if (c == null) return 0;
            int keep = System.Math.Max(0, Profile.MinGemKeep);
            int maxWallet = System.Math.Max(c.luong, c.luongKhoa);
            int spendable = maxWallet - keep;
            return spendable >= unitPrice ? (spendable / unitPrice) : 0;
        }

        public static int GetTDLTGemPrice(int itemId)
        {
            if (itemId == 521) return 1;
            if (itemId == 1523) return 9;
            if (itemId == 1524) return 22;
            return 25;
        }

        public static int GetTimeTDLT()
        {
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return 0;
            for (int i = 0; i < bag.Length; i++)
            {
                Item item = bag[i];
                if (item != null && item.template != null && (item.template.id == 521 || item.template.id == 1523 || item.template.id == 1524) &&
                    item.itemOption != null && item.itemOption.Length > 0 && item.itemOption[0] != null)
                {
                    if (item.itemOption[0].param > 0) return item.itemOption[0].param;
                }
            }
            return 0;
        }

        public static int GetItemQty(int itemId)
        {
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return 0;
            int total = 0;
            for (int i = 0; i < bag.Length; i++)
            {
                if (bag[i] != null && bag[i].template != null && bag[i].template.id == itemId)
                    total += System.Math.Max(1, bag[i].quantity);
            }
            return total;
        }

        public static void UseItem(int itemId)
        {
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return;
            for (int i = 0; i < bag.Length; i++)
            {
                if (bag[i] != null && bag[i].template != null && bag[i].template.id == itemId)
                {
                    Service.gI().useItem(0, 1, (sbyte)bag[i].indexUI, -1);
                    return;
                }
            }
        }

        public static void _useNhoTim()
        {
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return;
            for (int i = 0; i < bag.Length; i++)
            {
                if (bag[i] != null && bag[i].template != null && (bag[i].template.id == 211 || bag[i].template.id == 212))
                {
                    Service.gI().useItem(0, 1, (sbyte)bag[i].indexUI, -1);
                    return;
                }
            }
        }

        public static void GoHome()
        {
            PickMob.IsTanSat = false;
            PickMob.IsAutoPickItems = false;
            int villageMap = global::Char.myCharz().cgender * 7;
            if (TileMap.mapID != villageMap)
            {
                XmapController.StartRunToMapId(villageMap);
                return;
            }
            if (PopUp.vPopups.size() > 1)
            {
                ((PopUp)PopUp.vPopups.elementAt(1)).command.performAction();
            }
        }

        public static void TeleToNpc(Npc npc)
        {
            global::Char.myCharz().currentMovePoint = new MovePoint(npc.cx, npc.ySd - npc.ySd % 24);
            global::Char.myCharz().npcFocus = npc;
        }

        public static void UseAnyTDLT()
        {
            var bag = global::Char.myCharz().arrItemBag;
            if (bag == null) return;
            for (int i = 0; i < bag.Length; i++)
            {
                Item it = bag[i];
                if (it != null && it.template != null &&
                    (it.template.id == 521 || it.template.id == 1523 || it.template.id == 1524) &&
                    it.itemOption != null && it.itemOption.Length > 0 && it.itemOption[0].param > 0)
                {
                    Service.gI().useItem(0, 1, (sbyte)it.indexUI, -1);
                    return;
                }
            }
        }

        public static void TeleChar(int x, int y)
        {
            var c = global::Char.myCharz();
            if (c == null) return;
            c.currentMovePoint = null;
            if (c.vMovePoints != null) c.vMovePoints.removeAllElements();
            c.cvx = 0;
            c.cvy = 0;
            c.cx = x;
            c.cy = y;
            Service.gI().charMove();
            if (!GameScr.canAutoPlay)
            {
                c.cy = y + 1;
                Service.gI().charMove();
                c.cy = y;
                Service.gI().charMove();
            }
        }

        public static int GetYGround(int x, int fallbackY)
        {
            if (TileMap.pxh <= 0) return fallbackY;
            int y = 50;
            while (y < TileMap.pxh)
            {
                if (TileMap.tileTypeAt(x, y, 2))
                {
                    return y - y % 24;
                }
                y += 24;
            }
            return fallbackY;
        }

        public static int GetNextCharmToBuy()
        {
            var shops = global::Char.myCharz().arrItemShop;
            if (shops == null || shops.Length == 0 || shops[0] == null || Profile.CharmIDs == null) return -1;
            for (int i = 0; i < shops[0].Length; i++)
            {
                Item it = shops[0][i];
                if (it == null || !Profile.CharmIDs.Contains(it.template.id)) continue;
                bool active = false;
                if (it.itemOption != null)
                {
                    for (int j = 0; j < it.itemOption.Length; j++)
                    {
                        if (it.itemOption[j].optionTemplate.name.Contains("Còn")) { active = true; break; }
                    }
                }
                if (!active) return it.template.id;
            }
            return -1;
        }

        private static void AutoUpPotentialSafe()
        {
            var c = global::Char.myCharz();
            long tn = c.cTiemNang;
            if (Profile.TargetHP > 0 && c.cHPGoc < Profile.TargetHP)
            {
                long cost = 10L * (2L * (c.cHPGoc + 1000) + 180) / 2;
                if (tn >= cost) { Service.gI().upPotential(0, 10); return; }
            }
            if (Profile.TargetKI > 0 && c.cMPGoc < Profile.TargetKI)
            {
                long cost = 10L * (2L * (c.cMPGoc + 1000) + 180) / 2;
                if (tn >= cost) { Service.gI().upPotential(1, 10); return; }
            }
            if (Profile.TargetSD > 0 && c.cDamGoc < Profile.TargetSD)
            {
                long cost = 10L * (2L * c.cDamGoc + 9) / 2 * System.Math.Max(1, (int)c.expForOneAdd);
                if (tn >= cost) { Service.gI().upPotential(2, 10); return; }
            }
        }

        // --- Telemetry NDJSON đẩy về Go Manager ---

        private static List<object> CollectLootSnapshot()
        {
            var list = new List<object>();
            CollectFromArray(global::Char.myCharz().arrItemBox, "box", list);
            CollectFromArray(global::Char.myCharz().arrItemBag, "bag", list);
            return list;
        }

        private static void CollectFromArray(Item[] arr, string loc, List<object> dst)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                Item it = arr[i];
                if (it == null || it.template == null || it.template.type > 4) continue;
                bool skh = IsSKH(it);
                int stars = GetStars(it);
                if (!skh && stars <= 0) continue;
                dst.Add(new
                {
                    template_id = it.template.id,
                    name = it.template.name,
                    type = it.template.type,
                    is_skh = skh,
                    set_name = skh ? ExtractSKHSetName(it) : "",
                    stars = stars,
                    location = loc
                });
            }
        }

        private static void PushTelemetryIfNeeded()
        {
            if (!SocketClient.IsConnected) return;
            long now = mSystem.currentTimeMillis();
            var c = global::Char.myCharz();
            int bagUsed = GetBagUsed();
            int boxUsed = GetBoxUsed();
            string mapName = (TileMap.mapNames != null && TileMap.mapID >= 0 && TileMap.mapID < TileMap.mapNames.Length)
                ? TileMap.mapNames[TileMap.mapID] : ("Map " + TileMap.mapID);

            string mamRemain = GetMamRemain();
            RefreshPrivateTicketCache(c.arrItemBag);
            int pkLuck, dhLuck, gohanLuck, coLuck;
            string tName, tRem;
            ComputeLuckAndTitle(out pkLuck, out dhLuck, out gohanLuck, out coLuck, out tName, out tRem);
            int luckPct = pkLuck + dhLuck + gohanLuck + coLuck;
            string luckDet = BuildLuckDetailString(pkLuck, dhLuck, gohanLuck, coLuck);
            var lootSnap = CollectLootSnapshot();
            bool inPrivMap = AutoGame.IsInPrivateMap();
            int lastExpSec = AutoGame.GetLastExpIdleSec(now);
            int hitsPerMin = AutoGame.GetExpHitsLastMinute(now);
            int totalHits = AutoGame.GetTotalExpHits();
            string targetMob = "";
            if (c.mobFocus != null && c.mobFocus.hp > 0 && c.mobFocus.status != 0 && c.mobFocus.status != 1)
            {
                string mName = (c.mobFocus.getTemplate() != null && !string.IsNullOrEmpty(c.mobFocus.getTemplate().name))
                    ? c.mobFocus.getTemplate().name : ("Mob#" + c.mobFocus.mobId);
                targetMob = mName + " (" + c.mobFocus.hp + "/" + c.mobFocus.maxHp + ")";
            }

            string activeSkill = PickMobController.GetActiveSkillLabel(c);
            string sig = string.Concat(TileMap.mapID, "|", TileMap.zoneID, "|", inPrivMap ? "1" : "0", "|", AutoGame.fsmState, "|",
                AutoGame.isStatus, "|", activeSkill, "|", bagUsed, "|", boxUsed, "|", c.luong, "|", c.luongKhoa, "|",
                IsConMam() ? "1" : "0", "|", mamRemain, "|", _cachedTicketText, "|", luckPct, "|", tName, "|", tRem, "|", lootSnap.Count, "|",
                AutoGame.isPaused ? "1" : "0", "|", Profile.BlackScreenVPS ? "1" : "0");

            if (sig == _lastTelemetrySig && now - _lastTelemetryAt < 3000) return;

            _lastTelemetrySig = sig;
            _lastTelemetryAt = now;

            SocketClient.SendTelemetry(new
            {
                char_name = c.cName,
                gender = c.cgender,
                map_id = TileMap.mapID,
                map_name = mapName,
                zone_id = TileMap.zoneID,
                zone_players = GetCurrentZonePlayers(),
                zone_max = GetCurrentZoneMax(),
                cx = c.cx,
                cy = c.cy,
                in_private_map = inPrivMap,
                ticket_remain = _cachedTicketText,
                last_exp_sec = lastExpSec,
                hits_per_min = hitsPerMin,
                total_hits = totalHits,
                target_mob = targetMob,
                active_skill = activeSkill,
                hp = c.cHP,
                hp_max = c.cHPFull,
                mp = c.cMP,
                mp_max = c.cMPFull,
                power = c.cPower,
                gold = c.xu,
                gem = c.luong,
                gem_lock = c.luongKhoa,
                tdlt_minutes = GetTimeTDLT(),
                has_tdlt_active = ItemTime.isExistItem(ID_ICON_ITEM_TDLT),
                has_lucky_grass = ItemTime.isExistItem(ID_ICON_ITEM_COBONLA),
                is_new_member = IsConMam(),
                mam_remain = mamRemain,
                luck_percent = luckPct,
                luck_detail = luckDet,
                title_name = tName,
                title_remain = tRem,
                title_expiry_ms = _titleExpiryMs,
                bag_used = bagUsed,
                bag_max = c.arrItemBag != null ? c.arrItemBag.Length : 20,
                box_used = boxUsed,
                box_max = GetBoxMax(),
                fsm_state = AutoGame.fsmState,
                status_text = AutoGame.isStatus,
                paused = AutoGame.isPaused,
                black_screen = Profile.BlackScreenVPS,
                active_proxy = Account.proxy == "home" ? "Home" : IpConnect,
                loot = lootSnap
            });
        }

        // --- Paint Siêu Nhẹ (Cached + Màn Đen VPS + Bảng SKH 0-0-0-0-0 chuẩn Áo-Quần-Găng-Giày-Rd) ---

        private static string ShortSetLabel(string canonical)
        {
            if (canonical == "Songoku") return "SGK";
            if (canonical == "Kirin") return "Kirin";
            if (canonical == "Thiên Xin Hăng") return "TXH";
            if (canonical == "Thần Vũ Trụ Kaio") return "Kaio";
            if (canonical == "Ốc Tiêu") return "OcTieu";
            if (canonical == "Pikkoro Daimao") return "Daimao";
            if (canonical == "Picolo") return "Picolo";
            if (canonical == "Nail Chiến Binh Namếc") return "Nail";
            if (canonical == "Kakarot") return "Kakarot";
            if (canonical == "Cadic M") return "CadicM";
            if (canonical == "Cadic") return "Cadic";
            if (canonical == "Nappa") return "Nappa";
            if (canonical == "Gohan") return "Gohan";
            if (canonical == "Thần Hủy Diệt Champa") return "Champa";
            return canonical;
        }

        private static string BuildLuckDetailString(int pk, int dh, int gohan, int co)
        {
            var parts = new List<string>();
            parts.Add("PK:" + pk + "%");
            parts.Add("DH:" + dh + "%");
            if (gohan > 0) parts.Add("Gohan:" + gohan + "%");
            if (co > 0) parts.Add("Co:" + co + "%");
            return string.Join(" ", parts.ToArray());
        }

        private static string ShortTitleLabel(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "Chua co";
            string low = raw.ToLower();
            if (low.Contains("tay nhanh")) return "Tay nhanh";
            if (low.Contains("fan cứng") || low.Contains("fan cung")) return "Fan cung";
            return raw.Length > 14 ? raw.Substring(0, 14) : raw;
        }

        private static void RebuildPaintCacheIfNeeded()
        {
            long now = mSystem.currentTimeMillis();
            if (now - _lastPaintCacheAt < 1200) return;
            _lastPaintCacheAt = now;

            int pk, dh, gohan, co;
            string tName, tRem;
            ComputeLuckAndTitle(out pk, out dh, out gohan, out co, out tName, out tRem);
            _cachedTotalLuck = pk + dh + gohan + co;
            _cachedLuckDetail = BuildLuckDetailString(pk, dh, gohan, co);
            _cachedTitleName = tName;
            _cachedTitleRemain = tRem;

            _cachedSKHLines.Clear();
            var c = global::Char.myCharz();
            if (c == null) return;

            var orderedSets = new List<string>();
            var countsBySet = new Dictionary<string, int[]>();

            // Luôn hiện đủ 4 bộ SKH theo hành tinh + 2 bộ dùng chung (Gohan, Champa) ở dạng 0-0-0-0-0
            string[] planetSets;
            if (c.cgender == 1)
                planetSets = new string[] { "Ốc Tiêu", "Pikkoro Daimao", "Picolo", "Nail Chiến Binh Namếc", "Gohan", "Thần Hủy Diệt Champa" };
            else if (c.cgender == 2)
                planetSets = new string[] { "Kakarot", "Cadic M", "Cadic", "Nappa", "Gohan", "Thần Hủy Diệt Champa" };
            else
                planetSets = new string[] { "Songoku", "Kirin", "Thiên Xin Hăng", "Thần Vũ Trụ Kaio", "Gohan", "Thần Hủy Diệt Champa" };

            for (int i = 0; i < planetSets.Length; i++)
            {
                orderedSets.Add(planetSets[i]);
                countsBySet[planetSets[i]] = new int[5];
            }

            ScanPaintArray(c.arrItemBox, orderedSets, countsBySet);
            ScanPaintArray(c.arrItemBag, orderedSets, countsBySet);

            int totalSkh = 0;
            for (int i = 0; i < orderedSets.Count; i++)
            {
                string setKey = orderedSets[i];
                int[] arr = countsBySet[setKey];
                int sum = arr[0] + arr[1] + arr[2] + arr[3] + arr[4];
                totalSkh += sum;
                bool fullSet = arr[0] > 0 && arr[1] > 0 && arr[2] > 0 && arr[3] > 0 && arr[4] > 0;
                string code = arr[0] + "-" + arr[1] + "-" + arr[2] + "-" + arr[3] + "-" + arr[4];
                _cachedSKHLines.Add(new SKHHudLine
                {
                    Text = ShortSetLabel(setKey) + ": " + code,
                    HasAny = sum > 0,
                    Total = sum,
                    IsFullSet = fullSet
                });
            }
            _cachedTotalSKH = totalSkh;
            _cachedCloverBag = CountCloverInArray(c.arrItemBag);
            _cachedCloverBox = CountCloverInArray(c.arrItemBox);
            RefreshPrivateTicketCache(c.arrItemBag);
        }

        private static int CountCloverInArray(Item[] arr)
        {
            if (arr == null) return 0;
            int total = 0;
            for (int i = 0; i < arr.Length; i++)
            {
                Item it = arr[i];
                if (it == null || it.template == null) continue;
                if (it.template.id == ID_ITEM_COBONLA ||
                    (!string.IsNullOrEmpty(it.template.name) && it.template.name.ToLower().Contains("cỏ bốn lá")))
                {
                    total += System.Math.Max(1, it.quantity);
                }
            }
            return total;
        }

        private static void RefreshPrivateTicketCache(Item[] bag)
        {
            _cachedTicketIcon = 0;
            _cachedTicketText = "";
            if (bag == null) return;
            for (int i = 0; i < bag.Length; i++)
            {
                Item it = bag[i];
                if (it == null || it.template == null) continue;
                bool isTicket = it.template.id == 1825 ||
                    (!string.IsNullOrEmpty(it.template.name) && it.template.name.ToLower().Contains("vé riêng tư"));
                if (!isTicket) continue;

                _cachedTicketIcon = it.template.iconID > 0 ? it.template.iconID : ID_ICON_ITEM_VE_RIENG_TU;
                int totalMin = 0;
                bool foundTime = false;
                if (it.itemOption != null)
                {
                    for (int j = 0; j < it.itemOption.Length; j++)
                    {
                        var opt = it.itemOption[j];
                        if (opt == null || opt.optionTemplate == null) continue;
                        int oid = opt.optionTemplate.id;
                        int val = opt.param;
                        if (oid == 63 || oid == 93) { totalMin += System.Math.Max(0, val) * 1440; foundTime = true; }
                        else if (oid == 64 || oid == 252) { totalMin += System.Math.Max(0, val) * 60; foundTime = true; }
                        else if (oid == 65 || oid == 9 || oid == 1) { totalMin += System.Math.Max(0, val); foundTime = true; }
                    }
                }
                if (foundTime)
                {
                    int d = totalMin / 1440;
                    int h = (totalMin % 1440) / 60;
                    int m = totalMin % 60;
                    _cachedTicketText = d + "n " + h + "g " + m + "p";
                }
                else
                {
                    _cachedTicketText = "0n 0g 0p";
                }
                return;
            }
        }

        public static void PaintPrivateTicketBuff(mGraphics g, int x, int y)
        {
            try
            {
                if (_cachedTicketIcon <= 0) return;
                // Tránh vẽ trùng nếu game gốc đã đẩy Vé riêng tư vào Char.vItemTime
                if (ItemTime.isExistItem(_cachedTicketIcon) || ItemTime.isExistItem(ID_ICON_ITEM_VE_RIENG_TU)) return;
                SmallImage.drawSmallImage(g, _cachedTicketIcon, x, y, 0, 3);
                mFont txtFont = (_cachedTicketText == "0n 0g 0p") ? mFont.tahoma_7b_yellow : mFont.tahoma_7b_white;
                txtFont.drawString(g, _cachedTicketText, 3, y + 12, mFont.LEFT, mFont.tahoma_7b_dark);
            }
            catch { }
        }

        private static void ScanPaintArray(Item[] arr, List<string> orderedSets, Dictionary<string, int[]> countsBySet)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                Item it = arr[i];
                if (it == null || it.template == null || it.template.type < 0 || it.template.type > 4 || !IsSKH(it)) continue;
                string setName = ExtractSKHSetName(it);
                if (string.IsNullOrEmpty(setName)) setName = "SKH";
                if (!countsBySet.ContainsKey(setName))
                {
                    orderedSets.Add(setName);
                    countsBySet[setName] = new int[5];
                }
                countsBySet[setName][it.template.type]++;
            }
        }

        public static bool PaintBlackScreenIfEnabled(mGraphics g)
        {
            if (!Profile.BlackScreenVPS) return false;
            g.reset();
            g.setColor(0x0E1118);
            g.fillRect(0, 0, GameCanvas.w, GameCanvas.h);
            Paint(g);
            return true;
        }

        public static void Paint(mGraphics g)
        {
            try
            {
                RebuildPaintCacheIfNeeded();

                // Góc trên bên phải: thống kê tổng % May Mắn + hạn Danh hiệu + IP & Phím tắt xếp từng dòng
                int rx = GameCanvas.w - 5;
                string luckHead = "May Man: +" + _cachedTotalLuck + "%";
                mFont luckFont = _cachedTotalLuck > 0 ? mFont.tahoma_7b_green2 : mFont.tahoma_7b_yellow;
                luckFont.drawString(g, luckHead, rx, 4, mFont.RIGHT);
                mFont.tahoma_7b_white.drawString(g, _cachedLuckDetail, rx, 14, mFont.RIGHT);
                string dhTxt = string.IsNullOrEmpty(_cachedTitleName)
                    ? "DH: Chua co"
                    : ("DH: " + ShortTitleLabel(_cachedTitleName) + (string.IsNullOrEmpty(_cachedTitleRemain) ? "" : " (" + _cachedTitleRemain + ")"));
                mFont dhFont = string.IsNullOrEmpty(_cachedTitleName) ? mFont.tahoma_7b_white : mFont.tahoma_7b_yellow;
                dhFont.drawString(g, dhTxt, rx, 24, mFont.RIGHT);

                // IP Proxy + Các lệnh phím tắt chuyển sang góc phải, xếp gọn từng dòng
                int ry = 37;
                string netTxt = Account.privacyMode ? "IP: ***" : ("IP: " + (string.IsNullOrEmpty(IpConnect) ? "Home" : IpConnect));
                mFont.tahoma_7b_red.drawString(g, netTxt, rx, ry, mFont.RIGHT); ry += 10;
                mFont.tahoma_7b_white.drawString(g, Profile.BlackScreenVPS ? "B: Tat den" : "B: Man den", rx, ry, mFont.RIGHT, mFont.tahoma_7b_dark); ry += 10;
                mFont.tahoma_7b_white.drawString(g, Profile.ShowMobGraphics ? "M: An quai" : "M: Hien quai", rx, ry, mFont.RIGHT, mFont.tahoma_7b_dark); ry += 10;
                mFont.tahoma_7b_white.drawString(g, Profile.ShowCharGraphics ? "N: An NV" : "N: Hien NV", rx, ry, mFont.RIGHT, mFont.tahoma_7b_dark); ry += 10;
                mFont.tahoma_7b_white.drawString(g, Profile.HorizontalInventory ? "G: Tui Doc" : "G: Tui Ngang", rx, ry, mFont.RIGHT, mFont.tahoma_7b_dark); ry += 10;
                mFont.tahoma_7b_white.drawString(g, Profile.FastAttackNoAnim ? "K: Xoa anim" : "K: Danh thuong", rx, ry, mFont.RIGHT, mFont.tahoma_7b_dark); ry += 10;
                mFont.tahoma_7b_white.drawString(g, AutoGame.isPaused ? "P: Tiep tuc" : "P: Tam dung", rx, ry, mFont.RIGHT, mFont.tahoma_7b_dark);

                // Góc trên bên trái: tự co gọn theo pixel để KHÔNG BAO GIỜ đè lên cột May Mắn bên phải
                string mamTxt = IsConMam() ? ("Mam" + (string.IsNullOrEmpty(GetMamRemain()) ? "" : ":" + GetMamRemain())) : "HetMam";
                string tdltTxt = (GameScr.canAutoPlay || ItemTime.isExistItem(ID_ICON_ITEM_TDLT)) ? "TDLT" : "NoTDLT";
                string head = NetProfileMap.HudFullPrefix() + " | " + AutoGame.fsmState + " | K" + TileMap.zoneID + " | " + tdltTxt + " | " + mamTxt;
                int maxHeadW = GameCanvas.w - luckFont.getWidth(luckHead) - 16;
                if (mFont.tahoma_7b_yellow.getWidth(head) > maxHeadW && maxHeadW > 80)
                {
                    head = NetProfileMap.HudShortPrefix() + " | " + AutoGame.fsmState + " | K" + TileMap.zoneID + " | " + tdltTxt + " | " + mamTxt;
                }
                if (mFont.tahoma_7b_yellow.getWidth(head) > maxHeadW && maxHeadW > 80)
                {
                    head = NetProfileMap.HudShortPrefix() + " | " + AutoGame.fsmState + " | K" + TileMap.zoneID;
                }
                mFont.tahoma_7b_yellow.drawString(g, head, 6, 4, 0);

                string bagBoxTxt = "T:" + GetBagUsed() + " R:" + GetBoxUsed() + "/" + GetBoxMax();
                string stTxt = AutoGame.isStatus ?? "";
                int maxLine2W = GameCanvas.w - mFont.tahoma_7b_white.getWidth(_cachedLuckDetail) - 16;
                string line2 = stTxt + " | " + bagBoxTxt;
                if (mFont.tahoma_7b_white.getWidth(line2) > maxLine2W && maxLine2W > 60)
                {
                    int availStW = maxLine2W - mFont.tahoma_7b_white.getWidth(".. | " + bagBoxTxt);
                    if (availStW > 24 && stTxt.Length > 4)
                    {
                        int keep = stTxt.Length;
                        while (keep > 3 && mFont.tahoma_7b_white.getWidth(stTxt.Substring(0, keep)) > availStW) keep--;
                        line2 = stTxt.Substring(0, keep) + ".. | " + bagBoxTxt;
                    }
                    else
                    {
                        line2 = bagBoxTxt;
                    }
                }
                mFont.tahoma_7b_white.drawString(g, line2, 6, 14, 0);

                int y = 25;
                if (_cachedSKHLines.Count > 0)
                {
                    mFont.tahoma_7b_yellow.drawString(g, "SKH (Ao-Quan-Gang-Giay-Rd):", 6, y, 0);
                    y += 10;
                    int flashPhase = (GameCanvas.gameTick / 4) % 4;
                    for (int i = 0; i < _cachedSKHLines.Count && i < 8; i++)
                    {
                        var line = _cachedSKHLines[i];
                        if (line.Total >= 5)
                        {
                            // Hiệu ứng nhấp nháy kiểu trúng số / nổ set cho các bộ đã đạt >= 5 món
                            mFont mainF;
                            mFont borderF;
                            string suffix = line.IsFullSet ? " [5/5]!!" : " !!";
                            string disp = (flashPhase % 2 == 0) ? ("* " + line.Text + suffix) : (line.Text + suffix);
                            switch (flashPhase)
                            {
                                case 0: mainF = mFont.tahoma_7b_yellow; borderF = mFont.tahoma_7b_red; break;
                                case 1: mainF = mFont.tahoma_7b_red; borderF = mFont.tahoma_7b_yellow; break;
                                case 2: mainF = mFont.tahoma_7b_green2; borderF = mFont.tahoma_7b_dark; break;
                                default: mainF = mFont.tahoma_7b_white; borderF = mFont.tahoma_7b_red; break;
                            }
                            mainF.drawString(g, disp, 8, y, 0, borderF);
                        }
                        else
                        {
                            mFont font = line.HasAny ? mFont.tahoma_7b_green2 : mFont.tahoma_7b_white;
                            font.drawString(g, line.Text, 10, y, 0);
                        }
                        y += 10;
                    }
                    mFont.tahoma_7b_yellow.drawString(g, "Tong: " + _cachedTotalSKH + " mon", 6, y + 1, 0, mFont.tahoma_7b_dark);
                }

                // Thanh đáy: Vàng, Ngọc xanh, Ngọc hồng, số lượng Cỏ 4 lá còn lại (Rương / Hành trang)
                var me = global::Char.myCharz();
                if (me != null)
                {
                    int by = GameCanvas.h - 12;
                    int iconY = GameCanvas.h - 7;
                    int bx = 6;

                    string goldStr = !string.IsNullOrEmpty(me.xuStr) ? me.xuStr : mSystem.numberTostring(me.xu);
                    if (Panel.imgXu != null)
                    {
                        g.drawImage(Panel.imgXu, bx + 5, iconY, 3);
                        bx += 13;
                    }
                    else
                    {
                        goldStr = "V:" + goldStr;
                    }
                    mFont.tahoma_7b_yellow.drawString(g, goldStr, bx, by, 0, mFont.tahoma_7b_dark);
                    bx += mFont.tahoma_7b_yellow.getWidth(goldStr) + 9;

                    string gemStr = mSystem.numberTostring(me.luong);
                    if (Panel.imgLuong != null)
                    {
                        g.drawImage(Panel.imgLuong, bx + 5, iconY, 3);
                        bx += 13;
                    }
                    else
                    {
                        gemStr = "NX:" + gemStr;
                    }
                    mFont.tahoma_7b_green2.drawString(g, gemStr, bx, by, 0, mFont.tahoma_7b_dark);
                    bx += mFont.tahoma_7b_green2.getWidth(gemStr) + 9;

                    string pinkStr = mSystem.numberTostring(me.luongKhoa);
                    if (Panel.imgLuongKhoa != null)
                    {
                        g.drawImage(Panel.imgLuongKhoa, bx + 5, iconY, 3);
                        bx += 13;
                    }
                    else
                    {
                        pinkStr = "NH:" + pinkStr;
                    }
                    mFont.tahoma_7b_red.drawString(g, pinkStr, bx, by, 0, mFont.tahoma_7b_dark);
                    bx += mFont.tahoma_7b_red.getWidth(pinkStr) + 9;

                    string cloverStr;
                    if (_cachedCloverBox > 0 && _cachedCloverBag > 0)
                        cloverStr = "Co: " + _cachedCloverBox + "R+" + _cachedCloverBag + "T";
                    else if (_cachedCloverBox > 0)
                        cloverStr = "Co(R): " + _cachedCloverBox;
                    else
                        cloverStr = "Co: " + _cachedCloverBag;
                    mFont.tahoma_7b_green2.drawString(g, cloverStr, bx, by, 0, mFont.tahoma_7b_dark);
                }
            }
            catch { }
        }
    }
}
