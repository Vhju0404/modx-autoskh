using System.Collections.Generic;
using LitJson;

namespace AssemblyCSharp.Mod
{
    /// <summary>
    /// Cấu hình FarmProfile đồng bộ 1:1 với Go Manager (parse bằng LitJson thuần .NET 3.5 cho Unity 5.6).
    /// </summary>
    public class FarmProfile
    {
        public string Id = "default";
        public string Name = "Mặc định";
        public string FarmMode = "sp_tdlt"; // sp_tdlt | sp_normal | up_pet | private_ticket

        public int DefaultMapTD = 1;
        public int DefaultMapNM = 8;
        public int DefaultMapXD = 15;
        public string ZoneStrategy = "empty"; // empty | fixed
        public int FixedZoneID = 0;
        public int MaxPlayersInZone = 3;
        public int NoExpSwitchSec = 35;
        public int NoExpRestartSec = 95;

        public bool AutoBuyTDLT = true;
        public int TDLTItemID = 521;
        public int TDLTMinMinutes = 20;
        public bool AutoBuyLuckyGrass = false;
        public int LuckyGrassQty = 1;
        public bool AutoBuyPrivateTicket = false;
        public bool AutoBuyGoldBar = true;
        public bool AutoGetTitle = false;
        public bool PetWiggle = false;
        public int MinGemKeep = 5;
        public bool AutoBuyCharm = false;
        public string CharmDuration = "1h"; // 1h | 8h | 1m
        public List<int> CharmIDs = new List<int>();
        public int AutoBeanHP = 25;
        public int AutoBeanMP = 20;
        public int AutoPurpleGrape = 10;
        public bool AutoAskClanBean = true;
        public bool AutoUpPotential = false;
        public int TargetHP = 0;
        public int TargetKI = 0;
        public int TargetSD = 0;

        public bool KeepAllSKH = true;
        public List<string> KeepSKHSets = new List<string>();
        public int MinKeepStars = 3;
        public bool AutoStoreHouse = true;
        public string OnBoxFull = "keep_farming"; // keep_farming | mark_done_rotate | stop_at_home
        public bool AutoSellTrash = true;
        public bool PickAll = false;
        public List<int> PickItemIDs = new List<int> { 77, 861 };
        public List<int> SellItemIDs = new List<int>();
        public List<int> DropItemIDs = new List<int>();

        public float GameSpeed = 2.0f;
        public int CharSpeed = 6;
        public int TargetFPS = 15;
        public bool BlackScreenVPS = false;
        public bool ShowMobGraphics = true;
        public bool ShowCharGraphics = true;
        public bool FastAttackNoAnim = true;
        public string AttackSkillMode = "punch_only"; // punch_only | melee_only | auto_all | slots
        public List<int> AttackSkillSlots = new List<int> { 1 };
        public bool TeleToMobTDLT = false;
        public bool FreezeMobs = true;
        public bool HorizontalInventory = false;
        public bool HeadlessMode = false;

        public bool IsTDLTMode => FarmMode == "sp_tdlt" || FarmMode == "private_ticket";
        public bool IsPetMode => FarmMode == "up_pet";
        public bool IsPrivateTicketMode => FarmMode == "private_ticket";

        public static FarmProfile FromJsonData(JsonData d)
        {
            if (d == null || !d.IsObject) return new FarmProfile();
            var p = new FarmProfile();
            p.Id = GetStr(d, "id", p.Id);
            p.Name = GetStr(d, "name", p.Name);
            p.FarmMode = GetStr(d, "farm_mode", p.FarmMode);

            p.DefaultMapTD = GetInt(d, "default_map_td", p.DefaultMapTD);
            p.DefaultMapNM = GetInt(d, "default_map_nm", p.DefaultMapNM);
            p.DefaultMapXD = GetInt(d, "default_map_xd", p.DefaultMapXD);
            p.ZoneStrategy = GetStr(d, "zone_strategy", p.ZoneStrategy);
            p.FixedZoneID = GetInt(d, "fixed_zone_id", p.FixedZoneID);
            p.MaxPlayersInZone = GetInt(d, "max_players_zone", p.MaxPlayersInZone);
            p.NoExpSwitchSec = GetInt(d, "no_exp_switch_sec", p.NoExpSwitchSec);
            p.NoExpRestartSec = GetInt(d, "no_exp_restart_sec", p.NoExpRestartSec);

            p.AutoBuyTDLT = GetBool(d, "auto_buy_tdlt", p.AutoBuyTDLT);
            p.TDLTItemID = GetInt(d, "tdlt_item_id", p.TDLTItemID);
            p.TDLTMinMinutes = GetInt(d, "tdlt_min_minutes", p.TDLTMinMinutes);
            p.AutoBuyLuckyGrass = GetBool(d, "auto_buy_lucky", p.AutoBuyLuckyGrass);
            p.LuckyGrassQty = GetInt(d, "lucky_grass_qty", p.LuckyGrassQty);
            p.AutoBuyPrivateTicket = GetBool(d, "auto_buy_ticket", p.AutoBuyPrivateTicket);
            p.AutoBuyGoldBar = GetBool(d, "auto_buy_goldbar", p.AutoBuyGoldBar);
            p.AutoGetTitle = GetBool(d, "auto_get_title", p.AutoGetTitle);
            p.PetWiggle = GetBool(d, "pet_wiggle", p.PetWiggle);
            p.MinGemKeep = GetInt(d, "min_gem_keep", p.MinGemKeep);
            p.AutoBuyCharm = GetBool(d, "auto_buy_charm", p.AutoBuyCharm);
            p.CharmDuration = GetStr(d, "charm_duration", p.CharmDuration);
            p.CharmIDs = GetIntList(d, "charm_ids");
            p.AutoBeanHP = GetInt(d, "auto_bean_hp", p.AutoBeanHP);
            p.AutoBeanMP = GetInt(d, "auto_bean_mp", p.AutoBeanMP);
            p.AutoPurpleGrape = GetInt(d, "auto_purple_grape", p.AutoPurpleGrape);
            p.AutoAskClanBean = GetBool(d, "auto_ask_clan_bean", p.AutoAskClanBean);
            p.AutoUpPotential = GetBool(d, "auto_up_potential", p.AutoUpPotential);
            p.TargetHP = GetInt(d, "target_hp", p.TargetHP);
            p.TargetKI = GetInt(d, "target_ki", p.TargetKI);
            p.TargetSD = GetInt(d, "target_sd", p.TargetSD);

            p.KeepAllSKH = GetBool(d, "keep_all_skh", p.KeepAllSKH);
            p.KeepSKHSets = GetStrList(d, "keep_skh_sets");
            p.MinKeepStars = GetInt(d, "min_keep_stars", p.MinKeepStars);
            p.AutoStoreHouse = GetBool(d, "auto_store_house", p.AutoStoreHouse);
            p.OnBoxFull = GetStr(d, "on_box_full", p.OnBoxFull);
            p.AutoSellTrash = GetBool(d, "auto_sell_trash", p.AutoSellTrash);
            p.PickAll = GetBool(d, "pick_all", p.PickAll);
            p.PickItemIDs = GetIntList(d, "pick_item_ids");
            p.SellItemIDs = GetIntList(d, "sell_item_ids");
            p.DropItemIDs = GetIntList(d, "drop_item_ids");

            p.GameSpeed = GetFloat(d, "game_speed", p.GameSpeed);
            p.CharSpeed = GetInt(d, "char_speed", p.CharSpeed);
            p.TargetFPS = GetInt(d, "target_fps", p.TargetFPS);
            p.BlackScreenVPS = GetBool(d, "black_screen_vps", p.BlackScreenVPS);
            p.ShowMobGraphics = GetBool(d, "show_mob_graphics", p.ShowMobGraphics);
            p.ShowCharGraphics = GetBool(d, "show_char_graphics", p.ShowCharGraphics);
            p.FastAttackNoAnim = GetBool(d, "fast_attack_no_anim", p.FastAttackNoAnim);
            p.AttackSkillMode = GetStr(d, "attack_skill_mode", p.AttackSkillMode);
            if (string.IsNullOrEmpty(p.AttackSkillMode)) p.AttackSkillMode = "punch_only";
            var parsedSlots = GetIntList(d, "attack_skill_slots");
            if (parsedSlots.Count > 0) p.AttackSkillSlots = parsedSlots;
            p.TeleToMobTDLT = GetBool(d, "tele_to_mob_tdlt", p.TeleToMobTDLT);
            p.FreezeMobs = GetBool(d, "freeze_mobs", p.FreezeMobs);
            p.HorizontalInventory = GetBool(d, "horizontal_inventory", p.HorizontalInventory);
            p.HeadlessMode = GetBool(d, "headless_mode", p.HeadlessMode);
            return p;
        }

        private static bool HasKey(JsonData d, string k)
        {
            return d != null && d.IsObject && ((System.Collections.IDictionary)d).Contains(k) && d[k] != null;
        }

        private static string GetStr(JsonData d, string k, string def)
        {
            return HasKey(d, k) ? d[k].ToString() : def;
        }

        private static int GetInt(JsonData d, string k, int def)
        {
            if (!HasKey(d, k)) return def;
            int v;
            return int.TryParse(d[k].ToString(), out v) ? v : def;
        }

        private static float GetFloat(JsonData d, string k, float def)
        {
            if (!HasKey(d, k)) return def;
            float v;
            return float.TryParse(d[k].ToString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v) ? v : def;
        }

        private static bool GetBool(JsonData d, string k, bool def)
        {
            if (!HasKey(d, k)) return def;
            bool v;
            return bool.TryParse(d[k].ToString(), out v) ? v : def;
        }

        private static List<int> GetIntList(JsonData d, string k)
        {
            var list = new List<int>();
            if (!HasKey(d, k) || !d[k].IsArray) return list;
            for (int i = 0; i < d[k].Count; i++)
            {
                int v;
                if (d[k][i] != null && int.TryParse(d[k][i].ToString(), out v)) list.Add(v);
            }
            return list;
        }

        private static List<string> GetStrList(JsonData d, string k)
        {
            var list = new List<string>();
            if (!HasKey(d, k) || !d[k].IsArray) return list;
            for (int i = 0; i < d[k].Count; i++)
            {
                if (d[k][i] != null) list.Add(d[k][i].ToString());
            }
            return list;
        }
    }
}
