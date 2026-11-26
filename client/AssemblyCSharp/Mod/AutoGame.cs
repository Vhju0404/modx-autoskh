using System;
using System.Collections.Generic;
using System.Linq;
using AssemblyCSharp.Mod.Xmap;
using Assets.src.g;
using static AssemblyCSharp.Mod.LoadData;

namespace AssemblyCSharp.Mod
{
    /// <summary>
    /// Máy Trạng Thái (FSM) Up Set Kích Hoạt 24/7 — MOD X · H TEAM
    /// với bộ lọc từng bộ SKH, giữ >= N sao và điều phối đa tab Golang.
    /// </summary>
    public static class AutoGame
    {
        public static bool isAuto = false;
        public static bool isPaused = false;
        public static bool isConnect = false;

        public static string fsmState = "IDLE";
        public static string isStatus = "Khởi động";

        // Tương thích hook Panel.cs / Char.cs
        public static bool OnGiaoDich = false;
        public static bool StepGiaoDich = false;
        public static bool isPetProtect = false;
        public static bool isSaoMayLuoiThe = false;

        // Cờ trạng thái tác vụ
        public static bool isStoreMode = false;
        public static bool isSellMode = false;
        public static bool isBuyTDLT = false;
        public static bool isBuyLucky = false;
        public static bool isBuyTicket = false;
        public static bool isBuyGoldBar = false;
        public static bool isBuyCharm = false;
        public static bool isGetTitle = false;
        public static bool isBoxDoneStop = false;
        public static bool isUsingPrivateTicket = false;
        private static bool notifiedBoxFullContinue = false;

        // Sub-FSM tự lấy & gia hạn Danh hiệu Tay nhanh hơn não (1673) bằng 10 Loa LVT (1653)
        private static int titlePhase = 0; // 0=CheckTitleShop, 1=BuyLoa, 2=UseLoa, 3=BuyTitle
        private static int titleNeedLoa = 10;
        private static int titleLoaSeq = 1;
        private static int titleBuyAttempts = 0;
        private static int titleGemBeforeBuy = 0;
        private static int titlePhase0Checks = 0;
        private static long lastLoaUseAt = 0;
        private static string pendingClientInput = null;
        private static long pendingClientInputReadyAt = 0;
        private static long lastClientInputSentAt = 0;

        public static bool IsWaitingTitleBuyResult()
        {
            return isGetTitle && titlePhase == 3 && titleBuyAttempts > 0;
        }

        private static void CloseNpcUi()
        {
            try
            {
                if (GameCanvas.panel != null && GameCanvas.panel.isShow)
                {
                    GameCanvas.panel.ForceHide();
                }
            }
            catch { }
            try { if (GameCanvas.menu != null && GameCanvas.menu.showMenu) GameCanvas.menu.doCloseMenu(); } catch { }
            try { ChatPopup.currChatPopup = null; } catch { }
        }

        public static void OnTitleAlreadyActiveFromServer()
        {
            long now = mSystem.currentTimeMillis();
            GameEvents.ConfirmFastHandTitleActive(now, true);
            if (isGetTitle)
            {
                SocketClient.SendEvent("title_acquired", "Xác nhận đang đeo sẵn danh hiệu Tay nhanh hơn não (+20% May mắn)");
                AbortBuyTripCooldown("title", now, 1800000L, "Đã có danh hiệu Tay nhanh -> đi farm");
            }
        }

        public static void OnTitleActivatedFromServer()
        {
            long now = mSystem.currentTimeMillis();
            GameEvents.ConfirmFastHandTitleActive(now, true);
            if (isGetTitle)
            {
                SocketClient.SendEvent("title_acquired", "Đã nhận & kích hoạt danh hiệu Tay nhanh hơn não (+20% May mắn, HSD 7 ngày)");
                AbortBuyTripCooldown("title", now, 1800000L, "Đã kích hoạt danh hiệu -> đi farm");
            }
        }

        public static bool TryConsumePendingClientInput(long now, out string text)
        {
            text = null;
            if (pendingClientInput == null || now < pendingClientInputReadyAt || now - lastClientInputSentAt < 650L)
            {
                return false;
            }
            text = pendingClientInput;
            pendingClientInput = null;
            lastClientInputSentAt = now;
            return true;
        }

        // Watchdog & Cooldowns chuẩn ModZone / ModBuy / ModItemShop + bộ đếm X-Quang Headless
        public static long lastTimeHitTn = 0;
        private static readonly long[] _expHitRing = new long[128];
        private static int _expHitHead = 0;
        private static int _expHitTotal = 0;
        private static long farmStartAt = 0;

        public static void RecordExpHit()
        {
            long now = mSystem.currentTimeMillis();
            lastTimeHitTn = now;
            _expHitRing[_expHitHead] = now;
            _expHitHead = (_expHitHead + 1) % _expHitRing.Length;
            _expHitTotal++;
        }

        public static int GetExpHitsLastMinute(long now)
        {
            long cutoff = now - 60000L;
            int cnt = 0;
            for (int i = 0; i < _expHitRing.Length; i++)
            {
                if (_expHitRing[i] >= cutoff) cnt++;
            }
            return cnt;
        }

        public static int GetTotalExpHits() => _expHitTotal;

        public static int GetLastExpIdleSec(long now)
        {
            if (lastTimeHitTn <= 0) return -1;
            long diff = (now - lastTimeHitTn) / 1000L;
            return diff < 0 ? 0 : (int)diff;
        }
        private static long noMobSince = 0;
        private static int lastTrackedMap = -1;
        private static long mapEnteredAt = 0;

        // ModZone: chờ 8s sau khi vào map, cooldown đổi khu 12s, hỏi danh sách khu 15s
        private static long lastZoneReqAt = 0;
        private static long lastZoneAskAt = 0;
        private static bool waitingZoneCallback = false;
        private static bool zoneExcludeCurrent = false;
        private static int targetZone = -1;
        private static bool manualZonePending = false;
        private static int manualZoneId = -1;
        private static bool hasSwitchedForNoExp = false;

        // ModBuy: timeout chuyến mua 30s + cooldown 60s từng loại (không bao giờ kẹt NPC!)
        private static string activeBuyKind = "";
        private static long buyTripStartAt = 0;
        private static long lastBuyClickAt = 0;
        private static long lastMenuOpenAt = 0;
        private static long lastMenuSelectAt = 0;
        private static long lastNpcTeleAt = 0;
        private static long shopPanelOpenedAt = 0;
        private static readonly Dictionary<string, long> BuyCooldownUntil = new Dictionary<string, long>();

        // ModItemShop: bán/cất duyệt ngược cuối túi + giãn nhịp 650ms + cầu chì chống kẹt 24/7
        private static long lastBagOpAt = 0;
        private static long lastAskBeanAt = 0;
        private static long lastLowGemEventAt = 0;
        private static long homeEnteredAt = 0;
        private static long lastTicketUseAt = 0;
        private static long storeTripStartAt = 0;
        private static long storeCooldownUntil = 0;
        private static long sellTripStartAt = 0;
        private static long sellCooldownUntil = 0;
        private static long xmapTripStartAt = 0;
        private static int xmapTargetMap = -1;
        private static int xmapFailCount = 0;

        private static readonly string[] ShopCaptions = { "Cửa hàng", "OK", "Shop", "Cửa", "Từ chối", "Cho kẹo" };

        public static void SetState(string state, string statusText)
        {
            fsmState = state;
            isStatus = statusText;
        }

        public static void ResetNoExpTimer()
        {
            long now = mSystem.currentTimeMillis();
            lastTimeHitTn = now;
            farmStartAt = now;
            noMobSince = 0;
            hasSwitchedForNoExp = false;
        }

        public static void RequestManualSwitchZone(int zoneId)
        {
            manualZonePending = true;
            manualZoneId = zoneId; // -1 = tìm khu vắng
            targetZone = -1;
            ResetNoExpTimer();
        }

        public static void ForceStoreNow()
        {
            isStoreMode = true;
            isSellMode = false;
            isBoxDoneStop = false;
            storeTripStartAt = 0;
            storeCooldownUntil = 0;
            SetState("STORE", "Về nhà cất đồ (lệnh nóng)");
        }

        public static void ForceSellNow()
        {
            isSellMode = true;
            sellTripStartAt = 0;
            sellCooldownUntil = 0;
            SetState("SELL", "Đi bán rác (lệnh nóng)");
        }

        /// <summary>
        /// Callback gắn ở Controller.cs (case 29) khi server trả về danh sách khu thật mới nhất!
        /// </summary>
        public static void OnZoneListReceived()
        {
            if (!waitingZoneCallback) return;
            waitingZoneCallback = false;

            long now = mSystem.currentTimeMillis();
            if (now - mapEnteredAt < 8000L || now - lastZoneReqAt < 10000L) return;

            int best = FindBestEmptyZone(zoneExcludeCurrent);
            if (best >= 0 && best != TileMap.zoneID)
            {
                targetZone = best;
                lastZoneReqAt = now;
                Service.gI().requestChangeZone(best, -1);
                ResetNoExpTimer();
                SetState("ZONE", "Đổi sang khu vắng K" + best);
            }
        }

        /// <summary>
        /// Gọi từ PickMobController khi hết quái sống trong map -> sau 15s tự tìm khu vắng đổi khu.
        /// </summary>
        public static void OnNoMobTick(bool hasMobAlive)
        {
            if (hasMobAlive)
            {
                noMobSince = 0;
                return;
            }
            if (IsInPrivateMap()) return;
            long now = mSystem.currentTimeMillis();
            if (noMobSince == 0) noMobSince = now;

            var prof = GameEvents.Profile;
            bool isFixed = Account.zoneMode == "fixed" || (Account.zoneMode == "profile" && prof.ZoneStrategy == "fixed");
            if (!isFixed && now - noMobSince >= 15000L && CanChangeZone(now))
            {
                noMobSince = now;
                AskZoneListAndSwitch(now, true, "Hết quái 15s -> đổi khu vắng");
            }
        }

        public static int ResolveTargetMapId()
        {
            if (Account.mapID >= 0) return Account.mapID;
            var p = GameEvents.Profile;
            int gender = global::Char.myCharz().cgender;
            if (gender == 1) return p.DefaultMapNM;
            if (gender == 2) return p.DefaultMapXD;
            return p.DefaultMapTD;
        }

        public static bool IsInPrivateMap()
        {
            return (TileMap.mapID == 3 || TileMap.mapID == 11 || TileMap.mapID == 17) &&
                   TileMap.vGo.size() == 0;
        }

        public static void TickFSM()
        {
            var myChar = global::Char.myCharz();
            var prof = GameEvents.Profile;
            if (myChar == null || string.IsNullOrEmpty(myChar.cName)) return;

            long now = mSystem.currentTimeMillis();
            if (TileMap.mapID != lastTrackedMap)
            {
                lastTrackedMap = TileMap.mapID;
                mapEnteredAt = now;
                waitingZoneCallback = false;
            }

            if (isPaused)
            {
                StopCombat();
                SetState("PAUSED", "Đã tạm dừng");
                return;
            }

            if (isBoxDoneStop)
            {
                StopCombat();
                SetState("DONE", "Full rương — đứng chờ ở nhà");
                return;
            }

            AutoDropConfiguredItems();

            if (!isSaoMayLuoiThe && myChar.cFlag == 8)
            {
                Service.gI().getFlag(1, 0);
            }

            // P1 — Sinh tồn: Chết -> Hồi sinh về nhà
            if (myChar.meDead || myChar.cHP <= 0)
            {
                StopCombat();
                SetState("DEAD", "Chết — đang về nhà");
                Service.gI().returnTownFromDead();
                return;
            }

            // P2 — Cất đồ SKH / Đồ Sao đạt chuẩn về rương nhà (có cooldown chống lặp vô tận)
            if (prof.AutoStoreHouse && now >= storeCooldownUntil && GameEvents.CountItemsToStore() > 0)
            {
                if (!GameEvents.IsBoxFull())
                {
                    notifiedBoxFullContinue = false;
                    isStoreMode = true;
                }
                else if (prof.OnBoxFull == "mark_done_rotate" || prof.OnBoxFull == "stop_at_home")
                {
                    isStoreMode = true;
                }
                else if (!notifiedBoxFullContinue)
                {
                    // Kệ rương đầy — cứ đánh tiếp tận dụng hành trang ẩn của game!
                    notifiedBoxFullContinue = true;
                    SocketClient.SendEvent("info", "Rương nhà đã đầy (" + GameEvents.GetBoxUsed() + "/" + GameEvents.GetBoxMax() + ") — tiếp tục đánh (hành trang ẩn)");
                }
            }
            if (isStoreMode)
            {
                StopCombat();
                RunStoreAtHome(now);
                return;
            }
            storeTripStartAt = 0;

            // Nếu đang ở trong nhà -> thu đậu + ra khỏi nhà
            if (TileMap.mapID == GameEvents.MyHome())
            {
                StopCombat();
                ExitHomeSafe(now);
                return;
            }
            homeEnteredAt = 0;

            // P3 — Full hành trang (hoặc còn < 2 ô trống) -> bán đồ rác duyệt ngược ở Map 24 (có cooldown chống lặp vô tận)
            if (prof.AutoSellTrash && now >= sellCooldownUntil && GameEvents.GetBagFree() <= 1 && GameEvents.CountItemsToSell() > 0)
            {
                isSellMode = true;
            }
            if (isSellMode)
            {
                StopCombat();
                RunSellTrash(now);
                return;
            }
            sellTripStartAt = 0;

            // P4 — Chuyến mua hàng chống kẹt 30s (TDLT / Cỏ 4 lá / Vé riêng 1823 / Thỏi vàng 457 / Bùa Hạt Mít)
            EvaluateAutoBuyTriggers(now);
            if (RunActiveBuyTrip(now))
            {
                StopCombat();
                return;
            }

            // P5 — Chế độ Map Riêng (Vé 1825, chỉ dùng khi còn Mầm SKH)
            if (prof.IsPrivateTicketMode)
            {
                if (IsInPrivateMap())
                {
                    isUsingPrivateTicket = true;
                    if (prof.IsPetMode && !isPetProtect)
                    {
                        SetState("BUFF", "Bật đệ tử bảo vệ");
                        Service.gI().petStatus(1);
                        return;
                    }
                    StartCombat("Farm SKH (Map riêng)");
                    CheckNoExpWatchdog(allowZoneSwitch: false);
                    return;
                }
                if (GameEvents.IsConMam() && GameEvents.HasPrivateTicketInBag())
                {
                    // Vé riêng tư [1825] dùng được ở MỌI NƠI (sau khi kiểm tra/mua ở Santa, bán rác Map 24, ra khỏi nhà...) -> bấm vào thẳng luôn, KHÔNG chạy Xmap vòng vèo!
                    StopCombat();
                    CloseNpcUi();
                    if (now - lastTicketUseAt > 5000L)
                    {
                        lastTicketUseAt = now;
                        isUsingPrivateTicket = true;
                        SetState("TRAVEL", "Dùng Vé riêng tư [1825] vào map riêng ngay");
                        GameEvents.UseItem(1825);
                    }
                    return;
                }
                isUsingPrivateTicket = false;
            }

            // P6 — Map thường: Xmap tới map đích (có cầu chì chống kẹt 60s -> reset, quá 2 lần thoát tab mở lại)
            int dstMap = ResolveTargetMapId();
            if (TileMap.mapID != dstMap)
            {
                targetZone = -1;
                isPetProtect = false;
                StopCombat();
                if (xmapTargetMap != dstMap || xmapTripStartAt == 0)
                {
                    xmapTargetMap = dstMap;
                    xmapTripStartAt = now;
                }
                else if (now - xmapTripStartAt > 60000L)
                {
                    xmapFailCount++;
                    XmapController.FinishXmap();
                    xmapTripStartAt = now;
                    if (xmapFailCount >= 2)
                    {
                        SocketClient.SendEvent("stuck", "Kẹt Xmap tới Map " + dstMap + " quá 2 lần (120s) -> thoát tab mở lại");
                        Main.exit();
                        return;
                    }
                    SocketClient.SendEvent("stuck", "Kẹt Xmap tới Map " + dstMap + " > 60s -> tự tính lại đường");
                }
                SetState("TRAVEL", "Đang Xmap tới Map " + dstMap);
                XmapController.StartRunToMapId(dstMap);
                return;
            }
            xmapTripStartAt = 0;
            xmapFailCount = 0;
            xmapTargetMap = -1;

            // Bật đệ bảo vệ nếu đang ở mode Up Đệ
            if (prof.IsPetMode && !isPetProtect)
            {
                SetState("BUFF", "Bật đệ tử bảo vệ");
                Service.gI().petStatus(1);
                return;
            }

            // P7 — Chọn khu thông minh chuẩn ModZone
            if (HandleZoneSelection(now))
            {
                return;
            }

            // P8 — Farm SKH + Watchdog chống kẹt 2 tầng
            StartCombat("Đang farm K" + TileMap.zoneID);
            CheckNoExpWatchdog(allowZoneSwitch: true);
        }

        // --- ModZone: chọn khu an toàn (chờ 8s vào map, cooldown 12s/15s, callback thật) ---

        private static bool CanChangeZone(long now)
        {
            return now - mapEnteredAt >= 8000L && now - lastZoneReqAt >= 12000L;
        }

        private static void AskZoneListAndSwitch(long now, bool excludeCurrent, string status)
        {
            if (now - lastZoneAskAt < 15000L) return;
            lastZoneAskAt = now;
            waitingZoneCallback = true;
            zoneExcludeCurrent = excludeCurrent;
            SetState("ZONE", status);
            Service.gI().openUIZone();
        }

        private static bool HandleZoneSelection(long now)
        {
            var prof = GameEvents.Profile;

            // Lệnh đổi khu thủ công từ QLTK
            if (manualZonePending)
            {
                StopCombat();
                if (manualZoneId >= 0)
                {
                    if (TileMap.zoneID == manualZoneId)
                    {
                        manualZonePending = false;
                        return false;
                    }
                    if (now - lastZoneReqAt > 4000L)
                    {
                        lastZoneReqAt = now;
                        Service.gI().requestChangeZone(manualZoneId, -1);
                        manualZonePending = false;
                        ResetNoExpTimer();
                    }
                    SetState("ZONE", "Đổi sang khu " + manualZoneId);
                    return true;
                }
                manualZonePending = false;
                AskZoneListAndSwitch(now, true, "Quét danh sách khu vắng...");
                return true;
            }

            bool isFixed = Account.zoneMode == "fixed" || (Account.zoneMode == "profile" && prof.ZoneStrategy == "fixed");
            if (isFixed)
            {
                int fixedZ = Account.zoneMode == "fixed" ? Account.zoneID : prof.FixedZoneID;
                if (TileMap.zoneID != fixedZ && CanChangeZone(now))
                {
                    StopCombat();
                    lastZoneReqAt = now;
                    Service.gI().requestChangeZone(fixedZ, -1);
                    ResetNoExpTimer();
                    SetState("ZONE", "Vào khu cố định K" + fixedZ);
                    return true;
                }
                return false;
            }

            // Đếm người thật trong khu (chuẩn ModZone.players()) -> nếu đông quá MaxPlayersInZone tự hỏi list khu vắng!
            int otherPlayers = GameEvents.CountOtherPlayersInZone();
            if (prof.MaxPlayersInZone > 0 && otherPlayers >= prof.MaxPlayersInZone && CanChangeZone(now))
            {
                AskZoneListAndSwitch(now, true, "Khu đông (" + (otherPlayers + 1) + " ng) -> tìm khu vắng");
            }
            return false;
        }

        private static int FindBestEmptyZone(bool excludeCurrent)
        {
            var scr = GameScr.gI();
            if (scr == null || scr.zones == null || scr.numPlayer == null) return -1;

            int bestZone = -1;
            int minPlayers = int.MaxValue;
            int len = System.Math.Min(scr.zones.Length, scr.numPlayer.Length);
            for (int i = 0; i < len; i++)
            {
                if (excludeCurrent && i == TileMap.zoneID) continue;
                int np = scr.numPlayer[i];
                int mp = (scr.maxPlayer != null && i < scr.maxPlayer.Length) ? scr.maxPlayer[i] : 15;
                if (mp > 0 && np >= mp) continue; // Bỏ qua khu đã đầy!
                if (np == 0) return i;
                if (np < minPlayers)
                {
                    minPlayers = np;
                    bestZone = i;
                }
            }
            return bestZone;
        }

        // --- Watchdog chống kẹt 2 tầng ---

        private static void CheckNoExpWatchdog(bool allowZoneSwitch)
        {
            var prof = GameEvents.Profile;
            long now = mSystem.currentTimeMillis();
            if (farmStartAt == 0) farmStartAt = now;
            if (lastTimeHitTn == 0) lastTimeHitTn = now;

            long idleSec = (now - lastTimeHitTn) / 1000L;

            if (allowZoneSwitch && prof.NoExpSwitchSec > 0 && !hasSwitchedForNoExp && idleSec >= prof.NoExpSwitchSec)
            {
                hasSwitchedForNoExp = true;
                AskZoneListAndSwitch(now, true, idleSec + "s không nhảy TN -> đổi khu vắng");
                SocketClient.SendEvent("stuck", idleSec + "s không nhảy TN -> tự đổi khu vắng");
                return;
            }

            int restartSec = prof.NoExpRestartSec >= 40 ? prof.NoExpRestartSec : 95;
            if (idleSec >= restartSec)
            {
                SocketClient.SendEvent("stuck", idleSec + "s không nhảy TN -> thoát tab mở lại");
                Main.exit();
            }
        }

        private static void StartCombat(string status)
        {
            if (!PickMob.IsTanSat) ResetNoExpTimer();
            PickMob.IsTanSat = true;
            PickMob.IsAutoPickItems = true;
            SetState("FARM", status);
        }

        private static void StopCombat()
        {
            PickMob.IsTanSat = false;
            PickMob.IsAutoPickItems = false;
            farmStartAt = 0;
        }

        // --- ModItemShop: Cất rương nhà & Bán rác duyệt ngược từ cuối túi về đầu ---

        private static void RunStoreAtHome(long now)
        {
            if (storeTripStartAt == 0) storeTripStartAt = now;
            if (now - storeTripStartAt > 45000L)
            {
                isStoreMode = false;
                storeTripStartAt = 0;
                storeCooldownUntil = now + 120000L;
                CloseNpcUi();
                SocketClient.SendEvent("stuck", "Cất rương quá 45s -> hủy tạm 2p quay lại farm");
                SetState("TRAVEL", "Cất rương quá hạn -> quay lại farm");
                return;
            }
            if (IsInPrivateMap())
            {
                SetState("STORE", "Rời map riêng bằng vé 1825");
                if (now - lastTicketUseAt > 3000L)
                {
                    lastTicketUseAt = now;
                    GameEvents.UseItem(1825);
                }
                return;
            }
            if (TileMap.mapID != GameEvents.MyHome())
            {
                SetState("STORE", "Đang về nhà cất đồ SKH/Sao");
                GameEvents.GoHome();
                return;
            }

            if (GameEvents.IsBoxFull())
            {
                isStoreMode = false;
                if (GameEvents.Profile.OnBoxFull == "stop_at_home")
                {
                    SocketClient.SendEvent("box_full", "Rương nhà đã đầy (" + GameEvents.GetBoxUsed() + "/" + GameEvents.GetBoxMax() + ")");
                    isBoxDoneStop = true;
                    SetState("DONE", "Full rương — đứng nhà chờ");
                }
                else if (GameEvents.Profile.OnBoxFull == "mark_done_rotate")
                {
                    SocketClient.SendEvent("box_full", "Rương nhà đã đầy (" + GameEvents.GetBoxUsed() + "/" + GameEvents.GetBoxMax() + ")");
                    SetState("DONE", "Full rương — thoát xoay acc");
                    Main.exit();
                }
                else
                {
                    if (!notifiedBoxFullContinue)
                    {
                        notifiedBoxFullContinue = true;
                        SocketClient.SendEvent("info", "Rương nhà đã đầy (" + GameEvents.GetBoxUsed() + "/" + GameEvents.GetBoxMax() + ") — tiếp tục đánh (hành trang ẩn)");
                    }
                    SetState("TRAVEL", "Rương đầy — kệ, quay lại farm (hành trang ẩn)");
                }
                return;
            }

            if (now - lastBagOpAt < 650L) return;

            // Duyệt ngược từ cuối túi về 0 tránh lệch ô khi server dồn túi
            var bag = global::Char.myCharz().arrItemBag;
            for (int i = bag.Length - 1; i >= 0; i--)
            {
                Item item = bag[i];
                if (item != null && GameEvents.ShouldStoreItem(item))
                {
                    lastBagOpAt = now;
                    SetState("STORE", "Cất rương: " + item.template.name);
                    Service.gI().getItem(1, (sbyte)item.indexUI);
                    return;
                }
            }
            isStoreMode = false;
            SetState("TRAVEL", "Cất rương xong — quay lại farm");
        }

        private static void ExitHomeSafe(long now)
        {
            if (homeEnteredAt == 0) homeEnteredAt = now;
            if (now - homeEnteredAt > 15000L)
            {
                SocketClient.SendEvent("stuck", "Kẹt trong nhà > 15s -> thoát tab");
                Main.exit();
                return;
            }

            SetState("TRAVEL", "Thu đậu & ra khỏi nhà");
            Service.gI().pickItem(-1);
            if (GameScr.gI().magicTree != null && GameScr.gI().magicTree.currPeas > 0)
            {
                Service.gI().openMenu(4);
                Service.gI().confirmMenu(4, 0);
            }
            if (TileMap.vGo.size() > 0)
            {
                Waypoint wp = (Waypoint)TileMap.vGo.elementAt(0);
                int tx = wp.maxX - 20;
                if (global::Char.myCharz().cx != tx)
                {
                    GameEvents.TeleChar(tx, wp.maxY);
                    return;
                }
            }
            Service.gI().getMapOffline();
        }

        private static void RunSellTrash(long now)
        {
            if (sellTripStartAt == 0) sellTripStartAt = now;
            if (now - sellTripStartAt > 45000L)
            {
                isSellMode = false;
                sellTripStartAt = 0;
                sellCooldownUntil = now + 120000L;
                CloseNpcUi();
                SocketClient.SendEvent("stuck", "Bán rác Map 24 quá 45s -> hủy tạm 2p quay lại farm");
                SetState("TRAVEL", "Bán rác quá hạn -> quay lại farm");
                return;
            }
            if (IsInPrivateMap())
            {
                SetState("SELL", "Rời map riêng đi bán đồ");
                if (now - lastTicketUseAt > 3000L)
                {
                    lastTicketUseAt = now;
                    GameEvents.UseItem(1825);
                }
                return;
            }
            if (TileMap.mapID != 24)
            {
                SetState("SELL", "Xmap sang Map 24 bán rác");
                XmapController.StartRunToMapId(24);
                return;
            }
            Npc npc = GameScr.findNPCInMap(16);
            if (npc != null)
            {
                global::Char.myCharz().npcFocus = npc;
                if (global::Char.abs(npc.cx - global::Char.myCharz().cx) > 30)
                {
                    SetState("SELL", "Tới NPC " + npc.cName);
                    if (now - lastNpcTeleAt > 600L)
                    {
                        lastNpcTeleAt = now;
                        GameEvents.TeleChar(npc.cx, GameEvents.GetYGround(npc.cx, npc.cy));
                    }
                    return;
                }
            }
            if (GameCanvas.panel.isShow)
            {
                if (now - lastBagOpAt < 650L) return;
                // Duyệt ngược từ cuối túi về 0 tránh lệch index!
                var bag = global::Char.myCharz().arrItemBag;
                for (int i = bag.Length - 1; i >= 0; i--)
                {
                    Item item = bag[i];
                    if (item != null && GameEvents.ShouldSellItem(item))
                    {
                        lastBagOpAt = now;
                        short bagIdx = (short)(item.indexUI >= 0 ? item.indexUI : i);
                        SetState("SELL", "Bán rác: " + item.template.name);
                        Service.gI().saleItem(0, 1, bagIdx);
                        Service.gI().saleItem(1, 1, bagIdx);
                        return;
                    }
                }
                isSellMode = false;
                GameCanvas.panel.ForceHide();
                SetState("TRAVEL", "Đã bán sạch đồ rác");
                return;
            }
            OpenNpcMenuAndSelect(16, ShopCaptions);
        }

        // --- ModBuy: Bộ mua hàng chống kẹt 30s + cooldown 60s + tìm NPC theo tên + check shopHasItem ---

        private static bool IsBuyOnCooldown(string kind, long now)
        {
            long until;
            return BuyCooldownUntil.TryGetValue(kind, out until) && now < until;
        }

        private static void AbortBuyTrip(string kind, long now, string reason)
        {
            AbortBuyTripCooldown(kind, now, 60000L, reason);
        }

        private static void AbortBuyTripCooldown(string kind, long now, long cooldownMs, string reason)
        {
            BuyCooldownUntil[kind] = now + cooldownMs;
            activeBuyKind = "";
            buyTripStartAt = 0;
            shopPanelOpenedAt = 0;
            isBuyTDLT = false;
            isBuyLucky = false;
            isBuyTicket = false;
            isBuyGoldBar = false;
            isBuyCharm = false;
            isGetTitle = false;
            titlePhase = 0;
            titleBuyAttempts = 0;
            titlePhase0Checks = 0;
            pendingClientInput = null;
            CloseNpcUi();
            SetState("TRAVEL", reason);
        }

        private static void EvaluateAutoBuyTriggers(long now)
        {
            var prof = GameEvents.Profile;

            // 0. Nếu đã có Danh hiệu Tay nhanh hơn não (1673) trong hành trang mà chưa đeo -> tự đeo luôn!
            if (prof.AutoGetTitle && GameEvents.HasItemInBag(GameEvents.ID_ITEM_TITLE_FAST_HAND) &&
                !GameEvents.HasItemInBody(GameEvents.ID_ITEM_TITLE_FAST_HAND))
            {
                GameEvents.UseItem(GameEvents.ID_ITEM_TITLE_FAST_HAND);
                GameEvents.RecordFastHandTitleAcquired(now);
            }

            // 1. Tự đổi Thỏi vàng (457) chống tràn 2 tỷ vàng
            if (prof.AutoBuyGoldBar && !IsBuyOnCooldown("goldbar", now) && global::Char.myCharz().xu >= 1500000000L)
            {
                isBuyGoldBar = true;
            }

            // 2. Tự mua Vé riêng tư [1825]: CHỈ mua khi KHÔNG còn vé trong hành trang & đã ra ngoài map riêng (không check thời gian còn lại!)
            if (prof.IsPrivateTicketMode && prof.AutoBuyPrivateTicket && GameEvents.IsConMam() &&
                !IsInPrivateMap() && !GameEvents.HasPrivateTicketInBag() && !IsBuyOnCooldown("ticket", now))
            {
                if (GameEvents.HasEnoughGem(1)) isBuyTicket = true;
                else NotifyLowGemOnce("Không đủ ngọc mua Vé riêng tư");
            }

            // 3. Tự mua TDLT
            if (prof.IsTDLTMode && prof.AutoBuyTDLT && !ItemTime.isExistItem(GameEvents.ID_ICON_ITEM_TDLT) &&
                GameEvents.GetTimeTDLT() <= 0 && !IsBuyOnCooldown("tdlt", now))
            {
                int price = GameEvents.GetTDLTGemPrice(prof.TDLTItemID);
                if (GameEvents.HasEnoughGem(price)) isBuyTDLT = true;
                else NotifyLowGemOnce("Không đủ ngọc mua TDLT (cần " + price + " + giữ " + prof.MinGemKeep + ")");
            }

            // 4. Tự mua Cỏ 4 lá (1635)
            if (prof.AutoBuyLuckyGrass && !ItemTime.isExistItem(GameEvents.ID_ICON_ITEM_COBONLA) &&
                GameEvents.GetItemQty(1635) <= 0 && !IsBuyOnCooldown("lucky", now))
            {
                if (GameEvents.HasEnoughGem(10)) isBuyLucky = true;
                else NotifyLowGemOnce("Không đủ ngọc mua Cỏ 4 lá");
            }

            // 5. Tự nhận & gia hạn Danh hiệu "Tay nhanh hơn não" (1673 — +20% May mắn, HSD 7 ngày)
            if (prof.AutoGetTitle && !GameEvents.IsFastHandTitleActive(now) &&
                !GameEvents.HasItemInBag(GameEvents.ID_ITEM_TITLE_FAST_HAND) && !IsBuyOnCooldown("title", now))
            {
                if (GameEvents.HasEnoughGem(2))
                {
                    isGetTitle = true;
                }
                else
                {
                    NotifyLowGemOnce("Không đủ ngọc đi kiểm tra/nhận danh hiệu Tay nhanh hơn não");
                }
            }
        }

        private static bool RunActiveBuyTrip(long now)
        {
            string kind = "";
            if (isBuyGoldBar) kind = "goldbar";
            else if (isBuyTDLT) kind = "tdlt";
            else if (isBuyLucky) kind = "lucky";
            else if (isBuyTicket) kind = "ticket";
            else if (isGetTitle) kind = "title";
            else if (isBuyCharm) kind = "charm";

            if (string.IsNullOrEmpty(kind))
            {
                activeBuyKind = "";
                buyTripStartAt = 0;
                return false;
            }

            if (activeBuyKind != kind)
            {
                activeBuyKind = kind;
                buyTripStartAt = now;
                if (kind == "title")
                {
                    titlePhase = 0;
                    titleNeedLoa = 10;
                    titleLoaSeq = 1;
                    titleBuyAttempts = 0;
                    titlePhase0Checks = 0;
                }
            }

            // Watchdog chống kẹt NPC (chuyến lấy danh hiệu 10 loa cần ~40-60s nên đặt trần 150s)
            long maxTripMs = (kind == "title") ? 150000L : 30000L;
            if (now - buyTripStartAt > maxTripMs)
            {
                AbortBuyTripCooldown(kind, now, (kind == "title") ? 600000L : 60000L, "Chuyến " + kind + " quá thời gian -> hủy quay lại farm");
                return false;
            }

            if (kind == "goldbar") RunBuyGoldBar(now);
            else if (kind == "tdlt") RunBuyTDLT(now);
            else if (kind == "lucky") RunBuyLuckyGrass(now);
            else if (kind == "ticket") RunBuyPrivateTicket(now);
            else if (kind == "title") RunGetFastHandTitle(now);
            else if (kind == "charm") RunBuyCharm(now);
            return true;
        }

        /// <summary>
        /// Auto nhận & gia hạn danh hiệu "Tay nhanh hơn não" [1673] (+20% May mắn, HSD 7 ngày):
        /// Phase 0: Mở Santa -> Danh hiệu -> kiểm tra Sở hữu hoặc % Hoàn thành (option 220).
        /// Phase 1: Mở Santa -> Cửa hàng -> mua đủ số Loa to liên vũ trụ [1653] còn thiếu (15 ngọc/cái).
        /// Phase 2: Dùng từng loa tại hành trang & chat "hihi1" .. "hihi10" qua ClientInput.
        /// Phase 3: Mở Santa -> Danh hiệu -> mua [1673] (2 ngọc) -> tự đeo & ghi mốc hết hạn 7 ngày!
        /// </summary>
        private static void RunGetFastHandTitle(long now)
        {
            if (!GameEvents.Profile.AutoGetTitle)
            {
                AbortBuyTrip("title", now, "Đã tắt auto nhận danh hiệu");
                return;
            }
            if (GameEvents.IsFastHandTitleActive(now))
            {
                GameEvents.ConfirmFastHandTitleActive(now, true);
                SocketClient.SendEvent("title_acquired", "Đã xác nhận đeo danh hiệu Tay nhanh hơn não (+20% May mắn)");
                AbortBuyTripCooldown("title", now, 1800000L, "Đã có danh hiệu Tay nhanh -> đi farm");
                return;
            }
            if (GameEvents.HasItemInBag(GameEvents.ID_ITEM_TITLE_FAST_HAND))
            {
                CloseNpcUi();
                if (now - lastBuyClickAt > 900L)
                {
                    lastBuyClickAt = now;
                    SetState("BUFF", "Đeo danh hiệu [1673]");
                    GameEvents.UseItem(GameEvents.ID_ITEM_TITLE_FAST_HAND);
                    GameEvents.ConfirmFastHandTitleActive(now, true);
                }
                return;
            }

            if (!GoToMap5ShopNpc(now, "Qua Đảo Kame kiểm tra danh hiệu", out int npcId)) return;

            // Phase 0: Kiểm tra Santa -> Danh hiệu xem đã sở hữu hay hoàn thành bao nhiêu % rồi
            if (titlePhase == 0)
            {
                if (titlePhase0Checks >= 3)
                {
                    AbortBuyTripCooldown("title", now, 900000L, "Kiểm tra danh hiệu xong -> đi farm");
                    return;
                }
                if (GameCanvas.panel.isShow && global::Char.myCharz().arrItemShop != null)
                {
                    GameEvents.InspectOwnedTitleInShop(GameEvents.ID_ITEM_TITLE_FAST_HAND, now);
                    bool inOwned;
                    int pct = GameEvents.FindTitleShopProgress(GameEvents.ID_ITEM_TITLE_FAST_HAND, out inOwned);
                    if (pct < 0)
                    {
                        // Chưa phải shop danh hiệu -> đóng mở lại đúng menu Danh hiệu
                        CloseNpcUi();
                        return;
                    }
                    if (inOwned || pct >= 100)
                    {
                        titlePhase = 3;
                        titleBuyAttempts = 0;
                        return;
                    }
                    int done = pct / 10;
                    titleNeedLoa = System.Math.Max(1, 10 - done);
                    titleLoaSeq = done + 1;
                    int haveLoa = GameEvents.GetItemQty(GameEvents.ID_ITEM_LOA_LVT);
                    int needBuy = System.Math.Max(0, titleNeedLoa - haveLoa);
                    CloseNpcUi();

                    if (needBuy <= 0)
                    {
                        titlePhase = 2;
                        SetState("BUFF", "Đủ " + haveLoa + " Loa -> chat lấy DH");
                    }
                    else
                    {
                        int cost = needBuy * 15 + 2;
                        if (!GameEvents.HasEnoughGem(cost))
                        {
                            NotifyLowGemOnce("Cần " + cost + " ngọc (mua " + needBuy + " Loa LVT + 2 ngọc danh hiệu)");
                            AbortBuyTripCooldown("title", now, 600000L, "Thiếu " + cost + " ngọc lấy DH -> farm");
                            return;
                        }
                        titlePhase = 1;
                        SetState("BUFF", "Mua " + needBuy + " Loa LVT [1653]");
                    }
                    return;
                }
                SetState("BUFF", "Kiểm tra danh hiệu Santa");
                OpenNpcMenuAndSelect(npcId, new[] { "Danh hiệu", "Danh hieu" });
                return;
            }

            // Phase 1: Santa -> Cửa hàng -> mua đủ Loa to liên vũ trụ [1653]
            if (titlePhase == 1)
            {
                int haveLoa = GameEvents.GetItemQty(GameEvents.ID_ITEM_LOA_LVT);
                if (haveLoa >= titleNeedLoa)
                {
                    CloseNpcUi();
                    titlePhase = 2;
                    SetState("BUFF", "Đủ " + haveLoa + " Loa -> bắt đầu chat");
                    return;
                }
                if (GameCanvas.panel.isShow)
                {
                    if (!GameEvents.HasEnoughGem(15))
                    {
                        NotifyLowGemOnce("Không đủ 15 ngọc mua tiếp Loa to liên vũ trụ (1653)");
                        AbortBuyTripCooldown("title", now, 600000L, "Thiếu ngọc mua Loa -> farm");
                        return;
                    }
                    if (now - lastBuyClickAt > 1050L)
                    {
                        lastBuyClickAt = now;
                        pendingClientInput = "1";
                        pendingClientInputReadyAt = now + 350L;
                        SetState("BUFF", "Mua Loa [1653] (" + haveLoa + "/" + titleNeedLoa + ")");
                        Service.gI().buyItem(1, GameEvents.ID_ITEM_LOA_LVT, 0);
                    }
                    return;
                }
                SetState("BUFF", "Mở Cửa hàng Santa mua Loa");
                OpenNpcMenuAndSelect(npcId, new[] { "Cửa hàng" });
                return;
            }

            // Phase 2: Dùng từng Loa to liên vũ trụ [1653] trong hành trang & chat "hihi1" .. "hihi10"
            if (titlePhase == 2)
            {
                if (GameCanvas.panel.isShow || GameCanvas.menu.showMenu)
                {
                    CloseNpcUi();
                    return;
                }
                int remLoa = GameEvents.GetItemQty(GameEvents.ID_ITEM_LOA_LVT);
                if (remLoa <= 0)
                {
                    // Tự kiểm tra lại Santa -> Danh hiệu để xác minh đã mở khóa 100% trước khi mua
                    titlePhase = 0;
                    titlePhase0Checks++;
                    SetState("BUFF", "Xong Loa -> kiểm tra lại DH");
                    return;
                }
                if (now - lastLoaUseAt < 1700L) return;
                lastLoaUseAt = now;
                string chatMsg = "hihi" + titleLoaSeq;
                titleLoaSeq++;
                pendingClientInput = chatMsg;
                pendingClientInputReadyAt = now + 350L;
                SetState("BUFF", "Dùng Loa (" + chatMsg + ", còn " + remLoa + ")");
                GameEvents.UseItem(GameEvents.ID_ITEM_LOA_LVT);
                return;
            }

            // Phase 3: Santa -> Danh hiệu -> Sở hữu -> kích hoạt/mua [1673] (2 ngọc) & TỰ XÁC MINH!
            if (titlePhase == 3)
            {
                if (GameCanvas.panel.isShow && global::Char.myCharz().arrItemShop != null)
                {
                    var c = global::Char.myCharz();
                    GameEvents.InspectOwnedTitleInShop(GameEvents.ID_ITEM_TITLE_FAST_HAND, now);
                    if (c.arrItemShop.Length > 1 && GameCanvas.panel.currentTabIndex != 1)
                    {
                        GameCanvas.panel.currentTabIndex = 1;
                    }

                    // Tự kiểm tra kết quả sau khi đã bấm mua/kích hoạt
                    if (titleBuyAttempts > 0)
                    {
                        int curGem = c.luong + c.luongKhoa;
                        if (curGem < titleGemBeforeBuy || GameEvents.IsFastHandTitleActive(now))
                        {
                            OnTitleActivatedFromServer();
                            return;
                        }
                        if (now - lastBuyClickAt < 3000L)
                        {
                            SetState("BUFF", "Đang xác minh danh hiệu...");
                            return;
                        }
                        if (titleBuyAttempts >= 2)
                        {
                            AbortBuyTripCooldown("title", now, 900000L, "Hoàn tất kiểm tra DH -> đi farm");
                            return;
                        }
                    }

                    if (!GameEvents.HasEnoughGem(2))
                    {
                        NotifyLowGemOnce("Không đủ 2 ngọc mua danh hiệu Tay nhanh hơn não");
                        AbortBuyTripCooldown("title", now, 600000L, "Thiếu 2 ngọc DH -> đi farm");
                        return;
                    }

                    titleGemBeforeBuy = c.luong + c.luongKhoa;
                    titleBuyAttempts++;
                    lastBuyClickAt = now;
                    pendingClientInput = "1";
                    pendingClientInputReadyAt = now + 350L;
                    SetState("BUFF", "Kích hoạt danh hiệu [1673]");
                    Service.gI().buyItem(1, GameEvents.ID_ITEM_TITLE_FAST_HAND, 0);
                    return;
                }
                SetState("BUFF", "Mở mục Danh hiệu Santa");
                OpenNpcMenuAndSelect(npcId, new[] { "Danh hiệu", "Danh hieu" });
            }
        }

        private static void RunBuyGoldBar(long now)
        {
            if (global::Char.myCharz().xu < 500000000L)
            {
                AbortBuyTrip("goldbar", now, "Đã đổi Thỏi vàng xong");
                return;
            }
            if (!GoToMap5ShopNpc(now, "Qua Đảo Kame đổi Thỏi vàng (457)", out int npcId)) return;
            if (GameCanvas.panel.isShow)
            {
                if (shopPanelOpenedAt == 0) shopPanelOpenedAt = now;
                if (!GameEvents.ShopHasItem(457))
                {
                    if (now - shopPanelOpenedAt > 2500L && now - buyTripStartAt <= 18000L)
                    {
                        shopPanelOpenedAt = 0;
                        CloseNpcUi();
                        return;
                    }
                    if (now - buyTripStartAt > 18000L) AbortBuyTrip("goldbar", now, "Không thấy Thỏi vàng (457) trong shop");
                    return;
                }
                if (now - lastBuyClickAt > 1200L)
                {
                    lastBuyClickAt = now;
                    pendingClientInput = "1";
                    pendingClientInputReadyAt = now + 300L;
                    SetState("BUFF", "Mua Thỏi vàng (457)");
                    Service.gI().buyItem(0, 457, 0);
                }
                return;
            }
            shopPanelOpenedAt = 0;
            SetState("BUFF", "Mở Cửa hàng Santa đổi Thỏi vàng");
            OpenNpcMenuAndSelect(npcId, new[] { "Cửa hàng" });
        }

        private static void RunBuyTDLT(long now)
        {
            var prof = GameEvents.Profile;
            int price = GameEvents.GetTDLTGemPrice(prof.TDLTItemID);
            if (GameEvents.GetTimeTDLT() >= prof.TDLTMinMinutes || !GameEvents.HasEnoughGem(price))
            {
                AbortBuyTrip("tdlt", now, "Xong chuyến mua TDLT");
                return;
            }
            if (!GoToMap5ShopNpc(now, "Qua Đảo Kame mua TDLT", out int npcId)) return;
            if (GameCanvas.panel.isShow)
            {
                if (shopPanelOpenedAt == 0) shopPanelOpenedAt = now;
                if (global::Char.myCharz().arrItemShop == null || !GameEvents.ShopHasItem(prof.TDLTItemID))
                {
                    if (now - shopPanelOpenedAt > 2500L && now - buyTripStartAt <= 18000L)
                    {
                        shopPanelOpenedAt = 0;
                        CloseNpcUi();
                        return;
                    }
                    if (now - buyTripStartAt > 18000L) AbortBuyTrip("tdlt", now, "Không thấy TDLT (" + prof.TDLTItemID + ") trong shop");
                    return;
                }
                if (now - lastBuyClickAt > 1200L)
                {
                    lastBuyClickAt = now;
                    pendingClientInput = "1";
                    pendingClientInputReadyAt = now + 300L;
                    SetState("BUFF", "Mua TDLT (ID " + prof.TDLTItemID + ")");
                    Service.gI().buyItem(1, prof.TDLTItemID, 0);
                }
                return;
            }
            shopPanelOpenedAt = 0;
            SetState("BUFF", "Mở Cửa hàng Santa mua TDLT");
            OpenNpcMenuAndSelect(npcId, new[] { "Cửa hàng" });
        }

        private static void RunBuyLuckyGrass(long now)
        {
            var prof = GameEvents.Profile;
            int qty = System.Math.Max(1, prof.LuckyGrassQty);
            int curQty = GameEvents.GetItemQty(1635);
            int effectiveQty = curQty + (ItemTime.isExistItem(GameEvents.ID_ICON_ITEM_COBONLA) ? 1 : 0);
            // Tính cả Cỏ 4 lá vừa ăn bật buff (effectiveQty) để không bao giờ mua đúp!
            if (effectiveQty >= qty || !GameEvents.HasEnoughGem(10))
            {
                AbortBuyTrip("lucky", now, "Xong chuyến mua Cỏ 4 lá");
                return;
            }
            if (!GoToMap5ShopNpc(now, "Qua Đảo Kame mua Cỏ 4 lá", out int npcId)) return;
            if (GameCanvas.panel.isShow)
            {
                if (shopPanelOpenedAt == 0) shopPanelOpenedAt = now;
                // Nếu đang mở nhầm panel khác (vd hành trang / shop khác không có Cỏ 4 lá 1635) -> đóng mở lại đúng Cửa hàng Santa
                if (global::Char.myCharz().arrItemShop == null || !GameEvents.ShopHasItem(1635))
                {
                    if (now - shopPanelOpenedAt > 2500L && now - buyTripStartAt <= 18000L)
                    {
                        shopPanelOpenedAt = 0;
                        CloseNpcUi();
                        return;
                    }
                    if (now - buyTripStartAt > 18000L) AbortBuyTrip("lucky", now, "Không thấy Cỏ 4 lá (1635) trong shop Santa");
                    return;
                }
                if (now - lastBuyClickAt > 1400L)
                {
                    int need = System.Math.Max(1, qty - curQty);
                    int affordable = System.Math.Max(1, GameEvents.MaxAffordableQty(10));
                    int buyNow = System.Math.Min(need, affordable);
                    lastBuyClickAt = now;
                    pendingClientInput = buyNow.ToString();
                    pendingClientInputReadyAt = now + 300L;
                    SetState("BUFF", "Mua Cỏ 4 lá x" + buyNow);
                    Service.gI().buyItem(1, 1635, 0);
                }
                return;
            }
            shopPanelOpenedAt = 0;
            SetState("BUFF", "Mở Cửa hàng Santa mua Cỏ 4 lá");
            OpenNpcMenuAndSelect(npcId, new[] { "Cửa hàng" });
        }

        private static void RunBuyPrivateTicket(long now)
        {
            if (!GameEvents.IsConMam() || IsInPrivateMap() || GameEvents.HasPrivateTicketInBag())
            {
                AbortBuyTrip("ticket", now, "Xong chuyến mua Vé riêng tư");
                return;
            }
            if (!GoToMap5ShopNpc(now, "Qua Đảo Kame mua Vé riêng tư", out int npcId)) return;
            if (GameCanvas.panel.isShow)
            {
                if (shopPanelOpenedAt == 0) shopPanelOpenedAt = now;
                int price;
                int ticketShopId = GameEvents.FindPrivateTicketInShop(out price);
                if (ticketShopId < 0)
                {
                    if (now - shopPanelOpenedAt > 2500L && now - buyTripStartAt <= 18000L)
                    {
                        shopPanelOpenedAt = 0;
                        CloseNpcUi();
                        return;
                    }
                    if (now - buyTripStartAt > 18000L)
                    {
                        AbortBuyTripCooldown("ticket", now, 300000L, "Không thấy Vé riêng tư trong shop Santa");
                    }
                    return;
                }
                if (price > 0 && !GameEvents.HasEnoughGem(price))
                {
                    NotifyLowGemOnce("Không đủ " + price + " ngọc mua Vé riêng tư [" + ticketShopId + "]");
                    AbortBuyTripCooldown("ticket", now, 300000L, "Thiếu " + price + " ngọc mua Vé riêng tư");
                    return;
                }
                if (now - lastBuyClickAt > 1200L)
                {
                    lastBuyClickAt = now;
                    pendingClientInput = "1";
                    pendingClientInputReadyAt = now + 350L;
                    SetState("BUFF", "Mua Vé riêng tư [" + ticketShopId + "]");
                    Service.gI().buyItem(1, ticketShopId, 0);
                }
                return;
            }
            shopPanelOpenedAt = 0;
            SetState("BUFF", "Mở Cửa hàng Santa mua Vé riêng tư");
            OpenNpcMenuAndSelect(npcId, new[] { "Cửa hàng" });
        }

        private static bool GoToMap5ShopNpc(long now, string statusText, out int npcId)
        {
            npcId = 39;
            if (IsInPrivateMap())
            {
                SetState("BUFF", "Rời map riêng qua Đảo Kame");
                if (now - lastTicketUseAt > 3000L)
                {
                    lastTicketUseAt = now;
                    GameEvents.UseItem(1825);
                }
                return false;
            }
            if (TileMap.mapID != 5)
            {
                SetState("BUFF", statusText);
                XmapController.StartRunToMapId(5);
                return false;
            }
            Npc npc = GameEvents.FindNpcByNameOrId(39, "satan", "santa", "noel");
            if (npc != null)
            {
                npcId = npc.template.npcTemplateId;
                // Luôn gắn npcFocus = Santa để case 32 / confirmMenu nhận đúng NPC (kể cả khi đã đứng sẵn sát bên)
                global::Char.myCharz().npcFocus = npc;
                if (global::Char.abs(npc.cx - global::Char.myCharz().cx) > 30)
                {
                    if (now - lastNpcTeleAt > 600L)
                    {
                        lastNpcTeleAt = now;
                        GameEvents.TeleChar(npc.cx, GameEvents.GetYGround(npc.cx, npc.cy));
                    }
                    return false;
                }
            }
            return true;
        }

        private static void RunBuyCharm(long now)
        {
            var prof = GameEvents.Profile;
            if (!prof.AutoBuyCharm || prof.CharmIDs == null || prof.CharmIDs.Count == 0)
            {
                AbortBuyTrip("charm", now, "Xong mua Bùa");
                return;
            }
            int mapBua = global::Char.myCharz().cgender + 42;
            if (TileMap.mapID != mapBua)
            {
                SetState("BUFF", "Đi mua Bùa Hạt Mít");
                XmapController.StartRunToMapId(mapBua);
                return;
            }
            Npc npc = GameEvents.FindNpcByNameOrId(21, "hạt mít", "hat mit");
            int npcId = npc != null ? npc.template.npcTemplateId : 21;
            if (npc != null)
            {
                global::Char.myCharz().npcFocus = npc;
                if (global::Char.abs(npc.cx - global::Char.myCharz().cx) > 30)
                {
                    if (now - lastNpcTeleAt > 600L)
                    {
                        lastNpcTeleAt = now;
                        GameEvents.TeleChar(npc.cx, GameEvents.GetYGround(npc.cx, npc.cy));
                    }
                    return;
                }
            }
            if (GameCanvas.panel.isShow)
            {
                int charmId = GameEvents.GetNextCharmToBuy();
                if (charmId == -1)
                {
                    AbortBuyTrip("charm", now, "Đã mua đủ Bùa Hạt Mít");
                    return;
                }
                if (now - lastBuyClickAt > 1200L)
                {
                    lastBuyClickAt = now;
                    pendingClientInput = "1";
                    pendingClientInputReadyAt = now + 300L;
                    SetState("BUFF", "Mua Bùa ID=" + charmId);
                    Service.gI().buyItem(1, charmId, 0);
                }
                return;
            }
            string durCaption = prof.CharmDuration == "1m" ? "1 tháng" : (prof.CharmDuration == "8h" ? "8 giờ" : "1 giờ");
            OpenNpcMenuAndSelect(npcId, new[] { durCaption, "Cửa hàng", "OK", "Thưởng", "Từ chối", "Cho kẹo" });
        }

        private static void OpenNpcMenuAndSelect(int npcId, string[] captions)
        {
            long now = mSystem.currentTimeMillis();
            if (!GameCanvas.menu.showMenu)
            {
                // Chống spam openMenu liên tục 5 lần/giây và cho server 1.5s sau confirmMenu để bật Shop Panel!
                if (now - lastMenuOpenAt < 1300L || now - lastMenuSelectAt < 1500L) return;
                lastMenuOpenAt = now;
                Service.gI().openMenu(npcId);
                return;
            }
            if (GameCanvas.menu.menuItems == null || GameCanvas.menu.menuItems.size() == 0) return;
            if (now - lastMenuSelectAt < 900L) return;

            // Ưu tiên khớp chính xác caption trước (tránh nhầm "Cửa hàng" với "Cửa hàng Hạn sử dụng")
            for (int i = 0; i < GameCanvas.menu.menuItems.size(); i++)
            {
                Command cmd = (Command)GameCanvas.menu.menuItems.elementAt(i);
                if (cmd == null || string.IsNullOrEmpty(cmd.caption)) continue;
                string normCap = NormalizeMenuCaption(cmd.caption);
                if (captions.Any(c => !string.IsNullOrEmpty(c) && string.Equals(normCap, c, StringComparison.OrdinalIgnoreCase)))
                {
                    SelectMenuIndex(i, now);
                    return;
                }
            }
            for (int i = 0; i < GameCanvas.menu.menuItems.size(); i++)
            {
                Command cmd = (Command)GameCanvas.menu.menuItems.elementAt(i);
                if (cmd == null || string.IsNullOrEmpty(cmd.caption)) continue;
                string normCap = NormalizeMenuCaption(cmd.caption);
                if (captions.Any(c => !string.IsNullOrEmpty(c) && normCap.IndexOf(c, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    SelectMenuIndex(i, now);
                    return;
                }
            }
        }

        private static string NormalizeMenuCaption(string raw)
        {
            string s = (raw ?? "").Replace("\n", " ").Replace("\r", " ").Trim();
            while (s.Contains("  ")) s = s.Replace("  ", " ");
            return s;
        }

        private static void SelectMenuIndex(int index, long now)
        {
            GameCanvas.menu.menuSelectedItem = index;
            GameCanvas.menu.performSelect();
            GameCanvas.menu.doCloseMenu();
            try { ChatPopup.currChatPopup = null; } catch { }
            lastMenuSelectAt = now;
            lastMenuOpenAt = now;
        }

        public static void FixPetLazy()
        {
            if (!isSaoMayLuoiThe) return;
            var myChar = global::Char.myCharz();
            Skill skill1 = myChar.getSkill(myChar.nClass.skillTemplates[0]);
            isSaoMayLuoiThe = false;
            if (skill1 != null) Service.gI().selectSkill(skill1.template.id);
            if (myChar.cFlag != 8) Service.gI().getFlag(1, 8);
            MyVector vMe = new MyVector();
            vMe.addElement(myChar);
            Service.gI().sendPlayerAttack(new MyVector(), vMe, 2);
        }

        public static void AutoAskClanBean()
        {
            if (!GameEvents.Profile.AutoAskClanBean) return;
            if (global::Char.myCharz().clan == null) return;
            long now = mSystem.currentTimeMillis();
            if (now - lastAskBeanAt > 305000L)
            {
                Service.gI().clanMessage(1, "", -1);
                lastAskBeanAt = now;
            }
        }

        private static void AutoDropConfiguredItems()
        {
            var drops = GameEvents.Profile.DropItemIDs;
            if (drops == null || drops.Count == 0) return;
            long now = mSystem.currentTimeMillis();
            if (now - lastBagOpAt < 650L) return;
            var bag = global::Char.myCharz()?.arrItemBag;
            if (bag == null) return;
            for (int i = bag.Length - 1; i >= 0; i--)
            {
                Item it = bag[i];
                if (it != null && it.template != null && drops.Contains(it.template.id) && !GameEvents.ShouldStoreItem(it))
                {
                    lastBagOpAt = now;
                    Service.gI().useItem(2, 1, (sbyte)i, -1);
                    return;
                }
            }
        }

        private static void NotifyLowGemOnce(string reason)
        {
            long now = mSystem.currentTimeMillis();
            if (now - lastLowGemEventAt > 600000L)
            {
                lastLowGemEventAt = now;
                SocketClient.SendEvent("low_gem", reason);
            }
        }
    }
}
