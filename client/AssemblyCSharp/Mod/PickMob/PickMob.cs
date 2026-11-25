using System;
using System.Collections.Generic;
using System.Text;

// Token: 0x0200006D RID: 109
public class PickMob
{
    // Token: 0x06000579 RID: 1401 RVA: 0x00064D74 File Offset: 0x00062F74
    public static bool Chat(string text)
    {
        bool flag = text == "add";
        if (flag)
        {
            Mob mobFocus = global::Char.myCharz().mobFocus;
            ItemMap itemFocus = global::Char.myCharz().itemFocus;
            bool flag2 = mobFocus != null;
            if (flag2)
            {
                bool flag3 = PickMob.IdMobsTanSat.Contains(mobFocus.mobId);
                if (flag3)
                {
                    PickMob.IdMobsTanSat.Remove(mobFocus.mobId);
                    GameScr.info1.addInfo("Đã xoá mob: " + mobFocus.mobId.ToString(), 0);
                }
                else
                {
                    PickMob.IdMobsTanSat.Add(mobFocus.mobId);
                    GameScr.info1.addInfo("Đã thêm mob: " + mobFocus.mobId.ToString(), 0);
                }
            }
            else
            {
                bool flag4 = itemFocus != null;
                if (flag4)
                {
                    bool flag5 = PickMob.IdItemPicks.Contains(itemFocus.template.id);
                    if (flag5)
                    {
                        PickMob.IdItemPicks.Remove(itemFocus.template.id);
                        GameScr.info1.addInfo(string.Format("Đã xoá khỏi danh sách chỉ tự động nhặt item: {0}[{1}]", itemFocus.template.name, itemFocus.template.id), 0);
                    }
                    else
                    {
                        PickMob.IdItemPicks.Add(itemFocus.template.id);
                        GameScr.info1.addInfo(string.Format("Đã thêm vào danh sách chỉ tự động nhặt item: {0}[{1}]", itemFocus.template.name, itemFocus.template.id), 0);
                    }
                }
                else
                {
                    GameScr.info1.addInfo("Cần trỏ vào quái hay vật phẩm cần thêm vào danh sách", 0);
                }
            }
        }
        else
        {
            bool flag6 = text == "addt";
            if (flag6)
            {
                Mob mobFocus2 = global::Char.myCharz().mobFocus;
                ItemMap itemFocus2 = global::Char.myCharz().itemFocus;
                bool flag7 = mobFocus2 != null;
                if (flag7)
                {
                    bool flag8 = PickMob.TypeMobsTanSat.Contains(mobFocus2.templateId);
                    if (flag8)
                    {
                        PickMob.TypeMobsTanSat.Remove(mobFocus2.templateId);
                        GameScr.info1.addInfo(string.Format("Đã xoá loại mob: {0}[{1}]", mobFocus2.getTemplate().name, mobFocus2.templateId), 0);
                    }
                    else
                    {
                        PickMob.TypeMobsTanSat.Add(mobFocus2.templateId);
                        GameScr.info1.addInfo(string.Format("Đã thêm loại mob: {0}[{1}]", mobFocus2.getTemplate().name, mobFocus2.templateId), 0);
                    }
                }
                else
                {
                    bool flag9 = itemFocus2 != null;
                    if (flag9)
                    {
                        bool flag10 = PickMob.TypeItemPicks.Contains(itemFocus2.template.type);
                        if (flag10)
                        {
                            PickMob.TypeItemPicks.Remove(itemFocus2.template.type);
                            GameScr.info1.addInfo("Đã xoá khỏi danh sách chỉ tự động nhặt loại item:" + itemFocus2.template.type.ToString(), 0);
                        }
                        else
                        {
                            PickMob.TypeItemPicks.Add(itemFocus2.template.type);
                            GameScr.info1.addInfo("Đã thêm vào danh sách chỉ tự động nhặt loại item:" + itemFocus2.template.type.ToString(), 0);
                        }
                    }
                    else
                    {
                        GameScr.info1.addInfo("Cần trỏ vào quái hay vật phẩm cần thêm vào danh sách", 0);
                    }
                }
            }
            else
            {
                bool flag11 = text == "anhat";
                if (flag11)
                {
                    PickMob.IsAutoPickItems = !PickMob.IsAutoPickItems;
                    GameScr.info1.addInfo("Tự động nhặt vật phẩm: " + (PickMob.IsAutoPickItems ? "Bật" : "Tắt"), 0);
                }
                else
                {
                    bool flag12 = text == "itm";
                    if (flag12)
                    {
                        PickMob.IsItemMe = !PickMob.IsItemMe;
                        GameScr.info1.addInfo("Lọc không nhặt vật phẩm của người khác: " + (PickMob.IsItemMe ? "Bật" : "Tắt"), 0);
                    }
                    else
                    {
                        bool flag13 = text == "sln";
                        if (flag13)
                        {
                            PickMob.IsLimitTimesPickItem = !PickMob.IsLimitTimesPickItem;
                            StringBuilder stringBuilder = new StringBuilder();
                            stringBuilder.Append("Giới hạn số lần nhặt là ");
                            stringBuilder.Append(PickMob.TimesAutoPickItemMax);
                            stringBuilder.Append(PickMob.IsLimitTimesPickItem ? ": Bật" : ": Tắt");
                            GameScr.info1.addInfo(stringBuilder.ToString(), 0);
                        }
                        else
                        {
                            bool flag14 = PickMob.IsGetInfoChat<int>(text, "sln");
                            if (flag14)
                            {
                                PickMob.TimesAutoPickItemMax = PickMob.GetInfoChat<int>(text, "sln");
                                GameScr.info1.addInfo("Số lần nhặt giới hạn là: " + PickMob.TimesAutoPickItemMax.ToString(), 0);
                            }
                            else
                            {
                                bool flag15 = PickMob.IsGetInfoChat<short>(text, "addi");
                                if (flag15)
                                {
                                    short infoChat = PickMob.GetInfoChat<short>(text, "addi");
                                    bool flag16 = PickMob.IdItemPicks.Contains(infoChat);
                                    if (flag16)
                                    {
                                        PickMob.IdItemPicks.Remove(infoChat);
                                        GameScr.info1.addInfo(string.Format("Đã xoá khỏi danh sách chỉ tự động nhặt item: {0}[{1}]", ItemTemplates.get(infoChat).name, infoChat), 0);
                                    }
                                    else
                                    {
                                        PickMob.IdItemPicks.Add(infoChat);
                                        GameScr.info1.addInfo(string.Format("Đã thêm vào danh sách chỉ tự động nhặt item: {0}[{1}]", ItemTemplates.get(infoChat).name, infoChat), 0);
                                    }
                                }
                                else
                                {
                                    bool flag17 = text == "blocki";
                                    if (flag17)
                                    {
                                        ItemMap itemFocus3 = global::Char.myCharz().itemFocus;
                                        bool flag18 = itemFocus3 != null;
                                        if (flag18)
                                        {
                                            bool flag19 = PickMob.IdItemBlocks.Contains(itemFocus3.template.id);
                                            if (flag19)
                                            {
                                                PickMob.IdItemBlocks.Remove(itemFocus3.template.id);
                                                GameScr.info1.addInfo(string.Format("Đã xoá khỏi danh sách không tự động nhặt item: {0}[{1}]", itemFocus3.template.name, itemFocus3.template.id), 0);
                                            }
                                            else
                                            {
                                                PickMob.IdItemBlocks.Add(itemFocus3.template.id);
                                                GameScr.info1.addInfo(string.Format("Đã thêm vào danh sách không tự động nhặt item: {0}[{1}]", itemFocus3.template.name, itemFocus3.template.id), 0);
                                            }
                                        }
                                        else
                                        {
                                            GameScr.info1.addInfo("Cần trỏ vào vật phẩm cần chặn khi auto nhặt", 0);
                                        }
                                    }
                                    else
                                    {
                                        bool flag20 = PickMob.IsGetInfoChat<short>(text, "blocki");
                                        if (flag20)
                                        {
                                            short infoChat2 = PickMob.GetInfoChat<short>(text, "blocki");
                                            bool flag21 = PickMob.IdItemBlocks.Contains(infoChat2);
                                            if (flag21)
                                            {
                                                PickMob.IdItemBlocks.Remove(infoChat2);
                                                GameScr.info1.addInfo(string.Format("Đã thêm vào danh sách không tự động nhặt item: {0}[{1}]", ItemTemplates.get(infoChat2).name, infoChat2), 0);
                                            }
                                            else
                                            {
                                                PickMob.IdItemBlocks.Add(infoChat2);
                                                GameScr.info1.addInfo(string.Format("Đã xoá khỏi danh sách không tự động nhặt item: {0}[{1}]", ItemTemplates.get(infoChat2).name, infoChat2), 0);
                                            }
                                        }
                                        else
                                        {
                                            bool flag22 = PickMob.IsGetInfoChat<sbyte>(text, "addti");
                                            if (flag22)
                                            {
                                                sbyte infoChat3 = PickMob.GetInfoChat<sbyte>(text, "addti");
                                                bool flag23 = PickMob.TypeItemPicks.Contains(infoChat3);
                                                if (flag23)
                                                {
                                                    PickMob.TypeItemPicks.Remove(infoChat3);
                                                    GameScr.info1.addInfo("Đã xoá khỏi danh sách chỉ tự động nhặt loại item: " + infoChat3.ToString(), 0);
                                                }
                                                else
                                                {
                                                    PickMob.TypeItemPicks.Add(infoChat3);
                                                    GameScr.info1.addInfo("Đã thêm vào danh sách chỉ tự động nhặt loại item: " + infoChat3.ToString(), 0);
                                                }
                                            }
                                            else
                                            {
                                                bool flag24 = PickMob.IsGetInfoChat<sbyte>(text, "blockti");
                                                if (flag24)
                                                {
                                                    sbyte infoChat4 = PickMob.GetInfoChat<sbyte>(text, "blockti");
                                                    bool flag25 = PickMob.TypeItemBlock.Contains(infoChat4);
                                                    if (flag25)
                                                    {
                                                        PickMob.TypeItemBlock.Remove(infoChat4);
                                                        GameScr.info1.addInfo("Đã xoá khỏi danh sách không tự động nhặt loại item: " + infoChat4.ToString(), 0);
                                                    }
                                                    else
                                                    {
                                                        PickMob.TypeItemBlock.Add(infoChat4);
                                                        GameScr.info1.addInfo("Đã thêm vào danh sách không tự động nhặt loại item: " + infoChat4.ToString(), 0);
                                                    }
                                                }
                                                else
                                                {
                                                    bool flag26 = text == "clri";
                                                    if (flag26)
                                                    {
                                                        PickMob.IdItemPicks.Clear();
                                                        PickMob.TypeItemPicks.Clear();
                                                        PickMob.TypeItemBlock.Clear();
                                                        PickMob.IdItemBlocks.Clear();
                                                        PickMob.IdItemBlocks.AddRange(PickMob.IdItemBlockBase);
                                                        GameScr.info1.addInfo("Danh sách lọc item đã được đặt lại mặc định", 0);
                                                    }
                                                    else
                                                    {
                                                        bool flag27 = text == "cnn";
                                                        if (flag27)
                                                        {
                                                            PickMob.IdItemPicks.Clear();
                                                            PickMob.TypeItemPicks.Clear();
                                                            PickMob.TypeItemBlock.Clear();
                                                            PickMob.IdItemBlocks.Clear();
                                                            PickMob.IdItemBlocks.AddRange(PickMob.IdItemBlockBase);
                                                            PickMob.IdItemPicks.Add(77);
                                                            PickMob.IdItemPicks.Add(861);
                                                            GameScr.info1.addInfo("Đã cài đặt chỉ nhặt ngọc", 0);
                                                        }
                                                        else
                                                        {
                                                            bool flag28 = text == "ts";
                                                            if (flag28)
                                                            {
                                                                PickMob.IsTanSat = !PickMob.IsTanSat;
                                                                GameScr.info1.addInfo("Tự động đánh quái: " + (PickMob.IsTanSat ? "Bật" : "Tắt"), 0);
                                                            }
                                                            else
                                                            {
                                                                bool flag29 = text == "nsq";
                                                                if (flag29)
                                                                {
                                                                    PickMob.IsNeSieuQuai = !PickMob.IsNeSieuQuai;
                                                                    GameScr.info1.addInfo("Tàn sát né siêu quái: " + (PickMob.IsNeSieuQuai ? "Bật" : "Tắt"), 0);
                                                                }
                                                                else
                                                                {
                                                                    bool flag30 = PickMob.IsGetInfoChat<int>(text, "addm");
                                                                    if (flag30)
                                                                    {
                                                                        int infoChat5 = PickMob.GetInfoChat<int>(text, "addm");
                                                                        bool flag31 = PickMob.IdMobsTanSat.Contains(infoChat5);
                                                                        if (flag31)
                                                                        {
                                                                            PickMob.IdMobsTanSat.Remove(infoChat5);
                                                                            GameScr.info1.addInfo("Đã xoá mob: " + infoChat5.ToString(), 0);
                                                                        }
                                                                        else
                                                                        {
                                                                            PickMob.IdMobsTanSat.Add(infoChat5);
                                                                            GameScr.info1.addInfo("Đã thêm mob: " + infoChat5.ToString(), 0);
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        bool flag32 = PickMob.IsGetInfoChat<int>(text, "addtm");
                                                                        if (flag32)
                                                                        {
                                                                            int infoChat6 = PickMob.GetInfoChat<int>(text, "addtm");
                                                                            bool flag33 = PickMob.TypeMobsTanSat.Contains(infoChat6);
                                                                            if (flag33)
                                                                            {
                                                                                PickMob.TypeMobsTanSat.Remove(infoChat6);
                                                                                GameScr.info1.addInfo(string.Format("Đã xoá loại mob: {0}[{1}]", Mob.arrMobTemplate[infoChat6].name, infoChat6), 0);
                                                                            }
                                                                            else
                                                                            {
                                                                                PickMob.TypeMobsTanSat.Add(infoChat6);
                                                                                GameScr.info1.addInfo(string.Format("Đã thêm loại mob: {0}[{1}]", Mob.arrMobTemplate[infoChat6].name, infoChat6), 0);
                                                                            }
                                                                        }
                                                                        else
                                                                        {
                                                                            bool flag34 = text == "clrm";
                                                                            if (flag34)
                                                                            {
                                                                                PickMob.IdMobsTanSat.Clear();
                                                                                PickMob.TypeMobsTanSat.Clear();
                                                                                GameScr.info1.addInfo("Đã xoá danh sách đánh quái", 0);
                                                                            }
                                                                            else
                                                                            {
                                                                                bool flag35 = text == "skill";
                                                                                if (flag35)
                                                                                {
                                                                                    SkillTemplate template = global::Char.myCharz().myskill.template;
                                                                                    bool flag36 = PickMob.IdSkillsTanSat.Contains(template.id);
                                                                                    if (flag36)
                                                                                    {
                                                                                        PickMob.IdSkillsTanSat.Remove(template.id);
                                                                                        GameScr.info1.addInfo(string.Format("Đã xoá khỏi danh sách skill sử dụng tự động đánh quái skill: {0}[{1}]", template.name, template.id), 0);
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        PickMob.IdSkillsTanSat.Add(template.id);
                                                                                        GameScr.info1.addInfo(string.Format("Đã thêm vào danh sách skill sử dụng tự động đánh quái skill: {0}[{1}]", template.name, template.id), 0);
                                                                                    }
                                                                                }
                                                                                else
                                                                                {
                                                                                    bool flag37 = PickMob.IsGetInfoChat<int>(text, "skill");
                                                                                    if (flag37)
                                                                                    {
                                                                                        int num = PickMob.GetInfoChat<int>(text, "skill") - 1;
                                                                                        SkillTemplate skillTemplate = global::Char.myCharz().nClass.skillTemplates[num];
                                                                                        bool flag38 = PickMob.IdSkillsTanSat.Contains(skillTemplate.id);
                                                                                        if (flag38)
                                                                                        {
                                                                                            PickMob.IdSkillsTanSat.Remove(skillTemplate.id);
                                                                                            GameScr.info1.addInfo(string.Format("Đã xoá khỏi danh sách skill sử dụng tự động đánh quái skill: {0}[{1}]", skillTemplate.name, skillTemplate.id), 0);
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                            PickMob.IdSkillsTanSat.Add(skillTemplate.id);
                                                                                            GameScr.info1.addInfo(string.Format("Đã thêm vào danh sách skill sử dụng tự động đánh quái skill: {0}[{1}]", skillTemplate.name, skillTemplate.id), 0);
                                                                                        }
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        bool flag39 = PickMob.IsGetInfoChat<sbyte>(text, "skillid");
                                                                                        if (flag39)
                                                                                        {
                                                                                            sbyte infoChat7 = PickMob.GetInfoChat<sbyte>(text, "skillid");
                                                                                            bool flag40 = PickMob.IdSkillsTanSat.Contains(infoChat7);
                                                                                            if (flag40)
                                                                                            {
                                                                                                PickMob.IdSkillsTanSat.Remove(infoChat7);
                                                                                                GameScr.info1.addInfo("Đã xoá khỏi danh sách skill sử dụng tự động đánh quái skill: " + infoChat7.ToString(), 0);
                                                                                            }
                                                                                            else
                                                                                            {
                                                                                                PickMob.IdSkillsTanSat.Add(infoChat7);
                                                                                                GameScr.info1.addInfo("Đã thêm vào danh sách skill sử dụng tự động đánh quái skill: " + infoChat7.ToString(), 0);
                                                                                            }
                                                                                        }
                                                                                        else
                                                                                        {
                                                                                            bool flag41 = text == "clrs";
                                                                                            if (flag41)
                                                                                            {
                                                                                                PickMob.IdSkillsTanSat.Clear();
                                                                                                PickMob.IdSkillsTanSat.AddRange(PickMob.IdSkillsBase);
                                                                                                GameScr.info1.addInfo("Đã đặt danh sách skill sử dụng tự động đánh quái về mặc định", 0);
                                                                                            }
                                                                                            else
                                                                                            {
                                                                                                bool flag42 = text == "abf";
                                                                                                if (flag42)
                                                                                                {
                                                                                                    bool flag43 = PickMob.HpBuff == 0 && PickMob.MpBuff == 0;
                                                                                                    if (flag43)
                                                                                                    {
                                                                                                        GameScr.info1.addInfo("Tự động sử dụng đậu thần: Tắt", 0);
                                                                                                    }
                                                                                                    else
                                                                                                    {
                                                                                                        PickMob.HpBuff = 20;
                                                                                                        PickMob.MpBuff = 20;
                                                                                                        GameScr.info1.addInfo(string.Format("Tự động sử dụng đậu thần khi HP dưới {0}%, MP dưới {1}%", PickMob.HpBuff, PickMob.MpBuff), 0);
                                                                                                    }
                                                                                                }
                                                                                                else
                                                                                                {
                                                                                                    bool flag44 = PickMob.IsGetInfoChat<int>(text, "abf");
                                                                                                    if (flag44)
                                                                                                    {
                                                                                                        PickMob.HpBuff = PickMob.GetInfoChat<int>(text, "abf");
                                                                                                        PickMob.MpBuff = 0;
                                                                                                        GameScr.info1.addInfo(string.Format("Tự động sử dụng đậu thần khi HP dưới {0}%", PickMob.HpBuff), 0);
                                                                                                    }
                                                                                                    else
                                                                                                    {
                                                                                                        bool flag45 = PickMob.IsGetInfoChat<int>(text, "abf", 2);
                                                                                                        if (flag45)
                                                                                                        {
                                                                                                            int[] infoChat8 = PickMob.GetInfoChat<int>(text, "abf", 2);
                                                                                                            PickMob.HpBuff = infoChat8[0];
                                                                                                            PickMob.MpBuff = infoChat8[1];
                                                                                                            GameScr.info1.addInfo(string.Format("Tự động sử dụng đậu thần khi HP dưới {0}%, MP dưới {1}%", PickMob.HpBuff, PickMob.MpBuff), 0);
                                                                                                        }
                                                                                                        else
                                                                                                        {
                                                                                                            bool flag46 = !(text == "vdh");
                                                                                                            if (flag46)
                                                                                                            {
                                                                                                                return false;
                                                                                                            }
                                                                                                            PickMob.IsVuotDiaHinh = !PickMob.IsVuotDiaHinh;
                                                                                                            GameScr.info1.addInfo("Tự động đánh quái vượt địa hình: " + (PickMob.IsVuotDiaHinh ? "Bật" : "Tắt"), 0);
                                                                                                        }
                                                                                                    }
                                                                                                }
                                                                                            }
                                                                                        }
                                                                                    }
                                                                                }
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        return true;
    }

    // Token: 0x0600057A RID: 1402 RVA: 0x00065C8C File Offset: 0x00063E8C
    public static bool HotKeys()
    {
        int keyAsciiPress = GameCanvas.keyAsciiPress;
        bool flag = keyAsciiPress <= 98;
        if (flag)
        {
            bool flag2 = keyAsciiPress == 97;
            if (flag2)
            {
                PickMob.Chat("add");
                return true;
            }
            bool flag3 = keyAsciiPress == 98;
            if (flag3)
            {
                PickMob.Chat("abf");
                return true;
            }
        }
        else
        {
            bool flag4 = keyAsciiPress == 110;
            if (flag4)
            {
                PickMob.Chat("anhat");
                return true;
            }
            bool flag5 = keyAsciiPress == 116;
            if (flag5)
            {
                PickMob.Chat("ts");
                return true;
            }
        }
        return false;
    }

    // Token: 0x0600057B RID: 1403 RVA: 0x00005278 File Offset: 0x00003478
    public static void Update()
    {
        PickMobController.Update();
    }

    // Token: 0x0600057C RID: 1404 RVA: 0x00065D24 File Offset: 0x00063F24
    public static void MobStartDie(object obj)
    {
        Mob mob = (Mob)obj;
        bool flag = mob.status != 1 && mob.status != 0;
        if (flag)
        {
            mob.timeLastDie = mSystem.currentTimeMillis();
            mob.countDie++;
            bool flag2 = mob.countDie > 10;
            if (flag2)
            {
                mob.countDie = 0;
            }
        }
    }

    // Token: 0x0600057D RID: 1405 RVA: 0x00065D84 File Offset: 0x00063F84
    public static void UpdateCountDieMob(Mob mob)
    {
        bool flag = mob.levelBoss != 0;
        if (flag)
        {
            mob.countDie = 0;
        }
    }

    // Token: 0x0600057E RID: 1406 RVA: 0x00065DA8 File Offset: 0x00063FA8
    private static bool IsGetInfoChat<T>(string text, string s)
    {
        bool flag = !text.StartsWith(s);
        bool result;
        if (flag)
        {
            result = false;
        }
        else
        {
            try
            {
                Convert.ChangeType(text.Substring(s.Length), typeof(T));
            }
            catch
            {
                return false;
            }
            result = true;
        }
        return result;
    }

    // Token: 0x0600057F RID: 1407 RVA: 0x00065E08 File Offset: 0x00064008
    private static T GetInfoChat<T>(string text, string s)
    {
        return (T)((object)Convert.ChangeType(text.Substring(s.Length), typeof(T)));
    }

    // Token: 0x06000580 RID: 1408 RVA: 0x00065E3C File Offset: 0x0006403C
    private static bool IsGetInfoChat<T>(string text, string s, int n)
    {
        bool flag = !text.StartsWith(s);
        bool result;
        if (flag)
        {
            result = false;
        }
        else
        {
            try
            {
                string[] array = text.Substring(s.Length).Split(new char[]
                {
                    ' '
                });
                for (int i = 0; i < n; i++)
                {
                    Convert.ChangeType(array[i], typeof(T));
                }
            }
            catch
            {
                return false;
            }
            result = true;
        }
        return result;
    }

    // Token: 0x06000581 RID: 1409 RVA: 0x00065EC4 File Offset: 0x000640C4
    private static T[] GetInfoChat<T>(string text, string s, int n)
    {
        T[] array = new T[n];
        string[] array2 = text.Substring(s.Length).Split(new char[]
        {
            ' '
        });
        for (int i = 0; i < n; i++)
        {
            array[i] = (T)((object)Convert.ChangeType(array2[i], typeof(T)));
        }
        return array;
    }

    // Token: 0x04000BB4 RID: 2996
    private const int ID_ITEM_GEM = 77;

    // Token: 0x04000BB5 RID: 2997
    private const int ID_ITEM_GEM_LOCK = 861;

    // Token: 0x04000BB6 RID: 2998
    private const int DEFAULT_HP_BUFF = 20;

    // Token: 0x04000BB7 RID: 2999
    private const int DEFAULT_MP_BUFF = 20;

    // Token: 0x04000BB8 RID: 3000
    private static readonly sbyte[] IdSkillsBase = new sbyte[]
    {
        0,
        1,
        2,
        3,
        4,
        5,
        9,
        17
    };

    // Token: 0x04000BB9 RID: 3001
    private static readonly short[] IdItemBlockBase = new short[]
    {
        225,
        353,
        354,
        355,
        356,
        357,
        358,
        359,
        360,
        362
    };

    // Token: 0x04000BBA RID: 3002
    public static bool IsTanSat = false;

    // Token: 0x04000BBB RID: 3003
    public static bool IsNeSieuQuai = true;

    // Token: 0x04000BBC RID: 3004
    public static bool IsVuotDiaHinh = true;

    // Token: 0x04000BBD RID: 3005
    public static List<int> IdMobsTanSat = new List<int>();

    // Token: 0x04000BBE RID: 3006
    public static List<int> TypeMobsTanSat = new List<int>();

    // Token: 0x04000BBF RID: 3007
    public static List<sbyte> IdSkillsTanSat = new List<sbyte>(PickMob.IdSkillsBase);

    // Token: 0x04000BC0 RID: 3008
    public static bool IsAutoPickItems = true;

    // Token: 0x04000BC1 RID: 3009
    public static bool IsItemMe = true;

    // Token: 0x04000BC2 RID: 3010
    public static bool IsLimitTimesPickItem = true;

    // Token: 0x04000BC3 RID: 3011
    public static int TimesAutoPickItemMax = 15;

    // Token: 0x04000BC4 RID: 3012
    public static List<short> IdItemPicks = new List<short>();

    // Token: 0x04000BC5 RID: 3013
    public static List<short> IdItemBlocks = new List<short>(PickMob.IdItemBlockBase);

    // Token: 0x04000BC6 RID: 3014
    public static List<sbyte> TypeItemPicks = new List<sbyte>();

    // Token: 0x04000BC7 RID: 3015
    public static List<sbyte> TypeItemBlock = new List<sbyte>();

    // Token: 0x04000BC8 RID: 3016
    public static int HpBuff = 20;

    // Token: 0x04000BC9 RID: 3017
    public static int MpBuff = 5;
}
