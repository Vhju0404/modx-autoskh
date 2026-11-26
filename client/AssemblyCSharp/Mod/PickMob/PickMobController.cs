using AssemblyCSharp.Mod;
using System;
using System.Collections.Generic;
using System.Linq;
using static AssemblyCSharp.Mod.LoadData;

// Token: 0x02000125 RID: 293
public class PickMobController
{
    public static void Update()
    {
        global::Char @char = global::Char.myCharz();
        if (@char == null || global::Char.isLoadingMap || GameScr.isChangeZone)
        {
            return;
        }
        if (@char.statusMe == 14 || @char.cHP <= 0 || @char.isFreez || @char.blindEff || @char.sleepEff)
        {
            return;
        }

        // Sinh tồn ưu tiên cao nhất (không bị chặn bởi timer đánh/nhặt)
        if (@char.cHP <= @char.cHPFull * PickMob.HpBuff / 100 || @char.cMP <= @char.cMPFull * PickMob.MpBuff / 100)
        {
            GameScr.gI().doUseHP();
        }
        if (@char.cStamina <= @char.cMaxStamina * GameEvents.Profile.AutoPurpleGrape / 100)
        {
            GameEvents._useNhoTim();
        }

        // Chế độ Đánh Quái Xóa Anim (Tele sát quái + ra chiêu thẳng không chờ hiệu ứng)
        if (GameEvents.Profile.FastAttackNoAnim && !GameEvents.Profile.IsPetMode)
        {
            UpdateFastNoAnim(@char);
            return;
        }

        if (PickMobController.IsWaiting())
        {
            return;
        }

        bool flag = GameEvents.IsTDLTActive();
        bool flag2 = PickMob.IsTanSat && flag;
        if (PickMob.IsAutoPickItems && !flag2)
        {
            if (PickMobController.IsPickingItems)
            {
                if (PickMobController.IndexItemPick >= PickMobController.ItemPicks.Count)
                {
                    PickMobController.IsPickingItems = false;
                    return;
                }
                ItemMap itemMap = PickMobController.ItemPicks[PickMobController.IndexItemPick];
                switch (PickMobController.GetTpyePickItem(itemMap))
                {
                    case PickMobController.TpyePickItem.CanNotPickItem:
                        PickMobController.IndexItemPick++;
                        return;
                    case PickMobController.TpyePickItem.PickItemNormal:
                        Service.gI().charMove();
                        Service.gI().pickItem(itemMap.itemMapID);
                        itemMap.countAutoPick++;
                        PickMobController.IndexItemPick++;
                        PickMobController.Wait(500);
                        return;
                    case PickMobController.TpyePickItem.PickItemTDLT:
                        @char.cx = itemMap.xEnd;
                        @char.cy = itemMap.yEnd;
                        Service.gI().charMove();
                        Service.gI().pickItem(itemMap.itemMapID);
                        itemMap.countAutoPick++;
                        PickMobController.IndexItemPick++;
                        PickMobController.Wait(500);
                        return;
                    case PickMobController.TpyePickItem.PickItemTanSat:
                        PickMobController.Move(itemMap.xEnd, itemMap.yEnd);
                        @char.mobFocus = null;
                        PickMobController.Wait(500);
                        return;
                }
            }
            PickMobController.ItemPicks.Clear();
            PickMobController.IndexItemPick = 0;
            for (int i = 0; i < GameScr.vItemMap.size(); i++)
            {
                ItemMap itemMap2 = (ItemMap)GameScr.vItemMap.elementAt(i);
                if (PickMobController.GetTpyePickItem(itemMap2) != PickMobController.TpyePickItem.CanNotPickItem)
                {
                    PickMobController.ItemPicks.Add(itemMap2);
                }
            }
            if (PickMobController.ItemPicks.Count > 0)
            {
                PickMobController.IsPickingItems = true;
                return;
            }
        }

        if (PickMob.IsTanSat)
        {
            if (GameEvents.Profile.IsPetMode)
            {
                if (Account.cxEnd > 0 && Char.abs(@char.cx - Account.cxEnd) > 30)
                {
                    Move(Account.cxEnd, Account.cyEnd);
                }
                else if (GameEvents.Profile.PetWiggle)
                {
                    long nowMs = mSystem.currentTimeMillis();
                    if (nowMs - LastWiggleAt > 2200L)
                    {
                        LastWiggleAt = nowMs;
                        WiggleDir = !WiggleDir;
                        int baseX = Account.cxEnd > 0 ? Account.cxEnd : @char.cx;
                        int baseY = Account.cyEnd > 0 ? Account.cyEnd : @char.cy;
                        Move(baseX + (WiggleDir ? 12 : -12), baseY);
                    }
                }
                return;
            }
            if (@char.isCharge)
            {
                PickMobController.Wait(500);
                return;
            }
            @char.charFocus = null;
            @char.npcFocus = null;
            @char.itemFocus = null;
            @char.clearFocus(0);
            if (@char.mobFocus != null && !PickMobController.IsMobTanSat(@char.mobFocus))
            {
                @char.mobFocus = null;
            }
            if (@char.mobFocus == null)
            {
                @char.mobFocus = PickMobController.GetMobTanSat();
                if (flag && GameEvents.Profile.TeleToMobTDLT && @char.mobFocus != null)
                {
                    @char.cx = @char.mobFocus.xFirst - 24;
                    @char.cy = @char.mobFocus.yFirst;
                    Service.gI().charMove();
                }
            }
            if (@char.mobFocus != null)
            {
                AutoGame.OnNoMobTick(true);
                if (@char.skillInfoPaint() == null)
                {
                    Skill skillAttack = PickMobController.GetSkillAttack();

                    if (skillAttack != null && !skillAttack.paintCanNotUseSkill)
                    {
                        Mob mobFocus = @char.mobFocus;
                        mobFocus.x = mobFocus.xFirst;
                        mobFocus.y = mobFocus.yFirst;

                        if (Res.distance(mobFocus.xFirst, mobFocus.yFirst, @char.cx, @char.cy) <= 48)
                        {
                            if (!GameEvents.Profile.IsPetMode)
                            {
                                if (flag)
                                {
                                    Attack.toggleAutoAttack();
                                }
                                else
                                {
                                    Attack.stopAutoAttack();
                                    GameScr.gI().doDoubleClickToObj(mobFocus);
                                }
                            }
                        }
                        else
                        {
                            PickMobController.Move(mobFocus.xFirst - 24, mobFocus.yFirst);
                        }
                    }
                }
            }
            else
            {
                AutoGame.OnNoMobTick(false);
                if (!flag)
                {
                    Mob mobNext = PickMobController.GetMobNext();
                    if (mobNext != null)
                    {
                        PickMobController.Move(mobNext.xFirst - 24, mobNext.yFirst);
                    }
                }
            }
            PickMobController.Wait(200);
        }
    }

    // --- ĐỘNG CƠ ĐÁNH QUÁI XÓA ANIM TỐI ƯU TỐC ĐỘ ---

    private static void UpdateFastNoAnim(global::Char myChar)
    {
        if (!PickMob.IsTanSat && !PickMob.IsAutoPickItems)
        {
            return;
        }

        long now = mSystem.currentTimeMillis();
        bool hasTDLT = GameEvents.IsTDLTActive();

        // 1. Nhặt nhanh item dưới chân không cần tele & không ngắt nhịp đánh
        if (PickMob.IsAutoPickItems)
        {
            if (TryFastPickItem(myChar, now, hasTDLT))
            {
                return; // chỉ return nếu vừa tele đi nhặt đồ quý ở xa
            }
        }

        if (!PickMob.IsTanSat)
        {
            return;
        }

        // 2. Xóa sạch animation chiêu & đường chạy tồn đọng (0 frame anim, không bao giờ khựng)
        if (myChar.skillPaint != null || myChar.dart != null || myChar.arr != null)
        {
            myChar.skillPaint = null;
            myChar.skillPaintRandomPaint = null;
            myChar.dart = null;
            myChar.arr = null;
            myChar.effPaints = null;
        }
        if (myChar.currentMovePoint != null)
        {
            myChar.currentMovePoint = null;
            if (myChar.vMovePoints != null) myChar.vMovePoints.removeAllElements();
        }

        if (myChar.isCharge)
        {
            return;
        }

        myChar.charFocus = null;
        myChar.npcFocus = null;
        myChar.itemFocus = null;

        if (myChar.mobFocus != null && !IsMobTanSat(myChar.mobFocus))
        {
            myChar.mobFocus = null;
        }
        if (myChar.mobFocus == null)
        {
            myChar.mobFocus = GetMobTanSat();
        }

        // 3. Không có quái sống trong khu
        if (myChar.mobFocus == null)
        {
            AutoGame.OnNoMobTick(false);
            // Có TDLT (và tắt Dịch Quái TDLT) -> đứng yên 1 chỗ, không dịch thừa
            if (!hasTDLT || GameEvents.Profile.TeleToMobTDLT)
            {
                Mob mobNext = GetMobNext();
                if (mobNext != null && now - _lastTeleAt >= 250L)
                {
                    if (Res.abs(myChar.cx - mobNext.xFirst) > 48 || Res.abs(myChar.cy - mobNext.yFirst) > 48)
                    {
                        GameEvents.TeleChar(mobNext.xFirst, mobNext.yFirst);
                        _lastTeleAt = now;
                    }
                }
            }
            return;
        }

        AutoGame.OnNoMobTick(true);
        Mob target = myChar.mobFocus;
        int distX = Res.abs(myChar.cx - target.x);
        int distY = Res.abs(myChar.cy - target.y);

        // Chọn chiêu thông minh: ưu tiên chiêu tầm xa nếu quái đang xa (khỏi phải dịch!), hoặc chiêu hồi nhanh nhất
        Skill skill = GetFastSkillAttack(myChar, now, hasTDLT, distX, distY);
        myChar.cdir = (target.x >= myChar.cx) ? 1 : -1;

        // 4. Rẽ nhánh TDLT vs Không TDLT
        if (hasTDLT)
        {
            // Mặc định TeleToMobTDLT == false -> ĐỨNG YÊN 1 CHỖ ĐÁNH THẲNG TOÀN MAP (0ms đi tìm quái!)
            if (GameEvents.Profile.TeleToMobTDLT && (distX > 50 || distY > 50))
            {
                if (now - _lastTeleAt >= 100L)
                {
                    GameEvents.TeleChar(target.x, target.y);
                    _lastTeleAt = now;
                }
                else
                {
                    return;
                }
            }
        }
        else
        {
            // Không có TDLT: tính tầm đánh hiệu dụng theo dx/dy của chiêu
            Skill refSkill = skill ?? myChar.myskill;
            int rangeX = (refSkill != null && refSkill.dx > 0) ? System.Math.Max(55, (refSkill.dx * 5) / 3) : 65;
            int rangeY = (refSkill != null && refSkill.dy > 0) ? System.Math.Max(55, (refSkill.dy * 5) / 3) : 65;

            if (distX > rangeX || distY > rangeY)
            {
                if (now - _lastTeleAt < 100L)
                {
                    return;
                }
                bool isFlyMob = target.getTemplate() != null && (target.getTemplate().type == 4 || target.getTemplate().type == 5);
                int tx = isFlyMob ? target.x : (Res.abs(target.x - target.xFirst) <= 72 ? target.x : target.xFirst);
                int ty = isFlyMob ? target.y : GameEvents.GetYGround(tx, target.yFirst);
                GameEvents.TeleChar(tx, ty);
                _lastTeleAt = now;
                // KHÔNG return chờ settle! TCP giữ đúng thứ tự charMove() -> sendPlayerAttack() ngay trong cùng tick!
            }
        }

        if (skill == null || now - _lastAttackAt < 50L)
        {
            return;
        }

        // 5. Gửi thẳng gói đánh Message(54) không qua doFire/setSkillPaint (Xóa Anim 100%, chuẩn hy.cxl)
        if (myChar.myskill != skill)
        {
            myChar.myskill = skill;
            Service.gI().selectSkill(skill.template.id);
        }

        MyVector vMob = new MyVector();
        vMob.addElement(target);

        int maxTargets = System.Math.Max(1, skill.maxFight);
        if (maxTargets > 1 && GameScr.vMob != null)
        {
            int aoeX = (skill.dx > 0) ? System.Math.Max(55, (skill.dx * 5) / 3) : 65;
            int aoeY = (skill.dy > 0) ? System.Math.Max(55, (skill.dy * 5) / 3) : 65;
            for (int i = 0; i < GameScr.vMob.size() && vMob.size() < maxTargets; i++)
            {
                Mob other = (Mob)GameScr.vMob.elementAt(i);
                if (other == null || other == target || !IsMobTanSat(other)) continue;
                if (hasTDLT || (Res.abs(myChar.cx - other.x) <= aoeX && Res.abs(myChar.cy - other.y) <= aoeY))
                {
                    vMob.addElement(other);
                }
            }
        }

        Service.gI().sendPlayerAttack(vMob, new MyVector(), 1);
        skill.lastTimeUseThisSkill = now;
        skill.paintCanNotUseSkill = true;
        _lastAttackAt = now;
        _lastFastSkill = skill;
    }

    private static bool TryFastPickItem(global::Char myChar, long now, bool hasTDLT)
    {
        if (now - _lastPickAt < 250L || GameScr.vItemMap == null || GameScr.vItemMap.size() == 0)
        {
            return false;
        }

        if (_triedPick.Count > 200)
        {
            var expired = new List<int>();
            foreach (var kv in _triedPick)
            {
                if (now - kv.Value > 10000L) expired.Add(kv.Key);
            }
            for (int i = 0; i < expired.Count; i++) _triedPick.Remove(expired[i]);
        }

        ItemMap bestNear = null;
        ItemMap bestFar = null;
        int bestFarDist = int.MaxValue;

        for (int i = 0; i < GameScr.vItemMap.size(); i++)
        {
            ItemMap it = (ItemMap)GameScr.vItemMap.elementAt(i);
            if (it == null || it.template == null) continue;
            if (PickMob.IsLimitTimesPickItem && it.countAutoPick > PickMob.TimesAutoPickItemMax) continue;

            long triedAt;
            if (_triedPick.TryGetValue(it.itemMapID, out triedAt) && now - triedAt < 4000L) continue;

            bool isEquip = it.template.type >= 0 && it.template.type <= 4;
            bool isWanted = GameEvents.ItemMe(it.template.id) || isEquip;
            bool isGold = it.template.type == 9;

            if (hasTDLT && PickMob.IsTanSat)
            {
                // Khi có TDLT: chỉ nhặt trang bị / ngọc riêng trên đất, không bao giờ lao đi nhặt vàng (server TDLT tự hút)
                if (it.playerId != myChar.charID || !isWanted || isGold) continue;
            }
            else
            {
                bool mineOk = (it.playerId == myChar.charID || it.playerId == -1) && isWanted;
                if (PickMob.IsItemMe && !mineOk) continue;
                if (!FilterItemPick(it)) continue;
            }

            int dx = Res.abs(myChar.cx - it.xEnd);
            int dy = Res.abs(myChar.cy - it.yEnd);
            if (dx <= 65 && dy <= 65)
            {
                bestNear = it;
                break;
            }
            // Đồ ở xa: chỉ tele nhặt đồ giá trị (trang bị / item chọn / pick_all), bỏ qua vàng rác để khỏi tele loạn
            if (!GameEvents.Profile.PickAll && isGold) continue;
            // Nếu có TDLT & đang tắt Dịch Quái TDLT -> chỉ tele nhặt nếu đúng là trang bị (type 0..4) hoặc ngọc!
            if (hasTDLT && !GameEvents.Profile.TeleToMobTDLT && !isEquip && !GameEvents.ItemMe(it.template.id)) continue;

            int dist = dx + dy;
            if (dist < bestFarDist)
            {
                bestFarDist = dist;
                bestFar = it;
            }
        }

        // Đồ ngay dưới chân -> gửi pickItem và trả false để KHÔNG ngắt nhịp đánh quái!
        if (bestNear != null)
        {
            _lastPickAt = now;
            _triedPick[bestNear.itemMapID] = now;
            bestNear.countAutoPick++;
            Service.gI().pickItem(bestNear.itemMapID);
            return false;
        }

        if (bestFar == null)
        {
            return false;
        }

        _lastPickAt = now;
        _triedPick[bestFar.itemMapID] = now;
        bestFar.countAutoPick++;
        GameEvents.TeleChar(bestFar.xEnd, bestFar.yEnd);
        Service.gI().pickItem(bestFar.itemMapID);
        return true;
    }

    private static readonly sbyte[] IdSkillsPunchOnly = new sbyte[] { 0, 2, 4 };
    private static readonly sbyte[] IdSkillsMeleeOnly = new sbyte[] { 0, 2, 4, 9, 17 };

    public static void OnProfileChanged(FarmProfile prof)
    {
        _lastFastSkill = null;
        string mode = (prof != null && !string.IsNullOrEmpty(prof.AttackSkillMode)) ? prof.AttackSkillMode : "punch_only";
        PickMob.IdSkillsTanSat.Clear();
        if (mode == "punch_only")
        {
            PickMob.IdSkillsTanSat.AddRange(IdSkillsPunchOnly);
        }
        else if (mode == "melee_only")
        {
            PickMob.IdSkillsTanSat.AddRange(IdSkillsMeleeOnly);
        }
        else
        {
            PickMob.IdSkillsTanSat.AddRange(new sbyte[] { 0, 1, 2, 3, 4, 5, 9, 17 });
        }
    }

    public static string GetActiveSkillLabel(global::Char myChar)
    {
        if (myChar == null) return "";
        Skill s = _lastFastSkill ?? myChar.myskill;
        string name = (s != null && s.template != null && !string.IsNullOrEmpty(s.template.name))
            ? s.template.name : "Chưa chọn";
        var prof = GameEvents.Profile;
        string mode = (prof != null && !string.IsNullOrEmpty(prof.AttackSkillMode)) ? prof.AttackSkillMode : "punch_only";
        if (mode == "punch_only") return name + " (Chỉ Đấm)";
        if (mode == "melee_only") return name + " (Đấm + Liên hoàn)";
        if (mode == "slots")
        {
            var sl = prof.AttackSkillSlots;
            string slTxt = (sl != null && sl.Count > 0) ? string.Join(",", sl.Select(x => x.ToString()).ToArray()) : "1";
            return name + " (Ô " + slTxt + ")";
        }
        return name + " (Tự động Đấm+Chưởng)";
    }

    private static bool IsFastAttackSkill(Skill s)
    {
        if (s == null || s.template == null) return false;
        sbyte id = s.template.id;
        if (IdSkillsCanNotAttack.Contains(id)) return false;
        if (id == 6 || id == 8 || id == 12 || id == 13 || id == 19 || id == 21) return false;
        int t = s.template.type;
        return t != 2 && t != 3 && t != 4;
    }

    private static bool HasAnySlotAttackSkill(FarmProfile prof)
    {
        if (prof == null || prof.AttackSkillSlots == null || GameScr.keySkill == null) return false;
        for (int i = 0; i < prof.AttackSkillSlots.Count; i++)
        {
            int idx = prof.AttackSkillSlots[i] - 1;
            if (idx >= 0 && idx < GameScr.keySkill.Length && IsFastAttackSkill(GameScr.keySkill[idx]))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsSkillAllowedByProfile(global::Char myChar, Skill s)
    {
        if (!IsFastAttackSkill(s)) return false;
        var prof = GameEvents.Profile;
        string mode = (prof != null && !string.IsNullOrEmpty(prof.AttackSkillMode)) ? prof.AttackSkillMode : "punch_only";
        sbyte id = s.template.id;

        if (mode == "punch_only")
        {
            return id == 0 || id == 2 || id == 4;
        }
        if (mode == "melee_only")
        {
            return id == 0 || id == 2 || id == 4 || id == 9 || id == 17;
        }
        if (mode == "slots")
        {
            if (prof == null || prof.AttackSkillSlots == null || prof.AttackSkillSlots.Count == 0 || !HasAnySlotAttackSkill(prof))
            {
                return id == 0 || id == 2 || id == 4;
            }
            for (int i = 0; i < prof.AttackSkillSlots.Count; i++)
            {
                int idx = prof.AttackSkillSlots[i] - 1;
                if (idx >= 0 && idx < GameScr.keySkill.Length &&
                    GameScr.keySkill[idx] != null && GameScr.keySkill[idx].template != null &&
                    GameScr.keySkill[idx].template.id == id)
                {
                    return true;
                }
            }
            return false;
        }
        return PickMob.IdSkillsTanSat.Count == 0 || PickMob.IdSkillsTanSat.Contains(id);
    }

    private static List<Skill> CollectAllowedAttackSkills(global::Char myChar)
    {
        var list = new List<Skill>();
        if (myChar == null) return list;
        var prof = GameEvents.Profile;
        string mode = (prof != null && !string.IsNullOrEmpty(prof.AttackSkillMode)) ? prof.AttackSkillMode : "punch_only";

        if (mode == "slots" && prof != null && prof.AttackSkillSlots != null && GameScr.keySkill != null && HasAnySlotAttackSkill(prof))
        {
            for (int i = 0; i < prof.AttackSkillSlots.Count; i++)
            {
                int idx = prof.AttackSkillSlots[i] - 1;
                if (idx >= 0 && idx < GameScr.keySkill.Length)
                {
                    Skill sk = GameScr.keySkill[idx];
                    if (IsFastAttackSkill(sk) && !list.Contains(sk))
                    {
                        list.Add(sk);
                    }
                }
            }
            if (list.Count > 0) return list;
        }

        sbyte[] ids;
        if (mode == "punch_only") ids = IdSkillsPunchOnly;
        else if (mode == "melee_only") ids = IdSkillsMeleeOnly;
        else ids = PickMob.IdSkillsTanSat.ToArray();

        SkillTemplate tmpl = new SkillTemplate();
        for (int i = 0; i < ids.Length; i++)
        {
            tmpl.id = ids[i];
            Skill s = myChar.getSkill(tmpl);
            if (IsFastAttackSkill(s) && !list.Contains(s))
            {
                list.Add(s);
            }
        }
        return list;
    }

    // Tính nhịp hồi chiêu an toàn:
    // - Skill đặc biệt (7, 18, 20, 22, 23): coolDown + 500ms
    // - Skill đánh thường: max(415ms, (coolDown * 6) / 5)
    private static long GetSkillAttackDelayMs(Skill s)
    {
        if (s == null || s.template == null) return 415L;
        sbyte id = s.template.id;
        long cd = s.coolDown;
        if (id == 7 || id == 18 || id == 20 || id == 22 || id == 23)
        {
            return cd + 500L;
        }
        long scaled = cd * 6L;
        return scaled >= 2075L ? (scaled / 5L) : 415L;
    }

    private static bool IsSkillReadyFast(global::Char myChar, Skill s, long now)
    {
        if (!IsSkillAllowedByProfile(myChar, s)) return false;
        if (now - s.lastTimeUseThisSkill < GetSkillAttackDelayMs(s)) return false;
        s.paintCanNotUseSkill = false;
        return myChar.cMP >= GetManaUseSkill(s);
    }

    private static Skill GetFastSkillAttack(global::Char myChar, long now, bool hasTDLT, int distX, int distY)
    {
        var allowed = CollectAllowedAttackSkills(myChar);

        // 1. Khi không có TDLT và quái đang ở xa -> ưu tiên chiêu tầm xa (trong danh sách được phép) bắn tới khỏi cần tele!
        if (!hasTDLT && (distX > 70 || distY > 70))
        {
            Skill bestInRange = null;
            for (int i = 0; i < allowed.Count; i++)
            {
                Skill s = allowed[i];
                if (!IsSkillReadyFast(myChar, s, now)) continue;
                int rx = s.dx > 0 ? (s.dx * 5) / 3 : 65;
                int ry = s.dy > 0 ? (s.dy * 5) / 3 : 65;
                if (distX <= rx && distY <= ry)
                {
                    if (bestInRange == null || s.coolDown < bestInRange.coolDown)
                    {
                        bestInRange = s;
                    }
                }
            }
            if (bestInRange != null) return bestInRange;
        }

        // 2. Nếu chiêu hiện tại (hoặc _lastFastSkill) ĐƯỢC PHÉP & đã hồi xong -> dùng luôn (0 tốn gói selectSkill)
        if (myChar.myskill != null && IsSkillReadyFast(myChar, myChar.myskill, now))
        {
            return myChar.myskill;
        }
        if (_lastFastSkill != null && IsSkillReadyFast(myChar, _lastFastSkill, now))
        {
            return _lastFastSkill;
        }

        // 3. Chọn chiêu hồi NHANH NHẤT trong đúng tập chiêu người dùng chọn (tuyệt đối KHÔNG nhảy sang Chưởng nếu đã khóa Chỉ Đấm!)
        Skill best = null;
        for (int i = 0; i < allowed.Count; i++)
        {
            Skill s = allowed[i];
            if (!IsSkillReadyFast(myChar, s, now)) continue;
            if (best == null || s.coolDown < best.coolDown)
            {
                best = s;
            }
        }
        if (best != null) return best;

        // 4. Chỉ fallback sang vSkill nếu nhân vật không sở hữu bất kỳ skill nào trong tập được phép (tránh kẹt acc lạ)
        if (allowed.Count == 0 && myChar.vSkill != null)
        {
            for (int i = 0; i < myChar.vSkill.size(); i++)
            {
                Skill s = (Skill)myChar.vSkill.elementAt(i);
                if (!IsFastAttackSkill(s)) continue;
                if (now - s.lastTimeUseThisSkill < GetSkillAttackDelayMs(s)) continue;
                if (myChar.cMP < GetManaUseSkill(s)) continue;
                if (best == null || s.coolDown < best.coolDown)
                {
                    best = s;
                }
            }
        }
        return best;
    }

    public static void Move(int x, int y)
    {
        global::Char @char = global::Char.myCharz();
        if (ItemTime.isExistItem(4387) || GameEvents.Profile.FastAttackNoAnim)
        {
            GameEvents.TeleChar(x, y);
            return;
        }
        if (PickMob.IsVuotDiaHinh)
        {
            GameScr.gI().checkClickMoveTo(x, y);
            return;
        }
    }

    public static PickMobController.TpyePickItem GetTpyePickItem(ItemMap itemMap)
    {
        global::Char @char = global::Char.myCharz();
        bool flag = (itemMap.playerId == @char.charID || itemMap.playerId == -1) && (GameEvents.ItemMe(itemMap.template.id) || (itemMap.template.type >= 0 && itemMap.template.type <= 4));
        if (PickMob.IsItemMe && !flag)
        {
            return PickMobController.TpyePickItem.CanNotPickItem;
        }
        if (PickMob.IsLimitTimesPickItem && itemMap.countAutoPick > PickMob.TimesAutoPickItemMax)
        {
            return PickMobController.TpyePickItem.CanNotPickItem;
        }
        if (!PickMobController.FilterItemPick(itemMap))
        {
            return PickMobController.TpyePickItem.CanNotPickItem;
        }
        if (Res.abs(@char.cx - itemMap.xEnd) < 50 && Res.abs(@char.cy - itemMap.yEnd) < 50)
        {
            return PickMobController.TpyePickItem.PickItemNormal;
        }
        if (ItemTime.isExistItem(4387))
        {
            return PickMobController.TpyePickItem.PickItemTDLT;
        }
        if (PickMob.IsTanSat)
        {
            return PickMobController.TpyePickItem.PickItemTanSat;
        }
        return PickMobController.TpyePickItem.CanNotPickItem;
    }

    public static bool FilterItemPick(ItemMap itemMap)
    {
        return (PickMob.IdItemPicks.Count == 0 || PickMob.IdItemPicks.Contains(itemMap.template.id)) && (PickMob.IdItemBlocks.Count == 0 || !PickMob.IdItemBlocks.Contains(itemMap.template.id)) && (PickMob.TypeItemPicks.Count == 0 || PickMob.TypeItemPicks.Contains(itemMap.template.type)) && (PickMob.TypeItemBlock.Count == 0 || !PickMob.TypeItemBlock.Contains(itemMap.template.type));
    }

    public static Mob GetMobTanSat()
    {
        Mob result = null;
        int num = int.MaxValue;
        global::Char @char = global::Char.myCharz();
        for (int i = 0; i < GameScr.vMob.size(); i++)
        {
            Mob mob = (Mob)GameScr.vMob.elementAt(i);
            int num2 = (mob.xFirst - @char.cx) * (mob.xFirst - @char.cx) + (mob.yFirst - @char.cy) * (mob.yFirst - @char.cy);
            if (!(mob is NewBoss) && PickMobController.IsMobTanSat(mob) && num2 < num)
            {
                result = mob;
                num = num2;
            }
        }
        return result;
    }

    public static Mob GetMobNext()
    {
        Mob result = null;
        long num = mSystem.currentTimeMillis();
        for (int i = 0; i < GameScr.vMob.size(); i++)
        {
            Mob mob = (Mob)GameScr.vMob.elementAt(i);
            if (PickMobController.IsMobNext(mob) && mob.timeLastDie < num)
            {
                result = mob;
                num = mob.timeLastDie;
            }
        }
        return result;
    }

    public static bool IsMobTanSat(Mob mob)
    {
        if (mob == null || mob.status == 0 || mob.status == 1 || mob.hp <= 0 || mob.mobName == "" || mob.mobName == " " || mob.isMobMe || mob.isBigBoss())
        {
            return false;
        }
        return mob.levelBoss == 0 && PickMobController.FilterMobTanSat(mob);
    }

    public static bool IsMobNext(Mob mob)
    {
        if (mob == null || mob.isMobMe)
        {
            return false;
        }
        if (!PickMobController.FilterMobTanSat(mob))
        {
            return false;
        }
        if (PickMob.IsNeSieuQuai && !ItemTime.isExistItem(4387) && mob.getTemplate() != null && mob.getTemplate().hp >= 3000)
        {
            if (mob.levelBoss != 0)
            {
                Mob mob2 = null;
                bool flag = false;
                for (int i = 0; i < GameScr.vMob.size(); i++)
                {
                    mob2 = (Mob)GameScr.vMob.elementAt(i);
                    if (mob2.countDie == 10 && (mob2.status == 0 || mob2.status == 1))
                    {
                        flag = true;
                        break;
                    }
                }
                if (!flag)
                {
                    return false;
                }
                mob.timeLastDie = mob2.timeLastDie;
            }
            else if (mob.countDie == 10 && (mob.status == 0 || mob.status == 1))
            {
                return false;
            }
        }
        return true;
    }

    public static bool FilterMobTanSat(Mob mob)
    {
        return (PickMob.IdMobsTanSat.Count == 0 || PickMob.IdMobsTanSat.Contains(mob.mobId)) && (PickMob.TypeMobsTanSat.Count == 0 || PickMob.TypeMobsTanSat.Contains(mob.templateId));
    }

    public static Skill GetSkillAttack()
    {
        global::Char myChar = global::Char.myCharz();
        var allowed = CollectAllowedAttackSkills(myChar);
        Skill skill = null;
        for (int i = 0; i < allowed.Count; i++)
        {
            Skill skill2 = allowed[i];
            if (PickMobController.IsSkillBetter(skill2, skill))
            {
                skill = skill2;
            }
        }
        return skill;
    }

    public static bool IsSkillBetter(Skill SkillBetter, Skill skill)
    {
        if (SkillBetter == null)
        {
            return false;
        }
        if (!PickMobController.CanUseSkill(SkillBetter))
        {
            return false;
        }
        bool flag = skill != null && ((SkillBetter.template.id == 17 && skill.template.id == 2) || (SkillBetter.template.id == 9 && skill.template.id == 0));
        return skill == null || skill.coolDown < SkillBetter.coolDown || flag;
    }

    public static bool CanUseSkill(Skill skill)
    {
        if (mSystem.currentTimeMillis() - skill.lastTimeUseThisSkill > (long)skill.coolDown)
        {
            skill.paintCanNotUseSkill = false;
        }
        return (!skill.paintCanNotUseSkill || PickMobController.IdSkillsMelee.Contains(skill.template.id)) && !PickMobController.IdSkillsCanNotAttack.Contains(skill.template.id) && global::Char.myCharz().cMP >= PickMobController.GetManaUseSkill(skill);
    }

    public static int GetManaUseSkill(Skill skill)
    {
        if (skill.template.manaUseType == 2)
        {
            return 1;
        }
        if (skill.template.manaUseType == 1)
        {
            return skill.manaUse * global::Char.myCharz().cMPFull / 100;
        }
        return skill.manaUse;
    }

    public static int GetYsd(int xsd)
    {
        Char myChar = Char.myCharz();
        int dmin = TileMap.pxh;
        int d;
        int ysdBest = -1;
        for (int i = 24; i < TileMap.pxh; i += 24)
        {
            if (TileMap.tileTypeAt(xsd, i, 2))
            {
                d = Res.abs(i - myChar.cy);
                if (d < dmin)
                {
                    dmin = d;
                    ysdBest = i;
                }
            }
        }
        return ysdBest;
    }

    public static int[] GetPointYsdMax(int xStart, int xEnd)
    {
        int ysdMin = TileMap.pxh;
        int x = -1;

        if (xStart > xEnd)
        {
            for (int i = xEnd; i < xStart; i += 24)
            {
                int ysd = GetYsd(i);
                if (ysd < ysdMin)
                {
                    ysdMin = ysd;
                    x = i;
                }
            }
        }
        else
        {
            for (int i = xEnd; i > xStart; i -= 24)
            {
                int ysd = GetYsd(i);
                if (ysd < ysdMin)
                {
                    ysdMin = ysd;
                    x = i;
                }
            }
        }
        int[] vs = { x, ysdMin };
        return vs;
    }

    public static void Wait(int time)
    {
        PickMobController.IsWait = true;
        PickMobController.TimeStartWait = mSystem.currentTimeMillis();
        PickMobController.TimeWait = (long)time;
    }

    public static bool IsWaiting()
    {
        if (PickMobController.IsWait && mSystem.currentTimeMillis() - PickMobController.TimeStartWait >= PickMobController.TimeWait)
        {
            PickMobController.IsWait = false;
        }
        return PickMobController.IsWait;
    }

    public const int TIME_REPICKITEM = 500;
    public const int TIME_DELAY_TANSAT = 500;
    public const int ID_ICON_ITEM_TDLT = 4387;

    public static readonly sbyte[] IdSkillsMelee = new sbyte[] { 0, 9, 2, 17, 4 };
    public static readonly sbyte[] IdSkillsCanNotAttack = new sbyte[] { 10, 11, 14, 23, 7 };

    public static readonly PickMobController _Instance = new PickMobController();

    public static bool IsPickingItems;
    public static bool IsWait;
    public static long TimeStartWait;
    public static long TimeWait;
    public static List<ItemMap> ItemPicks = new List<ItemMap>();
    public static int IndexItemPick = 0;

    private static long LastWiggleAt = 0;
    private static bool WiggleDir = false;

    private static long _lastAttackAt = 0;
    private static long _lastTeleAt = 0;
    private static long _lastPickAt = 0;
    private static Skill _lastFastSkill = null;
    private static readonly Dictionary<int, long> _triedPick = new Dictionary<int, long>();

    public enum TpyePickItem
    {
        CanNotPickItem,
        PickItemNormal,
        PickItemTDLT,
        PickItemTanSat
    }
}
