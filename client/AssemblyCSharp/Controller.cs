using System;
using System.IO;
using AssemblyCSharp.Mod.Xmap;
using Assets.src.e;
using Assets.src.f;
using Assets.src.g;
using UnityEngine;

public class Controller : IMessageHandler
{
	protected static Controller me;

	protected static Controller me2;

	public Message messWait;

	public static bool isLoadingData;

	public static bool isConnectOK;

	public static bool isConnectionFail;

	public static bool isDisconnected;

	public static bool isMain;

	private float demCount;

	private int move;

	private int total;

	public static bool isStopReadMessage;

	public static MyHashTable frameHT_NEWBOSS = new MyHashTable();

	public const sbyte PHUBAN_TYPE_CHIENTRUONGNAMEK = 0;

	public const sbyte PHUBAN_START = 0;

	public const sbyte PHUBAN_UPDATE_POINT = 1;

	public const sbyte PHUBAN_END = 2;

	public const sbyte PHUBAN_LIFE = 4;

	public const sbyte PHUBAN_INFO = 5;

	public static Controller gI()
	{
		if (me == null)
		{
			me = new Controller();
		}
		return me;
	}

	public static Controller gI2()
	{
		if (me2 == null)
		{
			me2 = new Controller();
		}
		return me2;
	}

	public void onConnectOK(bool isMain1)
	{
		isMain = isMain1;
		mSystem.onConnectOK();
	}

	public void onConnectionFail(bool isMain1)
	{
		isMain = isMain1;
		mSystem.onConnectionFail();
	}

	public void onDisconnected(bool isMain1)
	{
		isMain = isMain1;
		mSystem.onDisconnected();
	}

	public void requestItemPlayer(Message msg)
	{
		try
		{
			int num = msg.reader().readUnsignedByte();
			Item item = GameScr.currentCharViewInfo.arrItemBody[num];
			item.saleCoinLock = msg.reader().readInt();
			item.sys = msg.reader().readByte();
			item.options = new MyVector();
			try
			{
				while (true)
				{
					item.options.addElement(new ItemOption(msg.reader().readUnsignedByte(), msg.reader().readUnsignedShort()));
				}
			}
			catch (Exception ex)
			{
				Cout.println("Loi tairequestItemPlayer 1" + ex.ToString());
			}
		}
		catch (Exception ex2)
		{
			Cout.println("Loi tairequestItemPlayer 2" + ex2.ToString());
		}
	}

	public void onMessage(Message msg)
	{
		GameCanvas.debugSession.removeAllElements();
		GameCanvas.debug("SA1", 2);
		try
		{
			Res.outz("<<<Read cmd= " + msg.command);
			Char @char = null;
			Mob mob = null;
			MyVector myVector = new MyVector();
			int num = 0;
			Controller2.readMessage(msg);
			switch (msg.command)
			{
			case 24:
				read_opt(msg);
				break;
			case 20:
				phuban_Info(msg);
				break;
			case 66:
				readGetImgByName(msg);
				break;
			case 65:
			{
				sbyte id3 = msg.reader().readSByte();
				string text6 = msg.reader().readUTF();
				short num150 = msg.reader().readShort();
				if (ItemTime.isExistMessage(id3))
				{
					if (num150 != 0)
					{
						ItemTime.getMessageById(id3).initTimeText(id3, text6, num150);
					}
					else
					{
						GameScr.textTime.removeElement(ItemTime.getMessageById(id3));
					}
				}
				else
				{
					ItemTime itemTime = new ItemTime();
					itemTime.initTimeText(id3, text6, num150);
					GameScr.textTime.addElement(itemTime);
				}
				break;
			}
			case 112:
			{
				sbyte b52 = msg.reader().readByte();
				Res.outz("spec type= " + b52);
				switch (b52)
				{
				case 0:
					Panel.spearcialImage = msg.reader().readShort();
					Panel.specialInfo = msg.reader().readUTF();
					break;
				case 1:
				{
					sbyte b53 = msg.reader().readByte();
					Char.myCharz().infoSpeacialSkill = new string[b53][];
					Char.myCharz().imgSpeacialSkill = new short[b53][];
					GameCanvas.panel.speacialTabName = new string[b53][];
					for (int num142 = 0; num142 < b53; num142++)
					{
						GameCanvas.panel.speacialTabName[num142] = new string[2];
						string[] array15 = Res.split(msg.reader().readUTF(), "\n", 0);
						if (array15.Length == 2)
						{
							GameCanvas.panel.speacialTabName[num142] = array15;
						}
						if (array15.Length == 1)
						{
							GameCanvas.panel.speacialTabName[num142][0] = array15[0];
							GameCanvas.panel.speacialTabName[num142][1] = string.Empty;
						}
						int num143 = msg.reader().readByte();
						Char.myCharz().infoSpeacialSkill[num142] = new string[num143];
						Char.myCharz().imgSpeacialSkill[num142] = new short[num143];
						for (int num144 = 0; num144 < num143; num144++)
						{
							Char.myCharz().imgSpeacialSkill[num142][num144] = msg.reader().readShort();
							Char.myCharz().infoSpeacialSkill[num142][num144] = msg.reader().readUTF();
						}
					}
					GameCanvas.panel.tabName[25] = GameCanvas.panel.speacialTabName;
					GameCanvas.panel.setTypeSpeacialSkill();
					GameCanvas.panel.show();
					break;
				}
				}
				break;
			}
			case -98:
			{
				sbyte b35 = msg.reader().readByte();
				GameCanvas.menu.showMenu = false;
				if (b35 == 0)
				{
					GameCanvas.startYesNoDlg(msg.reader().readUTF(), new Command(mResources.YES, GameCanvas.instance, 888397, msg.reader().readUTF()), new Command(mResources.NO, GameCanvas.instance, 888396, null));
				}
				break;
			}
			case -97:
				Char.myCharz().cNangdong = msg.reader().readInt();
				break;
			case -96:
			{
				sbyte typeTop = msg.reader().readByte();
				GameCanvas.panel.vTop.removeAllElements();
				string topName = msg.reader().readUTF();
				sbyte b62 = msg.reader().readByte();
				for (int num166 = 0; num166 < b62; num166++)
				{
					int rank = msg.reader().readInt();
					int pId = msg.reader().readInt();
					short headID = msg.reader().readShort();
					short headICON = msg.reader().readShort();
					short body = msg.reader().readShort();
					short leg = msg.reader().readShort();
					string name = msg.reader().readUTF();
					string info3 = msg.reader().readUTF();
					TopInfo topInfo = new TopInfo();
					topInfo.rank = rank;
					topInfo.headID = headID;
					topInfo.headICON = headICON;
					topInfo.body = body;
					topInfo.leg = leg;
					topInfo.name = name;
					topInfo.info = info3;
					topInfo.info2 = msg.reader().readUTF();
					topInfo.pId = pId;
					GameCanvas.panel.vTop.addElement(topInfo);
				}
				GameCanvas.panel.topName = topName;
				GameCanvas.panel.setTypeTop(typeTop);
				GameCanvas.panel.show();
				break;
			}
			case -94:
				while (msg.reader().available() > 0)
				{
					short num10 = msg.reader().readShort();
					int num11 = msg.reader().readInt();
					for (int i = 0; i < Char.myCharz().vSkill.size(); i++)
					{
						Skill skill = (Skill)Char.myCharz().vSkill.elementAt(i);
						if (skill != null && skill.skillId == num10)
						{
							if (num11 < skill.coolDown)
							{
								skill.lastTimeUseThisSkill = mSystem.currentTimeMillis() - (skill.coolDown - num11);
							}
							Res.outz("1 chieu id= " + skill.template.id + " cooldown= " + num11 + " curr cool down= " + skill.coolDown);
						}
					}
				}
				break;
			case -95:
			{
				sbyte b32 = msg.reader().readByte();
				Res.outz("type= " + b32);
				if (b32 == 0)
				{
					int num83 = msg.reader().readInt();
					short templateId = msg.reader().readShort();
					int num84 = msg.readInt3Byte();
					SoundMn.gI().explode_1();
					if (num83 == Char.myCharz().charID)
					{
						Char.myCharz().mobMe = new Mob(num83, isDisable: false, isDontMove: false, isFire: false, isIce: false, isWind: false, templateId, 1, num84, 0, num84, (short)(Char.myCharz().cx + ((Char.myCharz().cdir != 1) ? (-40) : 40)), (short)Char.myCharz().cy, 4, 0);
						Char.myCharz().mobMe.isMobMe = true;
						EffecMn.addEff(new Effect(18, Char.myCharz().mobMe.x, Char.myCharz().mobMe.y, 2, 10, -1));
						Char.myCharz().tMobMeBorn = 30;
						GameScr.vMob.addElement(Char.myCharz().mobMe);
					}
					else
					{
						@char = GameScr.findCharInMap(num83);
						if (@char != null)
						{
							Mob mob5 = new Mob(num83, isDisable: false, isDontMove: false, isFire: false, isIce: false, isWind: false, templateId, 1, num84, 0, num84, (short)@char.cx, (short)@char.cy, 4, 0);
							mob5.isMobMe = true;
							@char.mobMe = mob5;
							GameScr.vMob.addElement(@char.mobMe);
						}
						else
						{
							Mob mob6 = GameScr.findMobInMap(num83);
							if (mob6 == null)
							{
								mob6 = new Mob(num83, isDisable: false, isDontMove: false, isFire: false, isIce: false, isWind: false, templateId, 1, num84, 0, num84, -100, -100, 4, 0);
								mob6.isMobMe = true;
								GameScr.vMob.addElement(mob6);
							}
						}
					}
				}
				if (b32 == 1)
				{
					int num85 = msg.reader().readInt();
					int mobId = msg.reader().readByte();
					Res.outz("mod attack id= " + num85);
					if (num85 == Char.myCharz().charID)
					{
						if (GameScr.findMobInMap(mobId) != null)
						{
							Char.myCharz().mobMe.attackOtherMob(GameScr.findMobInMap(mobId));
						}
					}
					else
					{
						@char = GameScr.findCharInMap(num85);
						if (@char != null && GameScr.findMobInMap(mobId) != null)
						{
							@char.mobMe.attackOtherMob(GameScr.findMobInMap(mobId));
						}
					}
				}
				if (b32 == 2)
				{
					int num86 = msg.reader().readInt();
					int num87 = msg.reader().readInt();
					int num88 = msg.readInt3Byte();
					int cHPNew = msg.readInt3Byte();
					if (num86 == Char.myCharz().charID)
					{
						Res.outz("mob dame= " + num88);
						@char = GameScr.findCharInMap(num87);
						if (@char != null)
						{
							@char.cHPNew = cHPNew;
							if (Char.myCharz().mobMe.isBusyAttackSomeOne)
							{
								@char.doInjure(num88, 0, isCrit: false, isMob: true);
							}
							else
							{
								Char.myCharz().mobMe.dame = num88;
								Char.myCharz().mobMe.setAttack(@char);
							}
						}
					}
					else
					{
						mob = GameScr.findMobInMap(num86);
						if (mob != null)
						{
							if (num87 == Char.myCharz().charID)
							{
								Char.myCharz().cHPNew = cHPNew;
								if (mob.isBusyAttackSomeOne)
								{
									Char.myCharz().doInjure(num88, 0, isCrit: false, isMob: true);
								}
								else
								{
									mob.dame = num88;
									mob.setAttack(Char.myCharz());
								}
							}
							else
							{
								@char = GameScr.findCharInMap(num87);
								if (@char != null)
								{
									@char.cHPNew = cHPNew;
									if (mob.isBusyAttackSomeOne)
									{
										@char.doInjure(num88, 0, isCrit: false, isMob: true);
									}
									else
									{
										mob.dame = num88;
										mob.setAttack(@char);
									}
								}
							}
						}
					}
				}
				if (b32 == 3)
				{
					int num89 = msg.reader().readInt();
					int mobId2 = msg.reader().readInt();
					int hp = msg.readInt3Byte();
					int num90 = msg.readInt3Byte();
					@char = null;
					@char = ((Char.myCharz().charID != num89) ? GameScr.findCharInMap(num89) : Char.myCharz());
					if (@char != null)
					{
						mob = GameScr.findMobInMap(mobId2);
						if (@char.mobMe != null)
						{
							@char.mobMe.attackOtherMob(mob);
						}
						if (mob != null)
						{
							mob.hp = hp;
							mob.updateHp_bar();
							if (num90 == 0)
							{
								mob.x = mob.xFirst;
								mob.y = mob.yFirst;
								GameScr.startFlyText(mResources.miss, mob.x, mob.y - mob.h, 0, -2, mFont.MISS);
							}
							else
							{
								GameScr.startFlyText("-" + num90, mob.x, mob.y - mob.h, 0, -2, mFont.ORANGE);
							}
						}
					}
				}
				if (b32 == 4)
				{
				}
				if (b32 == 5)
				{
					int num91 = msg.reader().readInt();
					sbyte b33 = msg.reader().readByte();
					int mobId3 = msg.reader().readInt();
					int num92 = msg.readInt3Byte();
					int hp2 = msg.readInt3Byte();
					@char = null;
					@char = ((num91 != Char.myCharz().charID) ? GameScr.findCharInMap(num91) : Char.myCharz());
					if (@char == null)
					{
						return;
					}
					if ((TileMap.tileTypeAtPixel(@char.cx, @char.cy) & 2) == 2)
					{
						@char.setSkillPaint(GameScr.sks[b33], 0);
					}
					else
					{
						@char.setSkillPaint(GameScr.sks[b33], 1);
					}
					Mob mob7 = GameScr.findMobInMap(mobId3);
					if (@char.cx <= mob7.x)
					{
						@char.cdir = 1;
					}
					else
					{
						@char.cdir = -1;
					}
					@char.mobFocus = mob7;
					mob7.hp = hp2;
					mob7.updateHp_bar();
					GameCanvas.debug("SA83v2", 2);
					if (num92 == 0)
					{
						mob7.x = mob7.xFirst;
						mob7.y = mob7.yFirst;
						GameScr.startFlyText(mResources.miss, mob7.x, mob7.y - mob7.h, 0, -2, mFont.MISS);
					}
					else
					{
						GameScr.startFlyText("-" + num92, mob7.x, mob7.y - mob7.h, 0, -2, mFont.ORANGE);
					}
				}
				if (b32 == 6)
				{
					int num93 = msg.reader().readInt();
					if (num93 == Char.myCharz().charID)
					{
						Char.myCharz().mobMe.startDie();
					}
					else
					{
						@char = GameScr.findCharInMap(num93);
						@char?.mobMe.startDie();
					}
				}
				if (b32 != 7)
				{
					break;
				}
				int num94 = msg.reader().readInt();
				if (num94 == Char.myCharz().charID)
				{
					Char.myCharz().mobMe = null;
					for (int num95 = 0; num95 < GameScr.vMob.size(); num95++)
					{
						if (((Mob)GameScr.vMob.elementAt(num95)).mobId == num94)
						{
							GameScr.vMob.removeElementAt(num95);
						}
					}
					break;
				}
				@char = GameScr.findCharInMap(num94);
				for (int num96 = 0; num96 < GameScr.vMob.size(); num96++)
				{
					if (((Mob)GameScr.vMob.elementAt(num96)).mobId == num94)
					{
						GameScr.vMob.removeElementAt(num96);
					}
				}
				if (@char != null)
				{
					@char.mobMe = null;
				}
				break;
			}
			case -92:
				Main.typeClient = msg.reader().readByte();
				Rms.clearAll();
				Rms.saveRMSInt("clienttype", Main.typeClient);
				Rms.saveRMSInt("lastZoomlevel", mGraphics.zoomLevel);
				GameCanvas.startOK(mResources.plsRestartGame, 8885, null);
				break;
			case -91:
			{
				sbyte b43 = msg.reader().readByte();
				GameCanvas.panel.mapNames = new string[b43];
				GameCanvas.panel.planetNames = new string[b43];
				for (int num117 = 0; num117 < b43; num117++)
				{
					GameCanvas.panel.mapNames[num117] = msg.reader().readUTF();
					GameCanvas.panel.planetNames[num117] = msg.reader().readUTF();
				}
				Pk9rXmap.ShowPanelMapTrans();
				break;
			}
			case -90:
			{
				sbyte b38 = msg.reader().readByte();
				int num106 = msg.reader().readInt();
				Res.outz("===> UPDATE_BODY:    type = " + b38);
				@char = ((Char.myCharz().charID != num106) ? GameScr.findCharInMap(num106) : Char.myCharz());
				if (b38 != -1)
				{
					short num107 = msg.reader().readShort();
					short num108 = msg.reader().readShort();
					short num109 = msg.reader().readShort();
					sbyte isMonkey = msg.reader().readByte();
					Res.err("====> Cmd: -90 UPDATE_BODY   \n  isMonkey= " + isMonkey + " head=  " + num107 + " body= " + num108 + " legU= " + num109);
					if (@char != null)
					{
						if (@char.charID == num106)
						{
							@char.isMask = true;
							@char.isMonkey = isMonkey;
							if (@char.isMonkey != 0)
							{
								@char.isWaitMonkey = false;
								@char.isLockMove = false;
							}
						}
						else if (@char != null)
						{
							@char.isMask = true;
							@char.isMonkey = isMonkey;
						}
						if (num107 != -1)
						{
							@char.head = num107;
						}
						if (num108 != -1)
						{
							@char.body = num108;
						}
						if (num109 != -1)
						{
							@char.leg = num109;
						}
					}
				}
				if (b38 == -1 && @char != null)
				{
					@char.isMask = false;
					@char.isMonkey = 0;
				}
				if (@char == null)
				{
				}
				break;
			}
			case -88:
				GameCanvas.endDlg();
				GameCanvas.serverScreen.switchToMe();
				break;
			case -87:
			{
				Res.outz("GET UPDATE_DATA " + msg.reader().available() + " bytes");
				msg.reader().mark(100000);
				createData(msg.reader(), isSaveRMS: true);
				msg.reader().reset();
				sbyte[] data = new sbyte[msg.reader().available()];
				msg.reader().readFully(ref data);
				sbyte[] data2 = new sbyte[1] { GameScr.vcData };
				Rms.saveRMS("NRdataVersion", data2);
				LoginScr.isUpdateData = false;
				if (GameScr.vsData == GameScr.vcData && GameScr.vsMap == GameScr.vcMap && GameScr.vsSkill == GameScr.vcSkill && GameScr.vsItem == GameScr.vcItem)
				{
					Res.outz(GameScr.vsData + "," + GameScr.vsMap + "," + GameScr.vsSkill + "," + GameScr.vsItem);
					GameScr.gI().readDart();
					GameScr.gI().readEfect();
					GameScr.gI().readArrow();
					GameScr.gI().readSkill();
					Service.gI().clientOk();
					return;
				}
				break;
			}
			case -86:
			{
				sbyte b56 = msg.reader().readByte();
				Res.outz("server gui ve giao dich action = " + b56);
				if (b56 == 0)
				{
					int playerID = msg.reader().readInt();
					GameScr.gI().giaodich(playerID);
				}
				if (b56 == 1)
				{
					int num151 = msg.reader().readInt();
					Char char13 = GameScr.findCharInMap(num151);
					if (char13 == null)
					{
						return;
					}
					GameCanvas.panel.setTypeGiaoDich(char13);
					GameCanvas.panel.show();
					Service.gI().getPlayerMenu(num151);
				}
				if (b56 == 2)
				{
					sbyte b57 = msg.reader().readByte();
					for (int num152 = 0; num152 < GameCanvas.panel.vMyGD.size(); num152++)
					{
						Item item2 = (Item)GameCanvas.panel.vMyGD.elementAt(num152);
						if (item2.indexUI == b57)
						{
							GameCanvas.panel.vMyGD.removeElement(item2);
							break;
						}
					}
				}
				if (b56 == 5)
				{
				}
				if (b56 == 6)
				{
					GameCanvas.panel.isFriendLock = true;
					if (GameCanvas.panel2 != null)
					{
						GameCanvas.panel2.isFriendLock = true;
					}
					GameCanvas.panel.vFriendGD.removeAllElements();
					if (GameCanvas.panel2 != null)
					{
						GameCanvas.panel2.vFriendGD.removeAllElements();
					}
					int friendMoneyGD = msg.reader().readInt();
					sbyte b58 = msg.reader().readByte();
					Res.outz("item size = " + b58);
					for (int num153 = 0; num153 < b58; num153++)
					{
						Item item3 = new Item();
						item3.template = ItemTemplates.get(msg.reader().readShort());
						item3.quantity = msg.reader().readInt();
						int num154 = msg.reader().readUnsignedByte();
						if (num154 != 0)
						{
							item3.itemOption = new ItemOption[num154];
							for (int num155 = 0; num155 < item3.itemOption.Length; num155++)
							{
								int num156 = msg.reader().readUnsignedByte();
								int param5 = msg.reader().readUnsignedShort();
								if (num156 != -1)
								{
									item3.itemOption[num155] = new ItemOption(num156, param5);
									item3.compare = GameCanvas.panel.getCompare(item3);
								}
							}
						}
						if (GameCanvas.panel2 != null)
						{
							GameCanvas.panel2.vFriendGD.addElement(item3);
						}
						else
						{
							GameCanvas.panel.vFriendGD.addElement(item3);
						}
					}
					if (GameCanvas.panel2 != null)
					{
						GameCanvas.panel2.setTabGiaoDich(isMe: false);
						GameCanvas.panel2.friendMoneyGD = friendMoneyGD;
					}
					else
					{
						GameCanvas.panel.friendMoneyGD = friendMoneyGD;
						if (GameCanvas.panel.currentTabIndex == 2)
						{
							GameCanvas.panel.setTabGiaoDich(isMe: false);
						}
					}
				}
				if (b56 == 7)
				{
					InfoDlg.hide();
					if (GameCanvas.panel.isShow)
					{
						GameCanvas.panel.hide();
					}
				}
				break;
			}
			case -85:
			{
				Res.outz("CAP CHAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");
				sbyte b44 = msg.reader().readByte();
				if (b44 == 0)
				{
					int num122 = msg.reader().readUnsignedShort();
					Res.outz("lent =" + num122);
					sbyte[] data4 = new sbyte[num122];
					msg.reader().read(ref data4, 0, num122);
					GameScr.imgCapcha = Image.createImage(data4, 0, num122);
					GameScr.gI().keyInput = "-----";
					GameScr.gI().strCapcha = msg.reader().readUTF();
					GameScr.gI().keyCapcha = new int[GameScr.gI().strCapcha.Length];
					GameScr.gI().mobCapcha = new Mob();
					GameScr.gI().right = null;
				}
				if (b44 == 1)
				{
					MobCapcha.isAttack = true;
				}
				if (b44 == 2)
				{
					MobCapcha.explode = true;
					GameScr.gI().right = GameScr.gI().cmdFocus;
				}
				break;
			}
			case -112:
			{
				sbyte b42 = msg.reader().readByte();
				if (b42 == 0)
				{
					sbyte mobIndex = msg.reader().readByte();
					GameScr.findMobInMap(mobIndex).clearBody();
				}
				if (b42 == 1)
				{
					sbyte mobIndex2 = msg.reader().readByte();
					GameScr.findMobInMap(mobIndex2).setBody(msg.reader().readShort());
				}
				break;
			}
			case -84:
			{
				int index = msg.reader().readUnsignedByte();
				Mob mob3 = null;
				try
				{
					mob3 = (Mob)GameScr.vMob.elementAt(index);
				}
				catch (Exception)
				{
				}
				if (mob3 != null)
				{
					mob3.maxHp = msg.reader().readInt();
				}
				break;
			}
			case -83:
			{
				sbyte b15 = msg.reader().readByte();
				if (b15 == 0)
				{
					int num29 = msg.reader().readShort();
					int bgRID = msg.reader().readShort();
					int num30 = msg.reader().readUnsignedByte();
					int num31 = msg.reader().readInt();
					string text = msg.reader().readUTF();
					int xR = msg.reader().readShort();
					int yR = msg.reader().readShort();
					sbyte b16 = msg.reader().readByte();
					if (b16 == 1)
					{
						GameScr.gI().isRongNamek = true;
					}
					else
					{
						GameScr.gI().isRongNamek = false;
					}
					GameScr.gI().xR = xR;
					GameScr.gI().yR = yR;
					Res.outz("xR= " + xR + " yR= " + yR + " +++++++++++++++++++++++++++++++++++++++");
					if (Char.myCharz().charID == num31)
					{
						GameCanvas.panel.hideNow();
						GameScr.gI().activeRongThanEff(isMe: true);
					}
					else if (TileMap.mapID == num29 && TileMap.zoneID == num30)
					{
						GameScr.gI().activeRongThanEff(isMe: false);
					}
					else if (mGraphics.zoomLevel > 1)
					{
						GameScr.gI().doiMauTroi();
					}
					GameScr.gI().mapRID = num29;
					GameScr.gI().bgRID = bgRID;
					GameScr.gI().zoneRID = num30;
				}
				if (b15 == 1)
				{
					Res.outz("map RID = " + GameScr.gI().mapRID + " zone RID= " + GameScr.gI().zoneRID);
					Res.outz("map ID = " + TileMap.mapID + " zone ID= " + TileMap.zoneID);
					if (TileMap.mapID == GameScr.gI().mapRID && TileMap.zoneID == GameScr.gI().zoneRID)
					{
						GameScr.gI().hideRongThanEff();
					}
					else
					{
						GameScr.gI().isRongThanXuatHien = false;
						if (GameScr.gI().isRongNamek)
						{
							GameScr.gI().isRongNamek = false;
						}
					}
				}
				if (b15 != 2)
				{
				}
				break;
			}
			case -82:
			{
				sbyte b39 = msg.reader().readByte();
				TileMap.tileIndex = new int[b39][][];
				TileMap.tileType = new int[b39][];
				for (int num110 = 0; num110 < b39; num110++)
				{
					sbyte b40 = msg.reader().readByte();
					TileMap.tileType[num110] = new int[b40];
					TileMap.tileIndex[num110] = new int[b40][];
					for (int num111 = 0; num111 < b40; num111++)
					{
						TileMap.tileType[num110][num111] = msg.reader().readInt();
						sbyte b41 = msg.reader().readByte();
						TileMap.tileIndex[num110][num111] = new int[b41];
						for (int num112 = 0; num112 < b41; num112++)
						{
							TileMap.tileIndex[num110][num111][num112] = msg.reader().readByte();
						}
					}
				}
				break;
			}
			case -81:
			{
				sbyte b12 = msg.reader().readByte();
				if (b12 == 0)
				{
					string src = msg.reader().readUTF();
					string src2 = msg.reader().readUTF();
					GameCanvas.panel.setTypeCombine();
					GameCanvas.panel.combineInfo = mFont.tahoma_7b_blue.splitFontArray(src, Panel.WIDTH_PANEL);
					GameCanvas.panel.combineTopInfo = mFont.tahoma_7.splitFontArray(src2, Panel.WIDTH_PANEL);
					GameCanvas.panel.show();
				}
				if (b12 == 1)
				{
					GameCanvas.panel.vItemCombine.removeAllElements();
					sbyte b13 = msg.reader().readByte();
					for (int num25 = 0; num25 < b13; num25++)
					{
						sbyte b14 = msg.reader().readByte();
						for (int num26 = 0; num26 < Char.myCharz().arrItemBag.Length; num26++)
						{
							Item item = Char.myCharz().arrItemBag[num26];
							if (item != null && item.indexUI == b14)
							{
								item.isSelect = true;
								GameCanvas.panel.vItemCombine.addElement(item);
							}
						}
					}
					if (GameCanvas.panel.isShow)
					{
						GameCanvas.panel.setTabCombine();
					}
				}
				if (b12 == 2)
				{
					GameCanvas.panel.combineSuccess = 0;
					GameCanvas.panel.setCombineEff(0);
				}
				if (b12 == 3)
				{
					GameCanvas.panel.combineSuccess = 1;
					GameCanvas.panel.setCombineEff(0);
				}
				if (b12 == 4)
				{
					short iconID = msg.reader().readShort();
					GameCanvas.panel.iconID3 = iconID;
					GameCanvas.panel.combineSuccess = 0;
					GameCanvas.panel.setCombineEff(1);
				}
				if (b12 == 5)
				{
					short iconID2 = msg.reader().readShort();
					GameCanvas.panel.iconID3 = iconID2;
					GameCanvas.panel.combineSuccess = 0;
					GameCanvas.panel.setCombineEff(2);
				}
				if (b12 == 6)
				{
					short iconID3 = msg.reader().readShort();
					short iconID4 = msg.reader().readShort();
					GameCanvas.panel.combineSuccess = 0;
					GameCanvas.panel.setCombineEff(3);
					GameCanvas.panel.iconID1 = iconID3;
					GameCanvas.panel.iconID3 = iconID4;
				}
				if (b12 == 7)
				{
					short iconID5 = msg.reader().readShort();
					GameCanvas.panel.iconID3 = iconID5;
					GameCanvas.panel.combineSuccess = 0;
					GameCanvas.panel.setCombineEff(4);
				}
				if (b12 == 8)
				{
					GameCanvas.panel.iconID3 = -1;
					GameCanvas.panel.combineSuccess = 1;
					GameCanvas.panel.setCombineEff(4);
				}
				short num27 = 21;
				try
				{
					num27 = msg.reader().readShort();
				}
				catch (Exception)
				{
				}
				for (int num28 = 0; num28 < GameScr.vNpc.size(); num28++)
				{
					Npc npc = (Npc)GameScr.vNpc.elementAt(num28);
					if (npc.template.npcTemplateId == num27)
					{
						GameCanvas.panel.xS = npc.cx - GameScr.cmx;
						GameCanvas.panel.yS = npc.cy - GameScr.cmy;
						GameCanvas.panel.idNPC = num27;
						break;
					}
				}
				break;
			}
			case -80:
			{
				sbyte b45 = msg.reader().readByte();
				InfoDlg.hide();
				if (b45 == 0)
				{
					GameCanvas.panel.vFriend.removeAllElements();
					int num124 = msg.reader().readUnsignedByte();
					for (int num125 = 0; num125 < num124; num125++)
					{
						Char char12 = new Char();
						char12.charID = msg.reader().readInt();
						char12.head = msg.reader().readShort();
						char12.headICON = msg.reader().readShort();
						char12.body = msg.reader().readShort();
						char12.leg = msg.reader().readShort();
						char12.bag = msg.reader().readUnsignedByte();
						char12.cName = msg.reader().readUTF();
						bool isOnline2 = msg.reader().readBoolean();
						InfoItem infoItem2 = new InfoItem(mResources.power + ": " + msg.reader().readUTF());
						infoItem2.charInfo = char12;
						infoItem2.isOnline = isOnline2;
						GameCanvas.panel.vFriend.addElement(infoItem2);
					}
					GameCanvas.panel.setTypeFriend();
					GameCanvas.panel.show();
				}
				if (b45 == 3)
				{
					MyVector vFriend = GameCanvas.panel.vFriend;
					int num126 = msg.reader().readInt();
					Res.outz("online offline id=" + num126);
					for (int num127 = 0; num127 < vFriend.size(); num127++)
					{
						InfoItem infoItem3 = (InfoItem)vFriend.elementAt(num127);
						if (infoItem3.charInfo != null && infoItem3.charInfo.charID == num126)
						{
							Res.outz("online= " + infoItem3.isOnline);
							infoItem3.isOnline = msg.reader().readBoolean();
							break;
						}
					}
				}
				if (b45 != 2)
				{
					break;
				}
				MyVector vFriend2 = GameCanvas.panel.vFriend;
				int num128 = msg.reader().readInt();
				for (int num129 = 0; num129 < vFriend2.size(); num129++)
				{
					InfoItem infoItem4 = (InfoItem)vFriend2.elementAt(num129);
					if (infoItem4.charInfo != null && infoItem4.charInfo.charID == num128)
					{
						vFriend2.removeElement(infoItem4);
						break;
					}
				}
				if (GameCanvas.panel.isShow)
				{
					GameCanvas.panel.setTabFriend();
				}
				break;
			}
			case -99:
				InfoDlg.hide();
				if (msg.reader().readByte() == 0)
				{
					GameCanvas.panel.vEnemy.removeAllElements();
					int num101 = msg.reader().readUnsignedByte();
					for (int num102 = 0; num102 < num101; num102++)
					{
						Char char5 = new Char();
						char5.charID = msg.reader().readInt();
						char5.head = msg.reader().readShort();
						char5.headICON = msg.reader().readShort();
						char5.body = msg.reader().readShort();
						char5.leg = msg.reader().readShort();
						char5.bag = msg.reader().readShort();
						char5.cName = msg.reader().readUTF();
						InfoItem infoItem = new InfoItem(msg.reader().readUTF());
						bool isOnline = msg.reader().readBoolean();
						infoItem.charInfo = char5;
						infoItem.isOnline = isOnline;
						Res.outz("isonline = " + isOnline);
						GameCanvas.panel.vEnemy.addElement(infoItem);
					}
					GameCanvas.panel.setTypeEnemy();
					GameCanvas.panel.show();
				}
				break;
			case -79:
			{
				InfoDlg.hide();
				int num40 = msg.reader().readInt();
				Char charMenu = GameCanvas.panel.charMenu;
				if (charMenu == null)
				{
					return;
				}
				charMenu.cPower = msg.reader().readLong();
				charMenu.currStrLevel = msg.reader().readUTF();
				break;
			}
			case -93:
			{
				short num77 = msg.reader().readShort();
				BgItem.newSmallVersion = new sbyte[num77];
				for (int num78 = 0; num78 < num77; num78++)
				{
					BgItem.newSmallVersion[num78] = msg.reader().readByte();
				}
				break;
			}
			case -77:
			{
				short num13 = msg.reader().readShort();
				SmallImage.newSmallVersion = new sbyte[num13];
				SmallImage.maxSmall = num13;
				SmallImage.imgNew = new Small[num13];
				for (int j = 0; j < num13; j++)
				{
					SmallImage.newSmallVersion[j] = msg.reader().readByte();
				}
				break;
			}
			case -76:
				switch (msg.reader().readByte())
				{
				case 0:
				{
					sbyte b8 = msg.reader().readByte();
					if (b8 <= 0)
					{
						return;
					}
					Char.myCharz().arrArchive = new Archivement[b8];
					for (int m = 0; m < b8; m++)
					{
						Char.myCharz().arrArchive[m] = new Archivement();
						Char.myCharz().arrArchive[m].info1 = m + 1 + ". " + msg.reader().readUTF();
						Char.myCharz().arrArchive[m].info2 = msg.reader().readUTF();
						Char.myCharz().arrArchive[m].money = msg.reader().readShort();
						Char.myCharz().arrArchive[m].isFinish = msg.reader().readBoolean();
						Char.myCharz().arrArchive[m].isRecieve = msg.reader().readBoolean();
					}
					GameCanvas.panel.setTypeArchivement();
					GameCanvas.panel.show();
					break;
				}
				case 1:
				{
					int num17 = msg.reader().readUnsignedByte();
					if (Char.myCharz().arrArchive[num17] != null)
					{
						Char.myCharz().arrArchive[num17].isRecieve = true;
					}
					break;
				}
				}
				break;
			case -74:
			{
				if (ServerListScreen.stopDownload)
				{
					return;
				}
				if (!GameCanvas.isGetResourceFromServer())
				{
					Service.gI().getResource(3, null);
					SmallImage.loadBigRMS();
					SplashScr.imgLogo = null;
					if (Rms.loadRMSString("acc") != null || Rms.loadRMSString("userAo" + ServerListScreen.ipSelect) != null)
					{
						LoginScr.isContinueToLogin = true;
					}
					GameCanvas.loginScr = new LoginScr();
					GameCanvas.loginScr.switchToMe();
					return;
				}
				bool flag6 = true;
				sbyte b31 = msg.reader().readByte();
				Res.outz("action = " + b31);
				if (b31 == 0)
				{
					int num79 = msg.reader().readInt();
					string text3 = Rms.loadRMSString("ResVersion");
					int num80 = ((text3 == null || !(text3 != string.Empty)) ? (-1) : int.Parse(text3));
					if (num80 == -1 || num80 != num79)
					{
						ServerListScreen.loadScreen = false;
						GameCanvas.serverScreen.show2();
					}
					else
					{
						Res.outz("login ngay");
						SmallImage.loadBigRMS();
						SplashScr.imgLogo = null;
						ServerListScreen.loadScreen = true;
						if (GameCanvas.currentScreen != GameCanvas.loginScr)
						{
							GameCanvas.serverScreen.switchToMe();
						}
					}
				}
				if (b31 == 1)
				{
					ServerListScreen.strWait = mResources.downloading_data;
					short nBig = msg.reader().readShort();
					ServerListScreen.nBig = nBig;
					Service.gI().getResource(2, null);
				}
				if (b31 == 2)
				{
					try
					{
						isLoadingData = true;
						GameCanvas.endDlg();
						ServerListScreen.demPercent++;
						ServerListScreen.percent = ServerListScreen.demPercent * 100 / ServerListScreen.nBig;
						string original = msg.reader().readUTF();
						string[] array9 = Res.split(original, "/", 0);
						string filename = "x" + mGraphics.zoomLevel + array9[array9.Length - 1];
						int num81 = msg.reader().readInt();
						sbyte[] data3 = new sbyte[num81];
						msg.reader().read(ref data3, 0, num81);
						Rms.saveRMS(filename, data3);
					}
					catch (Exception)
					{
						GameCanvas.startOK(mResources.pls_restart_game_error, 8885, null);
					}
				}
				if (b31 == 3 && flag6)
				{
					isLoadingData = false;
					int num82 = msg.reader().readInt();
					Res.outz("last version= " + num82);
					Rms.saveRMSString("ResVersion", num82 + string.Empty);
					Service.gI().getResource(3, null);
					GameCanvas.endDlg();
					SplashScr.imgLogo = null;
					SmallImage.loadBigRMS();
					mSystem.gcc();
					ServerListScreen.bigOk = true;
					ServerListScreen.loadScreen = true;
					GameScr.gI().loadGameScr();
					if (GameCanvas.currentScreen != GameCanvas.loginScr)
					{
						GameCanvas.serverScreen.switchToMe();
					}
				}
				break;
			}
			case -43:
			{
				sbyte itemAction = msg.reader().readByte();
				sbyte where = msg.reader().readByte();
				sbyte index2 = msg.reader().readByte();
				string info2 = msg.reader().readUTF();
				GameCanvas.panel.itemRequest(itemAction, info2, where, index2);
				break;
			}
			case -59:
			{
				sbyte typePK = msg.reader().readByte();
				GameScr.gI().player_vs_player(msg.reader().readInt(), msg.reader().readInt(), msg.reader().readUTF(), typePK);
				break;
			}
			case -62:
			{
				int num53 = msg.reader().readUnsignedByte();
				sbyte b25 = msg.reader().readByte();
				if (b25 <= 0)
				{
					break;
				}
				ClanImage clanImage2 = ClanImage.getClanImage((short)num53);
				if (clanImage2 == null)
				{
					break;
				}
				clanImage2.idImage = new short[b25];
				for (int num54 = 0; num54 < b25; num54++)
				{
					clanImage2.idImage[num54] = msg.reader().readShort();
					if (clanImage2.idImage[num54] > 0)
					{
						SmallImage.vKeys.addElement(clanImage2.idImage[num54] + string.Empty);
					}
				}
				break;
			}
			case -65:
			{
				Res.outz("TELEPORT ...................................................");
				InfoDlg.hide();
				int num105 = msg.reader().readInt();
				sbyte b37 = msg.reader().readByte();
				if (b37 == 0)
				{
					break;
				}
				if (Char.myCharz().charID == num105)
				{
					isStopReadMessage = true;
					GameScr.lockTick = 500;
					GameScr.gI().center = null;
					if (b37 == 0 || b37 == 1 || b37 == 3)
					{
						Teleport p2 = new Teleport(Char.myCharz().cx, Char.myCharz().cy, Char.myCharz().head, Char.myCharz().cdir, 0, isMe: true, (b37 != 1) ? b37 : Char.myCharz().cgender);
						Teleport.addTeleport(p2);
					}
					if (b37 == 2)
					{
						GameScr.lockTick = 50;
						Char.myCharz().hide();
					}
				}
				else
				{
					Char char7 = GameScr.findCharInMap(num105);
					if ((b37 == 0 || b37 == 1 || b37 == 3) && char7 != null)
					{
						char7.isUsePlane = true;
						Teleport teleport = new Teleport(char7.cx, char7.cy, char7.head, char7.cdir, 0, isMe: false, (b37 != 1) ? b37 : char7.cgender);
						teleport.id = num105;
						Teleport.addTeleport(teleport);
					}
					if (b37 == 2)
					{
						char7.hide();
					}
				}
				break;
			}
			case -64:
			{
				int num42 = msg.reader().readInt();
				int bag = msg.reader().readUnsignedByte();
				@char = null;
				@char = ((num42 != Char.myCharz().charID) ? GameScr.findCharInMap(num42) : Char.myCharz());
				@char.bag = bag;
				if (@char.bag >= 201 && @char.bag < 2000)
				{
					Effect effect = new Effect(@char.bag, @char, 2, -1, 10, 1);
					effect.typeEff = 5;
					@char.addEffChar(effect);
				}
				else
				{
					for (int num43 = 0; num43 < 54; num43++)
					{
						@char.removeEffChar(0, 201 + num43);
					}
				}
				Res.outz("cmd:-64 UPDATE BAG PLAER = " + ((@char != null) ? @char.cName : string.Empty) + num42 + " BAG ID= " + bag);
				break;
			}
			case -63:
			{
				Res.outz("GET BAG");
				int iD = msg.reader().readUnsignedByte();
				sbyte b9 = msg.reader().readByte();
				ClanImage clanImage = new ClanImage();
				clanImage.ID = iD;
				if (b9 > 0)
				{
					clanImage.idImage = new short[b9];
					for (int n = 0; n < b9; n++)
					{
						clanImage.idImage[n] = msg.reader().readShort();
						Res.outz("ID=  " + iD + " frame= " + clanImage.idImage[n]);
					}
					ClanImage.idImages.put(iD + string.Empty, clanImage);
				}
				break;
			}
			case -57:
			{
				string strInvite = msg.reader().readUTF();
				int clanID = msg.reader().readInt();
				int code = msg.reader().readInt();
				GameScr.gI().clanInvite(strInvite, clanID, code);
				break;
			}
			case -51:
				InfoDlg.hide();
				readClanMsg(msg, 0);
				if (GameCanvas.panel.isMessage && GameCanvas.panel.type == 5)
				{
					GameCanvas.panel.initTabClans();
				}
				break;
			case -53:
			{
				InfoDlg.hide();
				bool flag7 = false;
				int num97 = msg.reader().readInt();
				Res.outz("clanId= " + num97);
				if (num97 == -1)
				{
					flag7 = true;
					Char.myCharz().clan = null;
					ClanMessage.vMessage.removeAllElements();
					if (GameCanvas.panel.member != null)
					{
						GameCanvas.panel.member.removeAllElements();
					}
					if (GameCanvas.panel.myMember != null)
					{
						GameCanvas.panel.myMember.removeAllElements();
					}
					if (GameCanvas.currentScreen == GameScr.gI())
					{
						GameCanvas.panel.setTabClans();
					}
					return;
				}
				GameCanvas.panel.tabIcon = null;
				if (Char.myCharz().clan == null)
				{
					Char.myCharz().clan = new Clan();
				}
				Char.myCharz().clan.ID = num97;
				Char.myCharz().clan.name = msg.reader().readUTF();
				Char.myCharz().clan.slogan = msg.reader().readUTF();
				Char.myCharz().clan.imgID = msg.reader().readUnsignedByte();
				Char.myCharz().clan.powerPoint = msg.reader().readUTF();
				Char.myCharz().clan.leaderName = msg.reader().readUTF();
				Char.myCharz().clan.currMember = msg.reader().readUnsignedByte();
				Char.myCharz().clan.maxMember = msg.reader().readUnsignedByte();
				Char.myCharz().role = msg.reader().readByte();
				Char.myCharz().clan.clanPoint = msg.reader().readInt();
				Char.myCharz().clan.level = msg.reader().readByte();
				GameCanvas.panel.myMember = new MyVector();
				for (int num98 = 0; num98 < Char.myCharz().clan.currMember; num98++)
				{
					Member member5 = new Member();
					member5.ID = msg.reader().readInt();
					member5.head = msg.reader().readShort();
					member5.headICON = msg.reader().readShort();
					member5.leg = msg.reader().readShort();
					member5.body = msg.reader().readShort();
					member5.name = msg.reader().readUTF();
					member5.role = msg.reader().readByte();
					member5.powerPoint = msg.reader().readUTF();
					member5.donate = msg.reader().readInt();
					member5.receive_donate = msg.reader().readInt();
					member5.clanPoint = msg.reader().readInt();
					member5.curClanPoint = msg.reader().readInt();
					member5.joinTime = NinjaUtil.getDate(msg.reader().readInt());
					GameCanvas.panel.myMember.addElement(member5);
				}
				int num99 = msg.reader().readUnsignedByte();
				for (int num100 = 0; num100 < num99; num100++)
				{
					readClanMsg(msg, -1);
				}
				if (GameCanvas.panel.isSearchClan || GameCanvas.panel.isViewMember || GameCanvas.panel.isMessage)
				{
					GameCanvas.panel.setTabClans();
				}
				if (flag7)
				{
					GameCanvas.panel.setTabClans();
				}
				Res.outz("=>>>>>>>>>>>>>>>>>>>>>> -537 MY CLAN INFO");
				break;
			}
			case -52:
			{
				sbyte b30 = msg.reader().readByte();
				if (b30 == 0)
				{
					Member member2 = new Member();
					member2.ID = msg.reader().readInt();
					member2.head = msg.reader().readShort();
					member2.headICON = msg.reader().readShort();
					member2.leg = msg.reader().readShort();
					member2.body = msg.reader().readShort();
					member2.name = msg.reader().readUTF();
					member2.role = msg.reader().readByte();
					member2.powerPoint = msg.reader().readUTF();
					member2.donate = msg.reader().readInt();
					member2.receive_donate = msg.reader().readInt();
					member2.clanPoint = msg.reader().readInt();
					member2.joinTime = NinjaUtil.getDate(msg.reader().readInt());
					if (GameCanvas.panel.myMember == null)
					{
						GameCanvas.panel.myMember = new MyVector();
					}
					GameCanvas.panel.myMember.addElement(member2);
					GameCanvas.panel.initTabClans();
				}
				if (b30 == 1)
				{
					GameCanvas.panel.myMember.removeElementAt(msg.reader().readByte());
					Panel panel = GameCanvas.panel;
					Panel panel2 = panel;
					panel2.currentListLength--;
					GameCanvas.panel.initTabClans();
				}
				if (b30 == 2)
				{
					Member member3 = new Member();
					member3.ID = msg.reader().readInt();
					member3.head = msg.reader().readShort();
					member3.headICON = msg.reader().readShort();
					member3.leg = msg.reader().readShort();
					member3.body = msg.reader().readShort();
					member3.name = msg.reader().readUTF();
					member3.role = msg.reader().readByte();
					member3.powerPoint = msg.reader().readUTF();
					member3.donate = msg.reader().readInt();
					member3.receive_donate = msg.reader().readInt();
					member3.clanPoint = msg.reader().readInt();
					member3.joinTime = NinjaUtil.getDate(msg.reader().readInt());
					for (int num76 = 0; num76 < GameCanvas.panel.myMember.size(); num76++)
					{
						Member member4 = (Member)GameCanvas.panel.myMember.elementAt(num76);
						if (member4.ID == member3.ID)
						{
							if (Char.myCharz().charID == member3.ID)
							{
								Char.myCharz().role = member3.role;
							}
							Member o = member3;
							GameCanvas.panel.myMember.removeElement(member4);
							GameCanvas.panel.myMember.insertElementAt(o, num76);
							return;
						}
					}
				}
				Res.outz("=>>>>>>>>>>>>>>>>>>>>>> -52  MY CLAN UPDSTE");
				break;
			}
			case -50:
			{
				InfoDlg.hide();
				GameCanvas.panel.member = new MyVector();
				sbyte b19 = msg.reader().readByte();
				for (int num33 = 0; num33 < b19; num33++)
				{
					Member member = new Member();
					member.ID = msg.reader().readInt();
					member.head = msg.reader().readShort();
					member.headICON = msg.reader().readShort();
					member.leg = msg.reader().readShort();
					member.body = msg.reader().readShort();
					member.name = msg.reader().readUTF();
					member.role = msg.reader().readByte();
					member.powerPoint = msg.reader().readUTF();
					member.donate = msg.reader().readInt();
					member.receive_donate = msg.reader().readInt();
					member.clanPoint = msg.reader().readInt();
					member.joinTime = NinjaUtil.getDate(msg.reader().readInt());
					GameCanvas.panel.member.addElement(member);
				}
				GameCanvas.panel.isViewMember = true;
				GameCanvas.panel.isSearchClan = false;
				GameCanvas.panel.isMessage = false;
				GameCanvas.panel.currentListLength = GameCanvas.panel.member.size() + 2;
				GameCanvas.panel.initTabClans();
				break;
			}
			case -47:
			{
				InfoDlg.hide();
				sbyte b6 = msg.reader().readByte();
				Res.outz("clan = " + b6);
				if (b6 == 0)
				{
					GameCanvas.panel.clanReport = mResources.cannot_find_clan;
					GameCanvas.panel.clans = null;
				}
				else
				{
					GameCanvas.panel.clans = new Clan[b6];
					Res.outz("clan search lent= " + GameCanvas.panel.clans.Length);
					for (int k = 0; k < GameCanvas.panel.clans.Length; k++)
					{
						GameCanvas.panel.clans[k] = new Clan();
						GameCanvas.panel.clans[k].ID = msg.reader().readInt();
						GameCanvas.panel.clans[k].name = msg.reader().readUTF();
						GameCanvas.panel.clans[k].slogan = msg.reader().readUTF();
						GameCanvas.panel.clans[k].imgID = msg.reader().readUnsignedByte();
						GameCanvas.panel.clans[k].powerPoint = msg.reader().readUTF();
						GameCanvas.panel.clans[k].leaderName = msg.reader().readUTF();
						GameCanvas.panel.clans[k].currMember = msg.reader().readUnsignedByte();
						GameCanvas.panel.clans[k].maxMember = msg.reader().readUnsignedByte();
						GameCanvas.panel.clans[k].date = msg.reader().readInt();
					}
				}
				GameCanvas.panel.isSearchClan = true;
				GameCanvas.panel.isViewMember = false;
				GameCanvas.panel.isMessage = false;
				if (GameCanvas.panel.isSearchClan)
				{
					GameCanvas.panel.initTabClans();
				}
				break;
			}
			case -46:
			{
				InfoDlg.hide();
				sbyte b63 = msg.reader().readByte();
				if (b63 == 1 || b63 == 3)
				{
					GameCanvas.endDlg();
					ClanImage.vClanImage.removeAllElements();
					int num172 = msg.reader().readUnsignedByte();
					for (int num173 = 0; num173 < num172; num173++)
					{
						ClanImage clanImage3 = new ClanImage();
						clanImage3.ID = msg.reader().readUnsignedByte();
						clanImage3.name = msg.reader().readUTF();
						clanImage3.xu = msg.reader().readInt();
						clanImage3.luong = msg.reader().readInt();
						if (!ClanImage.isExistClanImage(clanImage3.ID))
						{
							ClanImage.addClanImage(clanImage3);
							continue;
						}
						ClanImage.getClanImage((short)clanImage3.ID).name = clanImage3.name;
						ClanImage.getClanImage((short)clanImage3.ID).xu = clanImage3.xu;
						ClanImage.getClanImage((short)clanImage3.ID).luong = clanImage3.luong;
					}
					if (Char.myCharz().clan != null)
					{
						GameCanvas.panel.changeIcon();
					}
				}
				if (b63 == 4)
				{
					Char.myCharz().clan.imgID = msg.reader().readUnsignedByte();
					Char.myCharz().clan.slogan = msg.reader().readUTF();
				}
				break;
			}
			case -61:
			{
				int num131 = msg.reader().readInt();
				if (num131 != Char.myCharz().charID)
				{
					if (GameScr.findCharInMap(num131) != null)
					{
						GameScr.findCharInMap(num131).clanID = msg.reader().readInt();
						if (GameScr.findCharInMap(num131).clanID == -2)
						{
							GameScr.findCharInMap(num131).isCopy = true;
						}
					}
				}
				else if (Char.myCharz().clan != null)
				{
					Char.myCharz().clan.ID = msg.reader().readInt();
				}
				break;
			}
			case -42:
				Char.myCharz().cHPGoc = msg.readInt3Byte();
				Char.myCharz().cMPGoc = msg.readInt3Byte();
				Char.myCharz().cDamGoc = msg.reader().readInt();
				Char.myCharz().cHPFull = msg.readInt3Byte();
				Char.myCharz().cMPFull = msg.readInt3Byte();
				Char.myCharz().cHP = msg.readInt3Byte();
				Char.myCharz().cMP = msg.readInt3Byte();
				Char.myCharz().cspeed = msg.reader().readByte();
				Char.myCharz().hpFrom1000TiemNang = msg.reader().readByte();
				Char.myCharz().mpFrom1000TiemNang = msg.reader().readByte();
				Char.myCharz().damFrom1000TiemNang = msg.reader().readByte();
				Char.myCharz().cDamFull = msg.reader().readInt();
				Char.myCharz().cDefull = msg.reader().readInt();
				Char.myCharz().cCriticalFull = msg.reader().readByte();
				Char.myCharz().cTiemNang = msg.reader().readLong();
				Char.myCharz().expForOneAdd = msg.reader().readShort();
				Char.myCharz().cDefGoc = msg.reader().readShort();
				Char.myCharz().cCriticalGoc = msg.reader().readByte();
				InfoDlg.hide();
				break;
			case 1:
			{
				bool flag8 = msg.reader().readBool();
				Res.outz("isRes= " + flag8);
				if (!flag8)
				{
					GameCanvas.startOKDlg(msg.reader().readUTF());
					break;
				}
				GameCanvas.loginScr.isLogin2 = false;
				Rms.saveRMSString("userAo" + ServerListScreen.ipSelect, string.Empty);
				GameCanvas.endDlg();
				GameCanvas.loginScr.doLogin();
				break;
			}
			case 2:
				Char.isLoadingMap = true;
				LoginScr.isLoggingIn = false;
				if (!GameScr.isLoadAllData)
				{
					GameScr.gI().initSelectChar();
				}
				BgItem.clearHashTable();
				GameCanvas.endDlg();
				CreateCharScr.isCreateChar = true;
				CreateCharScr.gI().switchToMe();
				break;
			case -107:
			{
				sbyte b26 = msg.reader().readByte();
				if (b26 == 0)
				{
					Char.myCharz().havePet = false;
				}
				if (b26 == 1)
				{
					Char.myCharz().havePet = true;
				}
				if (b26 != 2)
				{
					break;
				}
				InfoDlg.hide();
				Char.myPetz().head = msg.reader().readShort();
				Char.myPetz().setDefaultPart();
				int num55 = msg.reader().readUnsignedByte();
				Res.outz("num body = " + num55);
				Char.myPetz().arrItemBody = new Item[num55];
				for (int num56 = 0; num56 < num55; num56++)
				{
					short num57 = msg.reader().readShort();
					Res.outz("template id= " + num57);
					if (num57 == -1)
					{
						continue;
					}
					Res.outz("1");
					Char.myPetz().arrItemBody[num56] = new Item();
					Char.myPetz().arrItemBody[num56].template = ItemTemplates.get(num57);
					int type2 = Char.myPetz().arrItemBody[num56].template.type;
					Char.myPetz().arrItemBody[num56].quantity = msg.reader().readInt();
					Res.outz("3");
					Char.myPetz().arrItemBody[num56].info = msg.reader().readUTF();
					Char.myPetz().arrItemBody[num56].content = msg.reader().readUTF();
					int num58 = msg.reader().readUnsignedByte();
					Res.outz("option size= " + num58);
					if (num58 != 0)
					{
						Char.myPetz().arrItemBody[num56].itemOption = new ItemOption[num58];
						for (int num59 = 0; num59 < Char.myPetz().arrItemBody[num56].itemOption.Length; num59++)
						{
							int num60 = msg.reader().readUnsignedByte();
							int param3 = msg.reader().readUnsignedShort();
							if (num60 != -1)
							{
								Char.myPetz().arrItemBody[num56].itemOption[num59] = new ItemOption(num60, param3);
							}
						}
					}
					switch (type2)
					{
					case 0:
						Char.myPetz().body = Char.myPetz().arrItemBody[num56].template.part;
						break;
					case 1:
						Char.myPetz().leg = Char.myPetz().arrItemBody[num56].template.part;
						break;
					}
				}
				Char.myPetz().cHP = msg.readInt3Byte();
				Char.myPetz().cHPFull = msg.readInt3Byte();
				Char.myPetz().cMP = msg.readInt3Byte();
				Char.myPetz().cMPFull = msg.readInt3Byte();
				Char.myPetz().cDamFull = msg.readInt3Byte();
				Char.myPetz().cName = msg.reader().readUTF();
				Char.myPetz().currStrLevel = msg.reader().readUTF();
				Char.myPetz().cPower = msg.reader().readLong();
				Char.myPetz().cTiemNang = msg.reader().readLong();
				Char.myPetz().petStatus = msg.reader().readByte();
				Char.myPetz().cStamina = msg.reader().readShort();
				Char.myPetz().cMaxStamina = msg.reader().readShort();
				Char.myPetz().cCriticalFull = msg.reader().readByte();
				Char.myPetz().cDefull = msg.reader().readShort();
				Char.myPetz().arrPetSkill = new Skill[msg.reader().readByte()];
				Res.outz("SKILLENT = " + Char.myPetz().arrPetSkill);
				for (int num61 = 0; num61 < Char.myPetz().arrPetSkill.Length; num61++)
				{
					short num62 = msg.reader().readShort();
					if (num62 != -1)
					{
						Char.myPetz().arrPetSkill[num61] = Skills.get(num62);
						continue;
					}
					Char.myPetz().arrPetSkill[num61] = new Skill();
					Char.myPetz().arrPetSkill[num61].template = null;
					Char.myPetz().arrPetSkill[num61].moreInfo = msg.reader().readUTF();
				}
				if (GameCanvas.w > 2 * Panel.WIDTH_PANEL)
				{
					GameCanvas.panel2 = new Panel();
					GameCanvas.panel2.tabName[7] = new string[1][] { new string[1] { string.Empty } };
					GameCanvas.panel2.setTypeBodyOnly();
					GameCanvas.panel2.show();
					GameCanvas.panel.setTypePetMain();
					GameCanvas.panel.show();
				}
				else
				{
					GameCanvas.panel.tabName[21] = mResources.petMainTab;
					GameCanvas.panel.setTypePetMain();
					GameCanvas.panel.show();
				}
				break;
			}
			case -37:
			{
				sbyte b20 = msg.reader().readByte();
				Res.outz("cAction= " + b20);
				if (b20 != 0)
				{
					break;
				}
				Char.myCharz().head = msg.reader().readShort();
				Char.myCharz().setDefaultPart();
				int num34 = msg.reader().readUnsignedByte();
				Res.outz("num body = " + num34);
				Char.myCharz().arrItemBody = new Item[num34];
				for (int num35 = 0; num35 < num34; num35++)
				{
					short num36 = msg.reader().readShort();
					if (num36 == -1)
					{
						continue;
					}
					Char.myCharz().arrItemBody[num35] = new Item();
					Char.myCharz().arrItemBody[num35].template = ItemTemplates.get(num36);
					int type = Char.myCharz().arrItemBody[num35].template.type;
					Char.myCharz().arrItemBody[num35].quantity = msg.reader().readInt();
					Char.myCharz().arrItemBody[num35].info = msg.reader().readUTF();
					Char.myCharz().arrItemBody[num35].content = msg.reader().readUTF();
					int num37 = msg.reader().readUnsignedByte();
					if (num37 != 0)
					{
						Char.myCharz().arrItemBody[num35].itemOption = new ItemOption[num37];
						for (int num38 = 0; num38 < Char.myCharz().arrItemBody[num35].itemOption.Length; num38++)
						{
							int num39 = msg.reader().readUnsignedByte();
							int param = msg.reader().readUnsignedShort();
							if (num39 != -1)
							{
								Char.myCharz().arrItemBody[num35].itemOption[num38] = new ItemOption(num39, param);
							}
						}
					}
					switch (type)
					{
					case 0:
						Char.myCharz().body = Char.myCharz().arrItemBody[num35].template.part;
						break;
					case 1:
						Char.myCharz().leg = Char.myCharz().arrItemBody[num35].template.part;
						break;
					}
				}
				break;
			}
			case -36:
			{
				sbyte b22 = msg.reader().readByte();
				Res.outz("cAction= " + b22);
				if (b22 == 0)
				{
					int num44 = msg.reader().readUnsignedByte();
					Char.myCharz().arrItemBag = new Item[num44];
					GameScr.hpPotion = 0;
					Res.outz("numC=" + num44);
					for (int num45 = 0; num45 < num44; num45++)
					{
						short num46 = msg.reader().readShort();
						if (num46 == -1)
						{
							continue;
						}
						Char.myCharz().arrItemBag[num45] = new Item();
						Char.myCharz().arrItemBag[num45].template = ItemTemplates.get(num46);
						Char.myCharz().arrItemBag[num45].quantity = msg.reader().readInt();
						Char.myCharz().arrItemBag[num45].info = msg.reader().readUTF();
						Char.myCharz().arrItemBag[num45].content = msg.reader().readUTF();
						Char.myCharz().arrItemBag[num45].indexUI = num45;
						int num47 = msg.reader().readUnsignedByte();
						if (num47 != 0)
						{
							Char.myCharz().arrItemBag[num45].itemOption = new ItemOption[num47];
							for (int num48 = 0; num48 < Char.myCharz().arrItemBag[num45].itemOption.Length; num48++)
							{
								int num49 = msg.reader().readUnsignedByte();
								int param2 = msg.reader().readUnsignedShort();
								if (num49 != -1)
								{
									Char.myCharz().arrItemBag[num45].itemOption[num48] = new ItemOption(num49, param2);
								}
							}
							Char.myCharz().arrItemBag[num45].compare = GameCanvas.panel.getCompare(Char.myCharz().arrItemBag[num45]);
						}
						if (Char.myCharz().arrItemBag[num45].template.type == 11)
						{
						}
						if (Char.myCharz().arrItemBag[num45].template.type == 6)
						{
							GameScr.hpPotion += Char.myCharz().arrItemBag[num45].quantity;
						}
					}
				}
				if (b22 == 2)
				{
					sbyte b23 = msg.reader().readByte();
					int quantity = msg.reader().readInt();
					int quantity2 = Char.myCharz().arrItemBag[b23].quantity;
					Char.myCharz().arrItemBag[b23].quantity = quantity;
					if (Char.myCharz().arrItemBag[b23].quantity < quantity2 && Char.myCharz().arrItemBag[b23].template.type == 6)
					{
						GameScr.hpPotion -= quantity2 - Char.myCharz().arrItemBag[b23].quantity;
					}
					if (Char.myCharz().arrItemBag[b23].quantity == 0)
					{
						Char.myCharz().arrItemBag[b23] = null;
					}
				}
				break;
			}
			case -35:
			{
				sbyte b59 = msg.reader().readByte();
				Res.outz("cAction= " + b59);
				if (b59 == 0)
				{
					int num160 = msg.reader().readUnsignedByte();
					Char.myCharz().arrItemBox = new Item[num160];
					GameCanvas.panel.hasUse = 0;
					for (int num161 = 0; num161 < num160; num161++)
					{
						short num162 = msg.reader().readShort();
						if (num162 == -1)
						{
							continue;
						}
						Char.myCharz().arrItemBox[num161] = new Item();
						Char.myCharz().arrItemBox[num161].template = ItemTemplates.get(num162);
						Char.myCharz().arrItemBox[num161].quantity = msg.reader().readInt();
						Char.myCharz().arrItemBox[num161].info = msg.reader().readUTF();
						Char.myCharz().arrItemBox[num161].content = msg.reader().readUTF();
						int num163 = msg.reader().readUnsignedByte();
						if (num163 != 0)
						{
							Char.myCharz().arrItemBox[num161].itemOption = new ItemOption[num163];
							for (int num164 = 0; num164 < Char.myCharz().arrItemBox[num161].itemOption.Length; num164++)
							{
								int num165 = msg.reader().readUnsignedByte();
								int param6 = msg.reader().readUnsignedShort();
								if (num165 != -1)
								{
									Char.myCharz().arrItemBox[num161].itemOption[num164] = new ItemOption(num165, param6);
								}
							}
						}
						Panel panel3 = GameCanvas.panel;
						Panel panel2 = panel3;
						panel2.hasUse++;
					}
				}
				if (b59 == 1)
				{
					bool isBoxClan = false;
					try
					{
						sbyte b60 = msg.reader().readByte();
						if (b60 == 1)
						{
							isBoxClan = true;
						}
					}
					catch (Exception)
					{
					}
					GameCanvas.panel.setTypeBox();
					GameCanvas.panel.isBoxClan = isBoxClan;
					GameCanvas.panel.show();
				}
				if (b59 == 2)
				{
					sbyte b61 = msg.reader().readByte();
					int quantity3 = msg.reader().readInt();
					Char.myCharz().arrItemBox[b61].quantity = quantity3;
					if (Char.myCharz().arrItemBox[b61].quantity == 0)
					{
						Char.myCharz().arrItemBox[b61] = null;
					}
				}
				break;
			}
			case -45:
			{
				sbyte b47 = msg.reader().readByte();
				int num136 = msg.reader().readInt();
				short num137 = msg.reader().readShort();
				Res.outz(">.SKILL_NOT_FOCUS  skill type= " + b47 + "   player use= " + num136);
				if (b47 == 20)
				{
					sbyte typeFrame = msg.reader().readByte();
					sbyte dir = msg.reader().readByte();
					short timeGong = msg.reader().readShort();
					bool isFly = ((msg.reader().readByte() != 0) ? true : false);
					sbyte typePaint = msg.reader().readByte();
					sbyte typeItem = -1;
					try
					{
						typeItem = msg.reader().readByte();
					}
					catch (Exception)
					{
					}
					Res.outz(">.SKILL_NOT_FOCUS  skill playerDir= " + dir);
					@char = ((Char.myCharz().charID != num136) ? GameScr.findCharInMap(num136) : Char.myCharz());
					@char.SetSkillPaint_NEW(num137, isFly, typeFrame, typePaint, dir, timeGong, typeItem);
				}
				if (b47 == 21)
				{
					Point point = new Point();
					point.x = msg.reader().readShort();
					point.y = msg.reader().readShort();
					short timeDame = msg.reader().readShort();
					short rangeDame = msg.reader().readShort();
					sbyte typePaint2 = 0;
					sbyte typeItem2 = -1;
					Point[] array14 = null;
					@char = ((Char.myCharz().charID != num136) ? GameScr.findCharInMap(num136) : Char.myCharz());
					try
					{
						typePaint2 = msg.reader().readByte();
						sbyte b48 = msg.reader().readByte();
						array14 = new Point[b48];
						for (int num138 = 0; num138 < array14.Length; num138++)
						{
							array14[num138] = new Point();
							array14[num138].type = msg.reader().readByte();
							if (array14[num138].type == 0)
							{
								array14[num138].id = msg.reader().readByte();
							}
							else
							{
								array14[num138].id = msg.reader().readInt();
							}
						}
					}
					catch (Exception)
					{
					}
					try
					{
						typeItem2 = msg.reader().readByte();
					}
					catch (Exception)
					{
					}
					Res.outz(">.SKILL_NOT_FOCUS  skill targetDame= " + point.x + ":" + point.y + "    c:" + @char.cx + ":" + @char.cy + "   cdir:" + @char.cdir);
					@char.SetSkillPaint_STT(1, num137, point, timeDame, rangeDame, typePaint2, array14, typeItem2);
				}
				if (b47 == 0)
				{
					Res.outz("id use= " + num136);
					if (Char.myCharz().charID != num136)
					{
						@char = GameScr.findCharInMap(num136);
						if ((TileMap.tileTypeAtPixel(@char.cx, @char.cy) & 2) == 2)
						{
							@char.setSkillPaint(GameScr.sks[num137], 0);
						}
						else
						{
							@char.setSkillPaint(GameScr.sks[num137], 1);
							@char.delayFall = 20;
						}
					}
					else
					{
						Char.myCharz().saveLoadPreviousSkill();
						Res.outz("LOAD LAST SKILL");
					}
					sbyte b49 = msg.reader().readByte();
					Res.outz("npc size= " + b49);
					for (int num139 = 0; num139 < b49; num139++)
					{
						sbyte index4 = msg.reader().readByte();
						sbyte seconds = msg.reader().readByte();
						Res.outz("index= " + index4);
						if (num137 >= 42 && num137 <= 48)
						{
							((Mob)GameScr.vMob.elementAt(index4)).isFreez = true;
							((Mob)GameScr.vMob.elementAt(index4)).seconds = seconds;
							((Mob)GameScr.vMob.elementAt(index4)).last = (((Mob)GameScr.vMob.elementAt(index4)).cur = mSystem.currentTimeMillis());
						}
					}
					sbyte b50 = msg.reader().readByte();
					for (int num140 = 0; num140 < b50; num140++)
					{
						int num141 = msg.reader().readInt();
						sbyte b51 = msg.reader().readByte();
						Res.outz("player ID= " + num141 + " my ID= " + Char.myCharz().charID);
						if (num137 < 42 || num137 > 48)
						{
							continue;
						}
						if (num141 == Char.myCharz().charID)
						{
							if (!Char.myCharz().isFlyAndCharge && !Char.myCharz().isStandAndCharge)
							{
								GameScr.gI().isFreez = true;
								Char.myCharz().isFreez = true;
								Char.myCharz().freezSeconds = b51;
								Char.myCharz().lastFreez = (Char.myCharz().currFreez = mSystem.currentTimeMillis());
								Char.myCharz().isLockMove = true;
							}
						}
						else
						{
							@char = GameScr.findCharInMap(num141);
							if (@char != null && !@char.isFlyAndCharge && !@char.isStandAndCharge)
							{
								@char.isFreez = true;
								@char.seconds = b51;
								@char.freezSeconds = b51;
								@char.lastFreez = (GameScr.findCharInMap(num141).currFreez = mSystem.currentTimeMillis());
							}
						}
					}
				}
				if (b47 == 1 && num136 != Char.myCharz().charID)
				{
					GameScr.findCharInMap(num136).isCharge = true;
				}
				if (b47 == 3)
				{
					if (num136 == Char.myCharz().charID)
					{
						Char.myCharz().isCharge = false;
						SoundMn.gI().taitaoPause();
						Char.myCharz().saveLoadPreviousSkill();
					}
					else
					{
						GameScr.findCharInMap(num136).isCharge = false;
					}
				}
				if (b47 == 4)
				{
					if (num136 == Char.myCharz().charID)
					{
						Char.myCharz().seconds = msg.reader().readShort() - 1000;
						Char.myCharz().last = mSystem.currentTimeMillis();
						Res.outz("second= " + Char.myCharz().seconds + " last= " + Char.myCharz().last);
					}
					else if (GameScr.findCharInMap(num136) != null)
					{
						switch (GameScr.findCharInMap(num136).cgender)
						{
						case 0:
							GameScr.findCharInMap(num136).useChargeSkill(isGround: false);
							break;
						case 1:
							GameScr.findCharInMap(num136).useChargeSkill(isGround: true);
							break;
						}
						GameScr.findCharInMap(num136).skillTemplateId = num137;
						GameScr.findCharInMap(num136).isUseSkillAfterCharge = true;
						GameScr.findCharInMap(num136).seconds = msg.reader().readShort();
						GameScr.findCharInMap(num136).last = mSystem.currentTimeMillis();
					}
				}
				if (b47 == 5)
				{
					if (num136 == Char.myCharz().charID)
					{
						Char.myCharz().stopUseChargeSkill();
					}
					else if (GameScr.findCharInMap(num136) != null)
					{
						GameScr.findCharInMap(num136).stopUseChargeSkill();
					}
				}
				if (b47 == 6)
				{
					if (num136 == Char.myCharz().charID)
					{
						Char.myCharz().setAutoSkillPaint(GameScr.sks[num137], 0);
					}
					else if (GameScr.findCharInMap(num136) != null)
					{
						GameScr.findCharInMap(num136).setAutoSkillPaint(GameScr.sks[num137], 0);
						SoundMn.gI().gong();
					}
				}
				if (b47 == 7)
				{
					if (num136 == Char.myCharz().charID)
					{
						Char.myCharz().seconds = msg.reader().readShort();
						Res.outz("second = " + Char.myCharz().seconds);
						Char.myCharz().last = mSystem.currentTimeMillis();
					}
					else if (GameScr.findCharInMap(num136) != null)
					{
						GameScr.findCharInMap(num136).useChargeSkill(isGround: true);
						GameScr.findCharInMap(num136).seconds = msg.reader().readShort();
						GameScr.findCharInMap(num136).last = mSystem.currentTimeMillis();
						SoundMn.gI().gong();
					}
				}
				if (b47 == 8 && num136 != Char.myCharz().charID && GameScr.findCharInMap(num136) != null)
				{
					GameScr.findCharInMap(num136).setAutoSkillPaint(GameScr.sks[num137], 0);
				}
				break;
			}
			case -44:
			{
				bool flag5 = false;
				if (GameCanvas.w > 2 * Panel.WIDTH_PANEL)
				{
					flag5 = true;
				}
				sbyte b27 = msg.reader().readByte();
				int num63 = msg.reader().readUnsignedByte();
				Char.myCharz().arrItemShop = new Item[num63][];
				GameCanvas.panel.shopTabName = new string[num63 + ((!flag5) ? 1 : 0)][];
				for (int num64 = 0; num64 < GameCanvas.panel.shopTabName.Length; num64++)
				{
					GameCanvas.panel.shopTabName[num64] = new string[2];
				}
				if (b27 == 2)
				{
					GameCanvas.panel.maxPageShop = new int[num63];
					GameCanvas.panel.currPageShop = new int[num63];
				}
				if (!flag5)
				{
					GameCanvas.panel.shopTabName[num63] = mResources.inventory;
				}
				for (int num65 = 0; num65 < num63; num65++)
				{
					string[] array5 = Res.split(msg.reader().readUTF(), "\n", 0);
					if (b27 == 2)
					{
						GameCanvas.panel.maxPageShop[num65] = msg.reader().readUnsignedByte();
					}
					if (array5.Length == 2)
					{
						GameCanvas.panel.shopTabName[num65] = array5;
					}
					if (array5.Length == 1)
					{
						GameCanvas.panel.shopTabName[num65][0] = array5[0];
						GameCanvas.panel.shopTabName[num65][1] = string.Empty;
					}
					int num66 = msg.reader().readUnsignedByte();
					Char.myCharz().arrItemShop[num65] = new Item[num66];
					Panel.strWantToBuy = mResources.say_wat_do_u_want_to_buy;
					if (b27 == 1)
					{
						Panel.strWantToBuy = mResources.say_wat_do_u_want_to_buy2;
					}
					for (int num67 = 0; num67 < num66; num67++)
					{
						short num68 = msg.reader().readShort();
						if (num68 == -1)
						{
							continue;
						}
						Char.myCharz().arrItemShop[num65][num67] = new Item();
						Char.myCharz().arrItemShop[num65][num67].template = ItemTemplates.get(num68);
						Res.outz("name " + num65 + " = " + Char.myCharz().arrItemShop[num65][num67].template.name + " id templat= " + Char.myCharz().arrItemShop[num65][num67].template.id);
						switch (b27)
						{
						case 8:
							Char.myCharz().arrItemShop[num65][num67].buyCoin = msg.reader().readInt();
							Char.myCharz().arrItemShop[num65][num67].buyGold = msg.reader().readInt();
							Char.myCharz().arrItemShop[num65][num67].quantity = msg.reader().readInt();
							break;
						case 4:
							Char.myCharz().arrItemShop[num65][num67].reason = msg.reader().readUTF();
							break;
						case 0:
							Char.myCharz().arrItemShop[num65][num67].buyCoin = msg.reader().readInt();
							Char.myCharz().arrItemShop[num65][num67].buyGold = msg.reader().readInt();
							break;
						case 1:
							Char.myCharz().arrItemShop[num65][num67].powerRequire = msg.reader().readLong();
							break;
						case 2:
							Char.myCharz().arrItemShop[num65][num67].itemId = msg.reader().readShort();
							Char.myCharz().arrItemShop[num65][num67].buyCoin = msg.reader().readInt();
							Char.myCharz().arrItemShop[num65][num67].buyGold = msg.reader().readInt();
							Char.myCharz().arrItemShop[num65][num67].buyType = msg.reader().readByte();
							Char.myCharz().arrItemShop[num65][num67].quantity = msg.reader().readInt();
							Char.myCharz().arrItemShop[num65][num67].isMe = msg.reader().readByte();
							break;
						case 3:
							Char.myCharz().arrItemShop[num65][num67].isBuySpec = true;
							Char.myCharz().arrItemShop[num65][num67].iconSpec = msg.reader().readShort();
							Char.myCharz().arrItemShop[num65][num67].buySpec = msg.reader().readInt();
							break;
						}
						int num69 = msg.reader().readUnsignedByte();
						if (num69 != 0)
						{
							Char.myCharz().arrItemShop[num65][num67].itemOption = new ItemOption[num69];
							for (int num70 = 0; num70 < Char.myCharz().arrItemShop[num65][num67].itemOption.Length; num70++)
							{
								int num71 = msg.reader().readUnsignedByte();
								int param4 = msg.reader().readUnsignedShort();
								if (num71 != -1)
								{
									Char.myCharz().arrItemShop[num65][num67].itemOption[num70] = new ItemOption(num71, param4);
									Char.myCharz().arrItemShop[num65][num67].compare = GameCanvas.panel.getCompare(Char.myCharz().arrItemShop[num65][num67]);
								}
							}
						}
						sbyte b28 = msg.reader().readByte();
						Char.myCharz().arrItemShop[num65][num67].newItem = ((b28 != 0) ? true : false);
						sbyte b29 = msg.reader().readByte();
						if (b29 == 1)
						{
							int headTemp = msg.reader().readShort();
							int bodyTemp = msg.reader().readShort();
							int legTemp = msg.reader().readShort();
							int bagTemp = msg.reader().readShort();
							Char.myCharz().arrItemShop[num65][num67].setPartTemp(headTemp, bodyTemp, legTemp, bagTemp);
						}
					}
				}
				if (flag5)
				{
					if (b27 != 2)
					{
						GameCanvas.panel2 = new Panel();
						GameCanvas.panel2.tabName[7] = new string[1][] { new string[1] { string.Empty } };
						GameCanvas.panel2.setTypeBodyOnly();
						GameCanvas.panel2.show();
					}
					else
					{
						GameCanvas.panel2 = new Panel();
						GameCanvas.panel2.setTypeKiGuiOnly();
						GameCanvas.panel2.show();
					}
				}
				GameCanvas.panel.tabName[1] = GameCanvas.panel.shopTabName;
				if (b27 == 2)
				{
					string[][] array6 = GameCanvas.panel.tabName[1];
					if (flag5)
					{
						GameCanvas.panel.tabName[1] = new string[4][]
						{
							array6[0],
							array6[1],
							array6[2],
							array6[3]
						};
					}
					else
					{
						GameCanvas.panel.tabName[1] = new string[5][]
						{
							array6[0],
							array6[1],
							array6[2],
							array6[3],
							array6[4]
						};
					}
				}
				GameCanvas.panel.setTypeShop(b27);
				GameCanvas.panel.show();
				break;
			}
			case -41:
			{
				sbyte b21 = msg.reader().readByte();
				Char.myCharz().strLevel = new string[b21];
				for (int num41 = 0; num41 < b21; num41++)
				{
					string text2 = msg.reader().readUTF();
					Char.myCharz().strLevel[num41] = text2;
				}
				Res.outz("---   xong  level caption cmd : " + msg.command);
				break;
			}
			case -34:
			{
				sbyte b17 = msg.reader().readByte();
				Res.outz("act= " + b17);
				if (b17 == 0 && GameScr.gI().magicTree != null)
				{
					Res.outz("toi duoc day");
					MagicTree magicTree = GameScr.gI().magicTree;
					magicTree.id = msg.reader().readShort();
					magicTree.name = msg.reader().readUTF();
					magicTree.name = Res.changeString(magicTree.name);
					magicTree.x = msg.reader().readShort();
					magicTree.y = msg.reader().readShort();
					magicTree.level = msg.reader().readByte();
					magicTree.currPeas = msg.reader().readShort();
					magicTree.maxPeas = msg.reader().readShort();
					Res.outz("curr Peas= " + magicTree.currPeas);
					magicTree.strInfo = msg.reader().readUTF();
					magicTree.seconds = msg.reader().readInt();
					magicTree.timeToRecieve = magicTree.seconds;
					sbyte b18 = msg.reader().readByte();
					magicTree.peaPostionX = new int[b18];
					magicTree.peaPostionY = new int[b18];
					for (int num32 = 0; num32 < b18; num32++)
					{
						magicTree.peaPostionX[num32] = msg.reader().readByte();
						magicTree.peaPostionY[num32] = msg.reader().readByte();
					}
					magicTree.isUpdate = msg.reader().readBool();
					magicTree.last = (magicTree.cur = mSystem.currentTimeMillis());
					GameScr.gI().magicTree.isUpdateTree = true;
				}
				if (b17 == 1)
				{
					myVector = new MyVector();
					try
					{
						while (msg.reader().available() > 0)
						{
							string caption = msg.reader().readUTF();
							myVector.addElement(new Command(caption, GameCanvas.instance, 888392, null));
						}
					}
					catch (Exception ex9)
					{
						Cout.println("Loi MAGIC_TREE " + ex9.ToString());
					}
					GameCanvas.menu.startAt(myVector, 3);
				}
				if (b17 == 2)
				{
					GameScr.gI().magicTree.remainPeas = msg.reader().readShort();
					GameScr.gI().magicTree.seconds = msg.reader().readInt();
					GameScr.gI().magicTree.last = (GameScr.gI().magicTree.cur = mSystem.currentTimeMillis());
					GameScr.gI().magicTree.isUpdateTree = true;
					GameScr.gI().magicTree.isPeasEffect = true;
				}
				break;
			}
			case 11:
			{
				GameCanvas.debug("SA9", 2);
				int num15 = msg.reader().readByte();
				sbyte b7 = msg.reader().readByte();
				if (b7 != 0)
				{
					Mob.arrMobTemplate[num15].data.readDataNewBoss(NinjaUtil.readByteArray(msg), b7);
				}
				else
				{
					Mob.arrMobTemplate[num15].data.readData(NinjaUtil.readByteArray(msg));
				}
				for (int l = 0; l < GameScr.vMob.size(); l++)
				{
					mob = (Mob)GameScr.vMob.elementAt(l);
					if (mob.templateId == num15)
					{
						mob.w = Mob.arrMobTemplate[num15].data.width;
						mob.h = Mob.arrMobTemplate[num15].data.height;
					}
				}
				sbyte[] array3 = NinjaUtil.readByteArray(msg);
				Image img = Image.createImage(array3, 0, array3.Length);
				Mob.arrMobTemplate[num15].data.img = img;
				int num16 = msg.reader().readByte();
				Mob.arrMobTemplate[num15].data.typeData = num16;
				if (num16 == 1 || num16 == 2)
				{
					readFrameBoss(msg, num15);
				}
				break;
			}
			case -69:
				Char.myCharz().cMaxStamina = msg.reader().readShort();
				break;
			case -68:
				Char.myCharz().cStamina = msg.reader().readShort();
				break;
			case -67:
			{
				Res.outz("RECIEVE ICON");
				demCount += 1f;
				int num171 = msg.reader().readInt();
				sbyte[] array19 = null;
				try
				{
					array19 = NinjaUtil.readByteArray(msg);
					Res.outz("request hinh icon = " + num171);
					if (num171 == 3896)
					{
						Res.outz("SIZE CHECK= " + array19.Length);
					}
					SmallImage.imgNew[num171].img = createImage(array19);
				}
				catch (Exception)
				{
					array19 = null;
					SmallImage.imgNew[num171].img = Image.createRGBImage(new int[1], 1, 1, bl: true);
				}
				if (array19 != null)
				{
					Rms.saveRMS(mGraphics.zoomLevel + "Small" + num171, array19);
				}
				break;
			}
			case -66:
			{
				short num146 = msg.reader().readShort();
				sbyte[] data5 = NinjaUtil.readByteArray(msg);
				EffectData effDataById = Effect.getEffDataById(num146);
				if (effDataById == null)
				{
					effDataById = new EffectData { ID = num146 };
					Effect.addEffData(effDataById);
				}
				sbyte b54 = msg.reader().readSByte();
				if (b54 == 0)
				{
					effDataById.readData(data5);
				}
				else
				{
					effDataById.readDataNewBoss(data5, b54);
				}
				sbyte[] array17 = NinjaUtil.readByteArray(msg);
				effDataById.img = Image.createImage(array17, 0, array17.Length);
				Res.outz("err5 ");
				if (num146 != 78)
				{
					break;
				}
				sbyte b55 = msg.reader().readByte();
				short[][] array18 = new short[b55][];
				for (int num147 = 0; num147 < b55; num147++)
				{
					int num148 = msg.reader().readUnsignedByte();
					array18[num147] = new short[num148];
					for (int num149 = 0; num149 < num148; num149++)
					{
						array18[num147][num149] = msg.reader().readShort();
					}
				}
				effDataById.anim_data = array18;
				break;
			}
			case -32:
			{
				short id2 = msg.reader().readShort();
				int num134 = msg.reader().readInt();
				sbyte[] array13 = null;
				Image image = null;
				try
				{
					array13 = new sbyte[num134];
					for (int num135 = 0; num135 < num134; num135++)
					{
						array13[num135] = msg.reader().readByte();
					}
					image = Image.createImage(array13, 0, num134);
					BgItem.imgNew.put(id2 + string.Empty, image);
				}
				catch (Exception)
				{
					array13 = null;
					BgItem.imgNew.put(id2 + string.Empty, Image.createRGBImage(new int[1], 1, 1, bl: true));
				}
				if (array13 != null)
				{
					if (mGraphics.zoomLevel > 1)
					{
						Rms.saveRMS(mGraphics.zoomLevel + "bgItem" + id2, array13);
					}
					BgItemMn.blendcurrBg(id2, image);
				}
				break;
			}
			case 92:
			{
				if (GameCanvas.currentScreen == GameScr.instance)
				{
					GameCanvas.endDlg();
				}
				string text4 = msg.reader().readUTF();
				string str2 = msg.reader().readUTF();
				str2 = Res.changeString(str2);
				string empty = string.Empty;
				Char char6 = null;
				sbyte b36 = 0;
				if (!text4.Equals(string.Empty))
				{
					char6 = new Char();
					char6.charID = msg.reader().readInt();
					char6.head = msg.reader().readShort();
					char6.headICON = msg.reader().readShort();
					char6.body = msg.reader().readShort();
					char6.bag = msg.reader().readShort();
					char6.leg = msg.reader().readShort();
					b36 = msg.reader().readByte();
					char6.cName = text4;
				}
				empty += str2;
				InfoDlg.hide();
				if (text4.Equals(string.Empty))
				{
					GameScr.info1.addInfo(empty, 0);
					break;
				}
				GameScr.info2.addInfoWithChar(empty, char6, b36 == 0);
				if (GameCanvas.panel.isShow && GameCanvas.panel.type == 8)
				{
					GameCanvas.panel.initLogMessage();
				}
				break;
			}
			case -26:
				ServerListScreen.testConnect = 2;
				GameCanvas.debug("SA2", 2);
				GameCanvas.startOKDlg(msg.reader().readUTF());
				InfoDlg.hide();
				LoginScr.isContinueToLogin = false;
				Char.isLoadingMap = false;
				if (GameCanvas.currentScreen == GameCanvas.loginScr)
				{
					GameCanvas.serverScreen.switchToMe();
				}
				break;
			case -25:
				GameCanvas.debug("SA3", 2);
				GameScr.info1.addInfo(msg.reader().readUTF(), 0);
				break;
			case 94:
				GameCanvas.debug("SA3", 2);
				GameScr.info1.addInfo(msg.reader().readUTF(), 0);
				break;
			case 47:
				GameCanvas.debug("SA4", 2);
				GameScr.gI().resetButton();
				break;
			case 81:
			{
				GameCanvas.debug("SXX4", 2);
				Mob mob12 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				mob12.isDisable = msg.reader().readBool();
				break;
			}
			case 82:
			{
				GameCanvas.debug("SXX5", 2);
				Mob mob11 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				mob11.isDontMove = msg.reader().readBool();
				break;
			}
			case 85:
			{
				GameCanvas.debug("SXX5", 2);
				Mob mob10 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				mob10.isFire = msg.reader().readBool();
				break;
			}
			case 86:
			{
				GameCanvas.debug("SXX5", 2);
				Mob mob8 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				mob8.isIce = msg.reader().readBool();
				if (!mob8.isIce)
				{
					ServerEffect.addServerEffect(77, mob8.x, mob8.y - 9, 1);
				}
				break;
			}
			case 87:
			{
				GameCanvas.debug("SXX5", 2);
				Mob mob9 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				mob9.isWind = msg.reader().readBool();
				break;
			}
			case 56:
			{
				GameCanvas.debug("SXX6", 2);
				@char = null;
				int num20 = msg.reader().readInt();
				if (num20 == Char.myCharz().charID)
				{
					bool flag3 = false;
					@char = Char.myCharz();
					@char.cHP = msg.readInt3Byte();
					int num21 = msg.readInt3Byte();
					Res.outz("dame hit = " + num21);
					if (num21 != 0)
					{
						@char.doInjure();
					}
					int num22 = 0;
					try
					{
						flag3 = msg.reader().readBoolean();
						sbyte b10 = msg.reader().readByte();
						if (b10 != -1)
						{
							Res.outz("hit eff= " + b10);
							EffecMn.addEff(new Effect(b10, @char.cx, @char.cy, 3, 1, -1));
						}
					}
					catch (Exception)
					{
					}
					num21 += num22;
					if (Char.myCharz().cTypePk != 4)
					{
						if (num21 == 0)
						{
							GameScr.startFlyText(mResources.miss, @char.cx, @char.cy - @char.ch, 0, -3, mFont.MISS_ME);
						}
						else
						{
							GameScr.startFlyText("-" + num21, @char.cx, @char.cy - @char.ch, 0, -3, flag3 ? mFont.FATAL : mFont.RED);
						}
					}
					break;
				}
				@char = GameScr.findCharInMap(num20);
				if (@char == null)
				{
					return;
				}
				@char.cHP = msg.readInt3Byte();
				bool flag4 = false;
				int num23 = msg.readInt3Byte();
				if (num23 != 0)
				{
					@char.doInjure();
				}
				int num24 = 0;
				try
				{
					flag4 = msg.reader().readBoolean();
					sbyte b11 = msg.reader().readByte();
					if (b11 != -1)
					{
						Res.outz("hit eff= " + b11);
						EffecMn.addEff(new Effect(b11, @char.cx, @char.cy, 3, 1, -1));
					}
				}
				catch (Exception)
				{
				}
				num23 += num24;
				if (@char.cTypePk != 4)
				{
					if (num23 == 0)
					{
						GameScr.startFlyText(mResources.miss, @char.cx, @char.cy - @char.ch, 0, -3, mFont.MISS);
					}
					else
					{
						GameScr.startFlyText("-" + num23, @char.cx, @char.cy - @char.ch, 0, -3, flag4 ? mFont.FATAL : mFont.ORANGE);
					}
				}
				break;
			}
			case 83:
			{
				GameCanvas.debug("SXX8", 2);
				int num14 = msg.reader().readInt();
				@char = ((num14 != Char.myCharz().charID) ? GameScr.findCharInMap(num14) : Char.myCharz());
				if (@char == null)
				{
					return;
				}
				Mob mobToAttack = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				if (@char.mobMe != null)
				{
					@char.mobMe.attackOtherMob(mobToAttack);
				}
				break;
			}
			case 84:
			{
				int num9 = msg.reader().readInt();
				if (num9 == Char.myCharz().charID)
				{
					@char = Char.myCharz();
				}
				else
				{
					@char = GameScr.findCharInMap(num9);
					if (@char == null)
					{
						return;
					}
				}
				@char.cHP = @char.cHPFull;
				@char.cMP = @char.cMPFull;
				@char.cx = msg.reader().readShort();
				@char.cy = msg.reader().readShort();
				@char.liveFromDead();
				break;
			}
			case 46:
				GameCanvas.debug("SA5", 2);
				Cout.LogWarning("Controler RESET_POINT  " + Char.ischangingMap);
				Char.isLockKey = false;
				Char.myCharz().setResetPoint(msg.reader().readShort(), msg.reader().readShort());
				break;
			case -29:
				messageNotLogin(msg);
				break;
			case -28:
				messageNotMap(msg);
				break;
			case -30:
				messageSubCommand(msg);
				break;
			case 62:
				GameCanvas.debug("SZ3", 2);
				@char = GameScr.findCharInMap(msg.reader().readInt());
				if (@char != null)
				{
					@char.killCharId = Char.myCharz().charID;
					Char.myCharz().npcFocus = null;
					Char.myCharz().mobFocus = null;
					Char.myCharz().itemFocus = null;
					Char.myCharz().charFocus = @char;
					Char.isManualFocus = true;
					GameScr.info1.addInfo(@char.cName + mResources.CUU_SAT, 0);
				}
				break;
			case 63:
				GameCanvas.debug("SZ4", 2);
				Char.myCharz().killCharId = msg.reader().readInt();
				Char.myCharz().npcFocus = null;
				Char.myCharz().mobFocus = null;
				Char.myCharz().itemFocus = null;
				Char.myCharz().charFocus = GameScr.findCharInMap(Char.myCharz().killCharId);
				Char.isManualFocus = true;
				break;
			case 64:
				GameCanvas.debug("SZ5", 2);
				@char = Char.myCharz();
				try
				{
					@char = GameScr.findCharInMap(msg.reader().readInt());
				}
				catch (Exception ex2)
				{
					Cout.println("Loi CLEAR_CUU_SAT " + ex2.ToString());
				}
				@char.killCharId = -9999;
				break;
			case 39:
				GameCanvas.debug("SA49", 2);
				GameScr.gI().typeTradeOrder = 2;
				if (GameScr.gI().typeTrade >= 2 && GameScr.gI().typeTradeOrder >= 2)
				{
					InfoDlg.showWait();
				}
				break;
			case 57:
			{
				GameCanvas.debug("SZ6", 2);
				MyVector myVector2 = new MyVector();
				myVector2.addElement(new Command(msg.reader().readUTF(), GameCanvas.instance, 88817, null));
				GameCanvas.menu.startAt(myVector2, 3);
				break;
			}
			case 58:
			{
				GameCanvas.debug("SZ7", 2);
				int num168 = msg.reader().readInt();
				Char char14 = ((num168 != Char.myCharz().charID) ? GameScr.findCharInMap(num168) : Char.myCharz());
				char14.moveFast = new short[3];
				char14.moveFast[0] = 0;
				short num169 = msg.reader().readShort();
				short num170 = msg.reader().readShort();
				char14.moveFast[1] = num169;
				char14.moveFast[2] = num170;
				try
				{
					num168 = msg.reader().readInt();
					Char char15 = ((num168 != Char.myCharz().charID) ? GameScr.findCharInMap(num168) : Char.myCharz());
					char15.cx = num169;
					char15.cy = num170;
				}
				catch (Exception ex24)
				{
					Cout.println("Loi MOVE_FAST " + ex24.ToString());
				}
				break;
			}
			case 88:
			{
				string info4 = msg.reader().readUTF();
				short num167 = msg.reader().readShort();
				GameCanvas.inputDlg.show(info4, new Command(mResources.ACCEPT, GameCanvas.instance, 88818, num167), TField.INPUT_TYPE_ANY);
				break;
			}
			case 27:
			{
				myVector = new MyVector();
				string text7 = msg.reader().readUTF();
				int num157 = msg.reader().readByte();
				for (int num158 = 0; num158 < num157; num158++)
				{
					string caption4 = msg.reader().readUTF();
					short num159 = msg.reader().readShort();
					myVector.addElement(new Command(caption4, GameCanvas.instance, 88819, num159));
				}
				GameCanvas.menu.startWithoutCloseButton(myVector, 3);
				break;
			}
			case 33:
			{
				GameCanvas.debug("SA51", 2);
				InfoDlg.hide();
				GameCanvas.clearKeyHold();
				GameCanvas.clearKeyPressed();
				myVector = new MyVector();
				try
				{
					while (true)
					{
						string caption3 = msg.reader().readUTF();
						myVector.addElement(new Command(caption3, GameCanvas.instance, 88822, null));
					}
				}
				catch (Exception ex22)
				{
					Cout.println("Loi OPEN_UI_MENU " + ex22.ToString());
				}
				if (Char.myCharz().npcFocus == null)
				{
					return;
				}
				for (int num145 = 0; num145 < Char.myCharz().npcFocus.template.menu.Length; num145++)
				{
					string[] array16 = Char.myCharz().npcFocus.template.menu[num145];
					myVector.addElement(new Command(array16[0], GameCanvas.instance, 88820, array16));
				}
				GameCanvas.menu.startAt(myVector, 3);
				break;
			}
			case 40:
			{
				GameCanvas.debug("SA52", 2);
				GameCanvas.taskTick = 150;
				short taskId = msg.reader().readShort();
				sbyte index3 = msg.reader().readByte();
				string str3 = msg.reader().readUTF();
				str3 = Res.changeString(str3);
				string str4 = msg.reader().readUTF();
				str4 = Res.changeString(str4);
				string[] array10 = new string[msg.reader().readByte()];
				string[] array11 = new string[array10.Length];
				GameScr.tasks = new int[array10.Length];
				GameScr.mapTasks = new int[array10.Length];
				short[] array12 = new short[array10.Length];
				short count = -1;
				for (int num132 = 0; num132 < array10.Length; num132++)
				{
					string str5 = msg.reader().readUTF();
					str5 = Res.changeString(str5);
					GameScr.tasks[num132] = msg.reader().readByte();
					GameScr.mapTasks[num132] = msg.reader().readShort();
					string str6 = msg.reader().readUTF();
					str6 = Res.changeString(str6);
					array12[num132] = -1;
					if (!str5.Equals(string.Empty))
					{
						array10[num132] = str5;
						array11[num132] = str6;
					}
				}
				try
				{
					count = msg.reader().readShort();
					for (int num133 = 0; num133 < array10.Length; num133++)
					{
						array12[num133] = msg.reader().readShort();
					}
				}
				catch (Exception ex17)
				{
					Cout.println("Loi TASK_GET " + ex17.ToString());
				}
				Char.myCharz().taskMaint = new Task(taskId, index3, str3, str4, array10, array12, count, array11);
				if (Char.myCharz().npcFocus != null)
				{
					Npc.clearEffTask();
				}
				Char.taskAction(isNextStep: false);
				break;
			}
			case 41:
			{
				GameCanvas.debug("SA53", 2);
				GameCanvas.taskTick = 100;
				Res.outz("TASK NEXT");
				Task taskMaint = Char.myCharz().taskMaint;
				Task task = taskMaint;
				task.index++;
				Char.myCharz().taskMaint.count = 0;
				Npc.clearEffTask();
				Char.taskAction(isNextStep: true);
				break;
			}
			case 50:
			{
				sbyte b46 = msg.reader().readByte();
				Panel.vGameInfo.removeAllElements();
				for (int num130 = 0; num130 < b46; num130++)
				{
					GameInfo gameInfo = new GameInfo();
					gameInfo.id = msg.reader().readShort();
					gameInfo.main = msg.reader().readUTF();
					gameInfo.content = msg.reader().readUTF();
					Panel.vGameInfo.addElement(gameInfo);
					bool hasRead = Rms.loadRMSInt(gameInfo.id + string.Empty) != -1;
					gameInfo.hasRead = hasRead;
				}
				break;
			}
			case 43:
				GameCanvas.taskTick = 50;
				GameCanvas.debug("SA55", 2);
				Char.myCharz().taskMaint.count = msg.reader().readShort();
				if (Char.myCharz().npcFocus != null)
				{
					Npc.clearEffTask();
				}
				try
				{
					short x_hint = msg.reader().readShort();
					short y_hint = msg.reader().readShort();
					Char.myCharz().x_hint = x_hint;
					Char.myCharz().y_hint = y_hint;
					Res.outz("CMD   TASK_UPDATE:43_mapID =    x|y " + x_hint + "|" + y_hint);
					for (int num123 = 0; num123 < TileMap.vGo.size(); num123++)
					{
						Res.outz("===> " + TileMap.vGo.elementAt(num123));
					}
				}
				catch (Exception)
				{
				}
				break;
			case 90:
				GameCanvas.debug("SA577", 2);
				requestItemPlayer(msg);
				break;
			case 29:
				GameCanvas.debug("SA58", 2);
				GameScr.gI().openUIZone(msg);
				AssemblyCSharp.Mod.AutoGame.OnZoneListReceived();
				break;
			case -21:
			{
				GameCanvas.debug("SA60", 2);
				short num120 = msg.reader().readShort();
				for (int num121 = 0; num121 < GameScr.vItemMap.size(); num121++)
				{
					if (((ItemMap)GameScr.vItemMap.elementAt(num121)).itemMapID == num120)
					{
						GameScr.vItemMap.removeElementAt(num121);
						break;
					}
				}
				break;
			}
			case -20:
			{
				GameCanvas.debug("SA61", 2);
				Char.myCharz().itemFocus = null;
				short num118 = msg.reader().readShort();
				for (int num119 = 0; num119 < GameScr.vItemMap.size(); num119++)
				{
					ItemMap itemMap2 = (ItemMap)GameScr.vItemMap.elementAt(num119);
					if (itemMap2.itemMapID != num118)
					{
						continue;
					}
					itemMap2.setPoint(Char.myCharz().cx, Char.myCharz().cy - 10);
					string text5 = msg.reader().readUTF();
					num = 0;
					try
					{
						num = msg.reader().readShort();
						if (itemMap2.template.type == 9)
						{
							num = msg.reader().readShort();
							Char char8 = Char.myCharz();
							Char char9 = char8;
							char9.xu += num;
							Char.myCharz().xuStr = mSystem.numberTostring(Char.myCharz().xu);
						}
						else if (itemMap2.template.type == 10)
						{
							num = msg.reader().readShort();
							Char char10 = Char.myCharz();
							Char char9 = char10;
							char9.luong += num;
							Char.myCharz().luongStr = mSystem.numberTostring(Char.myCharz().luong);
						}
						else if (itemMap2.template.type == 34)
						{
							num = msg.reader().readShort();
							Char char11 = Char.myCharz();
							Char char9 = char11;
							char9.luongKhoa += num;
							Char.myCharz().luongKhoaStr = mSystem.numberTostring(Char.myCharz().luongKhoa);
						}
					}
					catch (Exception)
					{
					}
					if (text5.Equals(string.Empty))
					{
						if (itemMap2.template.type == 9)
						{
							GameScr.startFlyText(((num >= 0) ? "+" : string.Empty) + num, Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch, 0, -2, mFont.YELLOW);
							SoundMn.gI().getItem();
						}
						else if (itemMap2.template.type == 10)
						{
							GameScr.startFlyText(((num >= 0) ? "+" : string.Empty) + num, Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch, 0, -2, mFont.GREEN);
							SoundMn.gI().getItem();
						}
						else if (itemMap2.template.type == 34)
						{
							GameScr.startFlyText(((num >= 0) ? "+" : string.Empty) + num, Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch, 0, -2, mFont.RED);
							SoundMn.gI().getItem();
						}
						else
						{
							GameScr.info1.addInfo(mResources.you_receive + " " + ((num <= 0) ? string.Empty : (num + " ")) + itemMap2.template.name, 0);
							SoundMn.gI().getItem();
						}
						if (num > 0 && Char.myCharz().petFollow != null && Char.myCharz().petFollow.smallID == 4683)
						{
							ServerEffect.addServerEffect(55, Char.myCharz().petFollow.cmx, Char.myCharz().petFollow.cmy, 1);
							ServerEffect.addServerEffect(55, Char.myCharz().cx, Char.myCharz().cy, 1);
						}
					}
					else if (text5.Length == 1)
					{
						Cout.LogError3("strInf.Length =1:  " + text5);
					}
					else
					{
						GameScr.info1.addInfo(text5, 0);
					}
					break;
				}
				break;
			}
			case -19:
			{
				GameCanvas.debug("SA62", 2);
				short num115 = msg.reader().readShort();
				@char = GameScr.findCharInMap(msg.reader().readInt());
				for (int num116 = 0; num116 < GameScr.vItemMap.size(); num116++)
				{
					ItemMap itemMap = (ItemMap)GameScr.vItemMap.elementAt(num116);
					if (itemMap.itemMapID != num115)
					{
						continue;
					}
					if (@char == null)
					{
						return;
					}
					itemMap.setPoint(@char.cx, @char.cy - 10);
					if (itemMap.x < @char.cx)
					{
						@char.cdir = -1;
					}
					else if (itemMap.x > @char.cx)
					{
						@char.cdir = 1;
					}
					break;
				}
				break;
			}
			case -18:
			{
				GameCanvas.debug("SA63", 2);
				int num114 = msg.reader().readByte();
				GameScr.vItemMap.addElement(new ItemMap(msg.reader().readShort(), Char.myCharz().arrItemBag[num114].template.id, Char.myCharz().cx, Char.myCharz().cy, msg.reader().readShort(), msg.reader().readShort()));
				Char.myCharz().arrItemBag[num114] = null;
				break;
			}
			case 68:
			{
				Res.outz("ADD ITEM TO MAP --------------------------------------");
				GameCanvas.debug("SA6333", 2);
				short itemMapID = msg.reader().readShort();
				short itemTemplateID = msg.reader().readShort();
				int x = msg.reader().readShort();
				int y = msg.reader().readShort();
				int num113 = msg.reader().readInt();
				short r = 0;
				if (num113 == -2)
				{
					r = msg.reader().readShort();
				}
				ItemMap o2 = new ItemMap(num113, itemMapID, itemTemplateID, x, y, r);
				GameScr.vItemMap.addElement(o2);
				break;
			}
			case 69:
				SoundMn.IsDelAcc = ((msg.reader().readByte() != 0) ? true : false);
				break;
			case -14:
				GameCanvas.debug("SA64", 2);
				@char = GameScr.findCharInMap(msg.reader().readInt());
				if (@char == null)
				{
					return;
				}
				GameScr.vItemMap.addElement(new ItemMap(msg.reader().readShort(), msg.reader().readShort(), @char.cx, @char.cy, msg.reader().readShort(), msg.reader().readShort()));
				break;
			case -22:
				GameCanvas.debug("SA65", 2);
				Char.isLockKey = true;
				Char.ischangingMap = true;
				GameScr.gI().timeStartMap = 0;
				GameScr.gI().timeLengthMap = 0;
				Char.myCharz().mobFocus = null;
				Char.myCharz().npcFocus = null;
				Char.myCharz().charFocus = null;
				Char.myCharz().itemFocus = null;
				Char.myCharz().focus.removeAllElements();
				Char.myCharz().testCharId = -9999;
				Char.myCharz().killCharId = -9999;
				GameCanvas.resetBg();
				GameScr.gI().resetButton();
				GameScr.gI().center = null;
				break;
			case -70:
			{
				Res.outz("BIG MESSAGE .......................................");
				GameCanvas.endDlg();
				int avatar2 = msg.reader().readShort();
				string chat3 = msg.reader().readUTF();
				AssemblyCSharp.Mod.GameEvents.CheckServerMaintenanceText(chat3, false);
				Npc npc6 = new Npc(-1, 0, 0, 0, 0, 0);
				npc6.avatar = avatar2;
				ChatPopup.addBigMessage(chat3, 100000, npc6);
				sbyte b34 = msg.reader().readByte();
				if (b34 == 0)
				{
					ChatPopup.serverChatPopUp.cmdMsg1 = new Command(mResources.CLOSE, ChatPopup.serverChatPopUp, 1001, null);
					ChatPopup.serverChatPopUp.cmdMsg1.x = GameCanvas.w / 2 - 35;
					ChatPopup.serverChatPopUp.cmdMsg1.y = GameCanvas.h - 35;
				}
				if (b34 == 1)
				{
					string p = msg.reader().readUTF();
					string caption2 = msg.reader().readUTF();
					ChatPopup.serverChatPopUp.cmdMsg1 = new Command(caption2, ChatPopup.serverChatPopUp, 1000, p);
					ChatPopup.serverChatPopUp.cmdMsg1.x = GameCanvas.w / 2 - 75;
					ChatPopup.serverChatPopUp.cmdMsg1.y = GameCanvas.h - 35;
					ChatPopup.serverChatPopUp.cmdMsg2 = new Command(mResources.CLOSE, ChatPopup.serverChatPopUp, 1001, null);
					ChatPopup.serverChatPopUp.cmdMsg2.x = GameCanvas.w / 2 + 11;
					ChatPopup.serverChatPopUp.cmdMsg2.y = GameCanvas.h - 35;
				}
				break;
			}
			case 38:
			{
				GameCanvas.debug("SA67", 2);
				InfoDlg.hide();
				int num103 = msg.reader().readShort();
				Res.outz("OPEN_UI_SAY ID= " + num103);
				string str = msg.reader().readUTF();
				str = Res.changeString(str);
				for (int num104 = 0; num104 < GameScr.vNpc.size(); num104++)
				{
					Npc npc4 = (Npc)GameScr.vNpc.elementAt(num104);
					Res.outz("npc id= " + npc4.template.npcTemplateId);
					if (npc4.template.npcTemplateId == num103)
					{
						ChatPopup.addChatPopupMultiLine(str, 100000, npc4);
						GameCanvas.panel.hideNow();
						return;
					}
				}
				Npc npc5 = new Npc(num103, 0, 0, 0, num103, GameScr.info1.charId[Char.myCharz().cgender][2]);
				if (npc5.template.npcTemplateId == 5)
				{
					npc5.charID = 5;
				}
				try
				{
					npc5.avatar = msg.reader().readShort();
				}
				catch (Exception)
				{
				}
				ChatPopup.addChatPopupMultiLine(str, 100000, npc5);
				GameCanvas.panel.hideNow();
				break;
			}
			case 32:
			{
				GameCanvas.debug("SA68", 2);
				int num72 = msg.reader().readShort();
				for (int num73 = 0; num73 < GameScr.vNpc.size(); num73++)
				{
					Npc npc2 = (Npc)GameScr.vNpc.elementAt(num73);
					if (npc2.template.npcTemplateId == num72 && npc2.Equals(Char.myCharz().npcFocus))
					{
						string chat = msg.reader().readUTF();
						string[] array7 = new string[msg.reader().readByte()];
						for (int num74 = 0; num74 < array7.Length; num74++)
						{
							array7[num74] = msg.reader().readUTF();
						}
						GameScr.gI().createMenu(array7, npc2);
						ChatPopup.addChatPopup(chat, 100000, npc2);
						return;
					}
				}
				Npc npc3 = new Npc(num72, 0, -100, 100, num72, GameScr.info1.charId[Char.myCharz().cgender][2]);
				Res.outz((Char.myCharz().npcFocus == null) ? "null" : "!null");
				string chat2 = msg.reader().readUTF();
				string[] array8 = new string[msg.reader().readByte()];
				for (int num75 = 0; num75 < array8.Length; num75++)
				{
					array8[num75] = msg.reader().readUTF();
				}
				try
				{
					short avatar = msg.reader().readShort();
					npc3.avatar = avatar;
				}
				catch (Exception)
				{
				}
				Res.outz((Char.myCharz().npcFocus == null) ? "null" : "!null");
				GameScr.gI().createMenu(array8, npc3);
				ChatPopup.addChatPopup(chat2, 100000, npc3);
				break;
			}
			case 7:
			{
				sbyte type3 = msg.reader().readByte();
				short id = msg.reader().readShort();
				string info = msg.reader().readUTF();
				GameCanvas.panel.saleRequest(type3, info, id);
				break;
			}
			case 6:
				GameCanvas.debug("SA70", 2);
				Char.myCharz().xu = msg.reader().readLong();
				Char.myCharz().luong = msg.reader().readInt();
				Char.myCharz().luongKhoa = msg.reader().readInt();
				Char.myCharz().xuStr = mSystem.numberTostring(Char.myCharz().xu);
				Char.myCharz().luongStr = mSystem.numberTostring(Char.myCharz().luong);
				Char.myCharz().luongKhoaStr = mSystem.numberTostring(Char.myCharz().luongKhoa);
				GameCanvas.endDlg();
				break;
			case -24:
				Char.isLoadingMap = true;
				Cout.println("GET MAP INFO");
				GameScr.gI().magicTree = null;
				GameCanvas.isLoading = true;
				GameCanvas.debug("SA75", 2);
				GameScr.resetAllvector();
				GameCanvas.endDlg();
				TileMap.vGo.removeAllElements();
				PopUp.vPopups.removeAllElements();
				mSystem.gcc();
				TileMap.mapID = msg.reader().readUnsignedByte();
				TileMap.planetID = msg.reader().readByte();
				TileMap.tileID = msg.reader().readByte();
				TileMap.bgID = msg.reader().readByte();
				Cout.println("load planet from server: " + TileMap.planetID + "bgType= " + TileMap.bgType + ".............................");
				TileMap.typeMap = msg.reader().readByte();
				TileMap.mapName = msg.reader().readUTF();
				TileMap.zoneID = msg.reader().readByte();
				GameCanvas.debug("SA75x1", 2);
				try
				{
					TileMap.loadMapFromResource(TileMap.mapID);
				}
				catch (Exception)
				{
					Service.gI().requestMaptemplate(TileMap.mapID);
					messWait = msg;
					return;
				}
				loadInfoMap(msg);
				try
				{
					TileMap.isMapDouble = ((msg.reader().readByte() != 0) ? true : false);
				}
				catch (Exception)
				{
				}
				GameScr.cmx = GameScr.cmtoX;
				GameScr.cmy = GameScr.cmtoY;
				break;
			case -31:
			{
				TileMap.vItemBg.removeAllElements();
				short num50 = msg.reader().readShort();
				Cout.LogError2("nItem= " + num50);
				for (int num51 = 0; num51 < num50; num51++)
				{
					BgItem bgItem = new BgItem();
					bgItem.id = num51;
					bgItem.idImage = msg.reader().readShort();
					bgItem.layer = msg.reader().readByte();
					bgItem.dx = msg.reader().readShort();
					bgItem.dy = msg.reader().readShort();
					sbyte b24 = msg.reader().readByte();
					bgItem.tileX = new int[b24];
					bgItem.tileY = new int[b24];
					for (int num52 = 0; num52 < b24; num52++)
					{
						bgItem.tileX[num51] = msg.reader().readByte();
						bgItem.tileY[num51] = msg.reader().readByte();
					}
					TileMap.vItemBg.addElement(bgItem);
				}
				break;
			}
			case -4:
			{
				GameCanvas.debug("SA76", 2);
				@char = GameScr.findCharInMap(msg.reader().readInt());
				if (@char == null)
				{
					return;
				}
				GameCanvas.debug("SA76v1", 2);
				if ((TileMap.tileTypeAtPixel(@char.cx, @char.cy) & 2) == 2)
				{
					@char.setSkillPaint(GameScr.sks[msg.reader().readUnsignedByte()], 0);
				}
				else
				{
					@char.setSkillPaint(GameScr.sks[msg.reader().readUnsignedByte()], 1);
				}
				GameCanvas.debug("SA76v2", 2);
				@char.attMobs = new Mob[msg.reader().readByte()];
				for (int num18 = 0; num18 < @char.attMobs.Length; num18++)
				{
					Mob mob4 = (Mob)GameScr.vMob.elementAt(msg.reader().readByte());
					@char.attMobs[num18] = mob4;
					if (num18 == 0)
					{
						if (@char.cx <= mob4.x)
						{
							@char.cdir = 1;
						}
						else
						{
							@char.cdir = -1;
						}
					}
				}
				GameCanvas.debug("SA76v3", 2);
				@char.charFocus = null;
				@char.mobFocus = @char.attMobs[0];
				Char[] array4 = new Char[10];
				num = 0;
				try
				{
					for (num = 0; num < array4.Length; num++)
					{
						int num19 = msg.reader().readInt();
						Char char4 = (array4[num] = ((num19 != Char.myCharz().charID) ? GameScr.findCharInMap(num19) : Char.myCharz()));
						if (num == 0)
						{
							if (@char.cx <= char4.cx)
							{
								@char.cdir = 1;
							}
							else
							{
								@char.cdir = -1;
							}
						}
					}
				}
				catch (Exception ex5)
				{
					Cout.println("Loi PLAYER_ATTACK_N_P " + ex5.ToString());
				}
				GameCanvas.debug("SA76v4", 2);
				if (num > 0)
				{
					@char.attChars = new Char[num];
					for (num = 0; num < @char.attChars.Length; num++)
					{
						@char.attChars[num] = array4[num];
					}
					@char.charFocus = @char.attChars[0];
					@char.mobFocus = null;
				}
				GameCanvas.debug("SA76v5", 2);
				break;
			}
			case 54:
			{
				@char = GameScr.findCharInMap(msg.reader().readInt());
				if (@char == null)
				{
					return;
				}
				int num12 = msg.reader().readUnsignedByte();
				if ((TileMap.tileTypeAtPixel(@char.cx, @char.cy) & 2) == 2)
				{
					@char.setSkillPaint(GameScr.sks[num12], 0);
				}
				else
				{
					@char.setSkillPaint(GameScr.sks[num12], 1);
				}
				Mob[] array2 = new Mob[10];
				num = 0;
				try
				{
					for (num = 0; num < array2.Length; num++)
					{
						Mob mob2 = (array2[num] = (Mob)GameScr.vMob.elementAt(msg.reader().readByte()));
						if (num == 0)
						{
							if (@char.cx <= mob2.x)
							{
								@char.cdir = 1;
							}
							else
							{
								@char.cdir = -1;
							}
						}
					}
				}
				catch (Exception ex3)
				{
					Cout.println("Loi PLAYER_ATTACK_NPC " + ex3.ToString());
				}
				if (num > 0)
				{
					@char.attMobs = new Mob[num];
					for (num = 0; num < @char.attMobs.Length; num++)
					{
						@char.attMobs[num] = array2[num];
					}
					@char.charFocus = null;
					@char.mobFocus = @char.attMobs[0];
				}
				break;
			}
			case -60:
			{
				GameCanvas.debug("SA7666", 2);
				int num2 = msg.reader().readInt();
				int num3 = -1;
				if (num2 != Char.myCharz().charID)
				{
					Char char2 = GameScr.findCharInMap(num2);
					if (char2 == null)
					{
						return;
					}
					if (char2.currentMovePoint != null)
					{
						char2.createShadow(char2.cx, char2.cy, 10);
						char2.cx = char2.currentMovePoint.xEnd;
						char2.cy = char2.currentMovePoint.yEnd;
					}
					int num4 = msg.reader().readUnsignedByte();
					Res.outz("player skill ID= " + num4);
					if ((TileMap.tileTypeAtPixel(char2.cx, char2.cy) & 2) == 2)
					{
						char2.setSkillPaint(GameScr.sks[num4], 0);
					}
					else
					{
						char2.setSkillPaint(GameScr.sks[num4], 1);
					}
					sbyte b = msg.reader().readByte();
					Res.outz("nAttack = " + b);
					Char[] array = new Char[b];
					for (num = 0; num < array.Length; num++)
					{
						num3 = msg.reader().readInt();
						Char char3;
						if (num3 == Char.myCharz().charID)
						{
							char3 = Char.myCharz();
							if (!PickMob.IsTanSat && !GameScr.isChangeZone && GameScr.isAutoPlay && GameScr.canAutoPlay)
							{
								Service.gI().requestChangeZone(-1, -1);
								GameScr.isChangeZone = true;
							}
						}
						else
						{
							char3 = GameScr.findCharInMap(num3);
						}
						array[num] = char3;
						if (num == 0)
						{
							if (char2.cx <= char3.cx)
							{
								char2.cdir = 1;
							}
							else
							{
								char2.cdir = -1;
							}
						}
					}
					if (num > 0)
					{
						char2.attChars = new Char[num];
						for (num = 0; num < char2.attChars.Length; num++)
						{
							char2.attChars[num] = array[num];
						}
						char2.mobFocus = null;
						char2.charFocus = char2.attChars[0];
					}
				}
				else
				{
					sbyte b2 = msg.reader().readByte();
					sbyte b3 = msg.reader().readByte();
					num3 = msg.reader().readInt();
				}
				try
				{
					sbyte b4 = msg.reader().readByte();
					Res.outz("isRead continue = " + b4);
					if (b4 != 1)
					{
						break;
					}
					sbyte b5 = msg.reader().readByte();
					Res.outz("type skill = " + b5);
					if (num3 == Char.myCharz().charID)
					{
						bool flag = false;
						@char = Char.myCharz();
						int num5 = msg.readInt3Byte();
						Res.outz("dame hit = " + num5);
						@char.isDie = msg.reader().readBoolean();
						if (@char.isDie)
						{
							Char.isLockKey = true;
						}
						Res.outz("isDie=" + @char.isDie + "---------------------------------------");
						int num6 = 0;
						flag = (@char.isCrit = msg.reader().readBoolean());
						@char.isMob = false;
						num5 = (@char.damHP = num5 + num6);
						if (b5 == 0)
						{
							@char.doInjure(num5, 0, flag, isMob: false);
						}
					}
					else
					{
						@char = GameScr.findCharInMap(num3);
						if (@char == null)
						{
							return;
						}
						bool flag2 = false;
						int num7 = msg.readInt3Byte();
						Res.outz("dame hit= " + num7);
						@char.isDie = msg.reader().readBoolean();
						Res.outz("isDie=" + @char.isDie + "---------------------------------------");
						int num8 = 0;
						flag2 = (@char.isCrit = msg.reader().readBoolean());
						@char.isMob = false;
						num7 = (@char.damHP = num7 + num8);
						if (b5 == 0)
						{
							@char.doInjure(num7, 0, flag2, isMob: false);
						}
					}
				}
				catch (Exception)
				{
				}
				break;
			}
			}
			switch (msg.command)
			{
			case -2:
			{
				GameCanvas.debug("SA77", 22);
				int num180 = msg.reader().readInt();
				Char char18 = Char.myCharz();
				Char char9 = char18;
				char9.yen += num180;
				GameScr.startFlyText((num180 <= 0) ? (string.Empty + num180) : ("+" + num180), Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch - 10, 0, -2, mFont.YELLOW);
				break;
			}
			case 95:
			{
				GameCanvas.debug("SA77", 22);
				int num197 = msg.reader().readInt();
				Char char25 = Char.myCharz();
				Char char9 = char25;
				char9.xu += num197;
				Char.myCharz().xuStr = mSystem.numberTostring(Char.myCharz().xu);
				GameScr.startFlyText((num197 <= 0) ? (string.Empty + num197) : ("+" + num197), Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch - 10, 0, -2, mFont.YELLOW);
				break;
			}
			case 96:
				GameCanvas.debug("SA77a", 22);
				Char.myCharz().taskOrders.addElement(new TaskOrder(msg.reader().readByte(), msg.reader().readShort(), msg.reader().readShort(), msg.reader().readUTF(), msg.reader().readUTF(), msg.reader().readByte(), msg.reader().readByte()));
				break;
			case 97:
			{
				sbyte b65 = msg.reader().readByte();
				for (int num181 = 0; num181 < Char.myCharz().taskOrders.size(); num181++)
				{
					TaskOrder taskOrder = (TaskOrder)Char.myCharz().taskOrders.elementAt(num181);
					if (taskOrder.taskId == b65)
					{
						taskOrder.count = msg.reader().readShort();
						break;
					}
				}
				break;
			}
			case -1:
			{
				GameCanvas.debug("SA77", 222);
				int num185 = msg.reader().readInt();
				Char char20 = Char.myCharz();
				Char char9 = char20;
				char9.xu += num185;
				Char.myCharz().xuStr = mSystem.numberTostring(Char.myCharz().xu);
				char20 = Char.myCharz();
				char9 = char20;
				char9.yen -= num185;
				GameScr.startFlyText("+" + num185, Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch - 10, 0, -2, mFont.YELLOW);
				break;
			}
			case -3:
			{
				GameCanvas.debug("SA78", 2);
				sbyte b70 = msg.reader().readByte();
				int num196 = msg.reader().readInt();
				if (b70 == 0)
				{
					Char char22 = Char.myCharz();
					Char char9 = char22;
					char9.cPower += num196;
				}
				if (b70 == 1)
				{
					Char char23 = Char.myCharz();
					Char char9 = char23;
					char9.cTiemNang += num196;
				}
				if (b70 == 2)
				{
					Char char24 = Char.myCharz();
					Char char9 = char24;
					char9.cPower += num196;
					char24 = Char.myCharz();
					char9 = char24;
					char9.cTiemNang += num196;
				}
				Char.myCharz().applyCharLevelPercent();
				if (Char.myCharz().cTypePk != 3)
				{
					GameScr.startFlyText(((num196 <= 0) ? string.Empty : "+") + num196, Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch, 0, -4, mFont.GREEN);
					if (num196 > 0 && Char.myCharz().petFollow != null && Char.myCharz().petFollow.smallID == 5002)
					{
						ServerEffect.addServerEffect(55, Char.myCharz().petFollow.cmx, Char.myCharz().petFollow.cmy, 1);
						ServerEffect.addServerEffect(55, Char.myCharz().cx, Char.myCharz().cy, 1);
					}
				}
				break;
			}
			case -73:
			{
				sbyte b68 = msg.reader().readByte();
				for (int num191 = 0; num191 < GameScr.vNpc.size(); num191++)
				{
					Npc npc7 = (Npc)GameScr.vNpc.elementAt(num191);
					if (npc7.template.npcTemplateId == b68)
					{
						if (msg.reader().readByte() == 0)
						{
							npc7.isHide = true;
						}
						else
						{
							npc7.isHide = false;
						}
						break;
					}
				}
				break;
			}
			case -5:
			{
				GameCanvas.debug("SA79", 2);
				int charID = msg.reader().readInt();
				int num187 = msg.reader().readInt();
				Char char21;
				if (num187 != -100)
				{
					char21 = new Char();
					char21.charID = charID;
					char21.clanID = num187;
				}
				else
				{
					char21 = new Mabu();
					char21.charID = charID;
					char21.clanID = num187;
				}
				if (char21.clanID == -2)
				{
					char21.isCopy = true;
				}
				if (readCharInfo(char21, msg))
				{
					sbyte b67 = msg.reader().readByte();
					if (char21.cy <= 10 && b67 != 0 && b67 != 2)
					{
						Res.outz("nhân vật bay trên trời xuống x= " + char21.cx + " y= " + char21.cy);
						Teleport teleport2 = new Teleport(char21.cx, char21.cy, char21.head, char21.cdir, 1, isMe: false, (b67 != 1) ? b67 : char21.cgender);
						teleport2.id = char21.charID;
						char21.isTeleport = true;
						Teleport.addTeleport(teleport2);
					}
					if (b67 == 2)
					{
						char21.show();
					}
					for (int num188 = 0; num188 < GameScr.vMob.size(); num188++)
					{
						Mob mob19 = (Mob)GameScr.vMob.elementAt(num188);
						if (mob19 != null && mob19.isMobMe && mob19.mobId == char21.charID)
						{
							Res.outz("co 1 con quai");
							char21.mobMe = mob19;
							char21.mobMe.x = char21.cx;
							char21.mobMe.y = char21.cy - 40;
							break;
						}
					}
					if (GameScr.findCharInMap(char21.charID) == null)
					{
						GameScr.vCharInMap.addElement(char21);
					}
					char21.isMonkey = msg.reader().readByte();
					short num189 = msg.reader().readShort();
					Res.outz("mount id= " + num189 + "+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++");
					if (num189 != -1)
					{
						char21.isHaveMount = true;
						switch (num189)
						{
						case 346:
						case 347:
						case 348:
							char21.isMountVip = false;
							break;
						case 349:
						case 350:
						case 351:
							char21.isMountVip = true;
							break;
						case 396:
							char21.isEventMount = true;
							break;
						case 532:
							char21.isSpeacialMount = true;
							break;
						default:
							if (num189 >= Char.ID_NEW_MOUNT)
							{
								char21.idMount = num189;
							}
							break;
						}
					}
					else
					{
						char21.isHaveMount = false;
					}
				}
				sbyte cFlag = msg.reader().readByte();
				Res.outz("addplayer:   " + cFlag);
				char21.cFlag = cFlag;
				char21.isNhapThe = msg.reader().readByte() == 1;
				try
				{
					char21.idAuraEff = msg.reader().readShort();
					char21.idEff_Set_Item = msg.reader().readSByte();
					char21.idHat = msg.reader().readShort();
					if (char21.bag >= 201 && char21.bag < 2000)
					{
						Effect effect2 = new Effect(char21.bag, char21, 2, -1, 10, 1);
						effect2.typeEff = 5;
						char21.addEffChar(effect2);
					}
					else
					{
						for (int num190 = 0; num190 < 54; num190++)
						{
							char21.removeEffChar(0, 201 + num190);
						}
					}
				}
				catch (Exception ex37)
				{
					Res.outz("cmd: -5 err: " + ex37.StackTrace);
				}
				GameScr.gI().getFlagImage(char21.charID, char21.cFlag);
				Res.outz("Cmd: -5 PLAYER_ADD: cID| cName| cFlag| cBag|    " + @char.charID + " | " + @char.cName + " | " + @char.cFlag + " | " + @char.bag);
				break;
			}
			case -7:
			{
				GameCanvas.debug("SA80", 2);
				int num182 = msg.reader().readInt();
				Cout.println("RECEVED MOVE OF " + num182);
				for (int num183 = 0; num183 < GameScr.vCharInMap.size(); num183++)
				{
					Char char19 = null;
					try
					{
						char19 = (Char)GameScr.vCharInMap.elementAt(num183);
					}
					catch (Exception ex29)
					{
						Cout.println("Loi PLAYER_MOVE " + ex29.ToString());
					}
					if (char19 == null)
					{
						break;
					}
					if (char19.charID == num182)
					{
						GameCanvas.debug("SA8x2y" + num183, 2);
						char19.moveTo(msg.reader().readShort(), msg.reader().readShort(), 0);
						char19.lastUpdateTime = mSystem.currentTimeMillis();
						break;
					}
				}
				GameCanvas.debug("SA80x3", 2);
				break;
			}
			case -6:
			{
				GameCanvas.debug("SA81", 2);
				int num178 = msg.reader().readInt();
				for (int num179 = 0; num179 < GameScr.vCharInMap.size(); num179++)
				{
					Char char17 = (Char)GameScr.vCharInMap.elementAt(num179);
					if (char17 != null && char17.charID == num178)
					{
						if (!char17.isInvisiblez && !char17.isUsePlane)
						{
							ServerEffect.addServerEffect(60, char17.cx, char17.cy, 1);
						}
						if (!char17.isUsePlane)
						{
							GameScr.vCharInMap.removeElementAt(num179);
						}
						return;
					}
				}
				break;
			}
			case -13:
			{
				GameCanvas.debug("SA82", 2);
				int num192 = msg.reader().readUnsignedByte();
				if (num192 > GameScr.vMob.size() - 1 || num192 < 0)
				{
					return;
				}
				Mob mob20 = (Mob)GameScr.vMob.elementAt(num192);
				mob20.sys = msg.reader().readByte();
				mob20.levelBoss = msg.reader().readByte();
				if (mob20.levelBoss != 0)
				{
					mob20.typeSuperEff = Res.random(0, 3);
				}
				mob20.x = mob20.xFirst;
				mob20.y = mob20.yFirst;
				mob20.status = 5;
				mob20.injureThenDie = false;
				mob20.hp = msg.reader().readInt();
				mob20.maxHp = mob20.hp;
				mob20.updateHp_bar();
				ServerEffect.addServerEffect(60, mob20.x, mob20.y, 1);
				break;
			}
			case -75:
			{
				Mob mob17 = null;
				try
				{
					mob17 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				}
				catch (Exception)
				{
				}
				if (mob17 != null)
				{
					mob17.levelBoss = msg.reader().readByte();
					if (mob17.levelBoss > 0)
					{
						mob17.typeSuperEff = Res.random(0, 3);
					}
				}
				break;
			}
			case -9:
			{
				GameCanvas.debug("SA83", 2);
				Mob mob16 = null;
				try
				{
					mob16 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				}
				catch (Exception)
				{
				}
				GameCanvas.debug("SA83v1", 2);
				if (mob16 != null)
				{
					mob16.hp = msg.readInt3Byte();
					mob16.updateHp_bar();
					int num184 = msg.readInt3Byte();
					if (num184 == 1)
					{
						return;
					}
					if (num184 > 1)
					{
						mob16.setInjure();
					}
					bool flag9 = false;
					try
					{
						flag9 = msg.reader().readBoolean();
					}
					catch (Exception)
					{
					}
					sbyte b66 = msg.reader().readByte();
					if (b66 != -1)
					{
						EffecMn.addEff(new Effect(b66, mob16.x, mob16.getY(), 3, 1, -1));
					}
					GameCanvas.debug("SA83v2", 2);
					if (flag9)
					{
						GameScr.startFlyText("-" + num184, mob16.x, mob16.getY() - mob16.getH(), 0, -2, mFont.FATAL);
					}
					else if (num184 == 0)
					{
						mob16.x = mob16.xFirst;
						mob16.y = mob16.yFirst;
						GameScr.startFlyText(mResources.miss, mob16.x, mob16.getY() - mob16.getH(), 0, -2, mFont.MISS);
					}
					else if (num184 > 1)
					{
						GameScr.startFlyText("-" + num184, mob16.x, mob16.getY() - mob16.getH(), 0, -2, mFont.ORANGE);
					}
				}
				GameCanvas.debug("SA83v3", 2);
				break;
			}
			case 45:
			{
				GameCanvas.debug("SA84", 2);
				Mob mob14 = null;
				try
				{
					mob14 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				}
				catch (Exception ex28)
				{
					Cout.println("Loi tai NPC_MISS  " + ex28.ToString());
				}
				if (mob14 != null)
				{
					mob14.hp = msg.reader().readInt();
					mob14.updateHp_bar();
					GameScr.startFlyText(mResources.miss, mob14.x, mob14.y - mob14.h, 0, -2, mFont.MISS);
				}
				break;
			}
			case -12:
			{
				Res.outz("SERVER SEND MOB DIE");
				GameCanvas.debug("SA85", 2);
				Mob mob21 = null;
				try
				{
					mob21 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				}
				catch (Exception)
				{
					Cout.println("LOi tai NPC_DIE cmd " + msg.command);
				}
				if (mob21 == null || mob21.status == 0 || mob21.status == 0)
				{
					break;
				}
				mob21.startDie();
				try
				{
					int num193 = msg.readInt3Byte();
					if (msg.reader().readBool())
					{
						GameScr.startFlyText("-" + num193, mob21.x, mob21.y - mob21.h, 0, -2, mFont.FATAL);
					}
					else
					{
						GameScr.startFlyText("-" + num193, mob21.x, mob21.y - mob21.h, 0, -2, mFont.ORANGE);
					}
					sbyte b69 = msg.reader().readByte();
					for (int num194 = 0; num194 < b69; num194++)
					{
						ItemMap itemMap4 = new ItemMap(msg.reader().readShort(), msg.reader().readShort(), mob21.x, mob21.y, msg.reader().readShort(), msg.reader().readShort());
						int num195 = (itemMap4.playerId = msg.reader().readInt());
						Res.outz("playerid= " + num195 + " my id= " + Char.myCharz().charID);
						GameScr.vItemMap.addElement(itemMap4);
						if (Res.abs(itemMap4.y - Char.myCharz().cy) < 24 && Res.abs(itemMap4.x - Char.myCharz().cx) < 24)
						{
							Char.myCharz().charFocus = null;
						}
					}
				}
				catch (Exception ex39)
				{
					Cout.println("LOi tai NPC_DIE " + ex39.ToString() + " cmd " + msg.command);
				}
				break;
			}
			case 74:
			{
				GameCanvas.debug("SA85", 2);
				Mob mob15 = null;
				try
				{
					mob15 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				}
				catch (Exception)
				{
					Cout.println("Loi tai NPC CHANGE " + msg.command);
				}
				if (mob15 != null && mob15.status != 0 && mob15.status != 0)
				{
					mob15.status = 0;
					ServerEffect.addServerEffect(60, mob15.x, mob15.y, 1);
					ItemMap itemMap3 = new ItemMap(msg.reader().readShort(), msg.reader().readShort(), mob15.x, mob15.y, msg.reader().readShort(), msg.reader().readShort());
					GameScr.vItemMap.addElement(itemMap3);
					if (Res.abs(itemMap3.y - Char.myCharz().cy) < 24 && Res.abs(itemMap3.x - Char.myCharz().cx) < 24)
					{
						Char.myCharz().charFocus = null;
					}
				}
				break;
			}
			case -11:
			{
				GameCanvas.debug("SA86", 2);
				Mob mob13 = null;
				try
				{
					int index5 = msg.reader().readUnsignedByte();
					mob13 = (Mob)GameScr.vMob.elementAt(index5);
				}
				catch (Exception ex26)
				{
					Res.outz("Loi tai NPC_ATTACK_ME " + msg.command + " err= " + ex26.StackTrace);
				}
				if (mob13 != null)
				{
					Char.myCharz().isDie = false;
					Char.isLockKey = false;
					int num175 = msg.readInt3Byte();
					int num176;
					try
					{
						num176 = msg.readInt3Byte();
					}
					catch (Exception)
					{
						num176 = 0;
					}
					if (mob13.isBusyAttackSomeOne)
					{
						Char.myCharz().doInjure(num175, num176, isCrit: false, isMob: true);
						break;
					}
					mob13.dame = num175;
					mob13.dameMp = num176;
					mob13.setAttack(Char.myCharz());
				}
				break;
			}
			case -10:
			{
				GameCanvas.debug("SA87", 2);
				Mob mob18 = null;
				try
				{
					mob18 = (Mob)GameScr.vMob.elementAt(msg.reader().readUnsignedByte());
				}
				catch (Exception)
				{
				}
				GameCanvas.debug("SA87x1", 2);
				if (mob18 != null)
				{
					GameCanvas.debug("SA87x2", 2);
					@char = GameScr.findCharInMap(msg.reader().readInt());
					if (@char == null)
					{
						return;
					}
					GameCanvas.debug("SA87x3", 2);
					int num186 = msg.readInt3Byte();
					mob18.dame = @char.cHP - num186;
					@char.cHPNew = num186;
					GameCanvas.debug("SA87x4", 2);
					try
					{
						@char.cMP = msg.readInt3Byte();
					}
					catch (Exception)
					{
					}
					GameCanvas.debug("SA87x5", 2);
					if (mob18.isBusyAttackSomeOne)
					{
						@char.doInjure(mob18.dame, 0, isCrit: false, isMob: true);
					}
					else
					{
						mob18.setAttack(@char);
					}
					GameCanvas.debug("SA87x6", 2);
				}
				break;
			}
			case -17:
				GameCanvas.debug("SA88", 2);
				Char.myCharz().meDead = true;
				Char.myCharz().cPk = msg.reader().readByte();
				Char.myCharz().startDie(msg.reader().readShort(), msg.reader().readShort());
				try
				{
					Char.myCharz().cPower = msg.reader().readLong();
					Char.myCharz().applyCharLevelPercent();
				}
				catch (Exception)
				{
					Cout.println("Loi tai ME_DIE " + msg.command);
				}
				Char.myCharz().countKill = 0;
				break;
			case 66:
				Res.outz("ME DIE XP DOWN NOT IMPLEMENT YET!!!!!!!!!!!!!!!!!!!!!!!!!!");
				break;
			case -8:
				GameCanvas.debug("SA89", 2);
				@char = GameScr.findCharInMap(msg.reader().readInt());
				if (@char == null)
				{
					return;
				}
				@char.cPk = msg.reader().readByte();
				@char.waitToDie(msg.reader().readShort(), msg.reader().readShort());
				break;
			case -16:
				GameCanvas.debug("SA90", 2);
				if (Char.myCharz().wdx != 0 || Char.myCharz().wdy != 0)
				{
					Char.myCharz().cx = Char.myCharz().wdx;
					Char.myCharz().cy = Char.myCharz().wdy;
					Char.myCharz().wdx = (Char.myCharz().wdy = 0);
				}
				Char.myCharz().liveFromDead();
				Char.myCharz().isLockMove = false;
				Char.myCharz().meDead = false;
				break;
			case 44:
			{
				GameCanvas.debug("SA91", 2);
				int num177 = msg.reader().readInt();
				string text8 = msg.reader().readUTF();
				Res.outz("user id= " + num177 + " text= " + text8);
				@char = ((Char.myCharz().charID != num177) ? GameScr.findCharInMap(num177) : Char.myCharz());
				if (@char == null)
				{
					return;
				}
				@char.addInfo(text8);
				break;
			}
			case 18:
			{
				sbyte b64 = msg.reader().readByte();
				for (int num174 = 0; num174 < b64; num174++)
				{
					int charId = msg.reader().readInt();
					int cx = msg.reader().readShort();
					int cy = msg.reader().readShort();
					int cHPShow = msg.readInt3Byte();
					Char char16 = GameScr.findCharInMap(charId);
					if (char16 != null)
					{
						char16.cx = cx;
						char16.cy = cy;
						char16.cHP = (char16.cHPShow = cHPShow);
						char16.lastUpdateTime = mSystem.currentTimeMillis();
					}
				}
				break;
			}
			case 19:
				Char.myCharz().countKill = msg.reader().readUnsignedShort();
				Char.myCharz().countKillMax = msg.reader().readUnsignedShort();
				break;
			}
			GameCanvas.debug("SA92", 2);
		}
		catch (Exception ex40)
		{
			Res.outz("Controller = " + ex40.StackTrace);
		}
		finally
		{
			msg?.cleanup();
		}
	}

	private void createItem(myReader d)
	{
		GameScr.vcItem = d.readByte();
		ItemTemplates.itemTemplates.clear();
		GameScr.gI().iOptionTemplates = new ItemOptionTemplate[d.readUnsignedByte()];
		for (int i = 0; i < GameScr.gI().iOptionTemplates.Length; i++)
		{
			GameScr.gI().iOptionTemplates[i] = new ItemOptionTemplate();
			GameScr.gI().iOptionTemplates[i].id = i;
			GameScr.gI().iOptionTemplates[i].name = d.readUTF();
			GameScr.gI().iOptionTemplates[i].type = d.readByte();
		}
		int num = d.readShort();
		for (int j = 0; j < num; j++)
		{
			ItemTemplate it = new ItemTemplate((short)j, d.readByte(), d.readByte(), d.readUTF(), d.readUTF(), d.readByte(), d.readInt(), d.readShort(), d.readShort(), d.readBool());
			ItemTemplates.add(it);
		}
	}

	private void createSkill(myReader d)
	{
		GameScr.vcSkill = d.readByte();
		GameScr.gI().sOptionTemplates = new SkillOptionTemplate[d.readByte()];
		for (int i = 0; i < GameScr.gI().sOptionTemplates.Length; i++)
		{
			GameScr.gI().sOptionTemplates[i] = new SkillOptionTemplate();
			GameScr.gI().sOptionTemplates[i].id = i;
			GameScr.gI().sOptionTemplates[i].name = d.readUTF();
		}
		GameScr.nClasss = new NClass[d.readByte()];
		for (int j = 0; j < GameScr.nClasss.Length; j++)
		{
			GameScr.nClasss[j] = new NClass();
			GameScr.nClasss[j].classId = j;
			GameScr.nClasss[j].name = d.readUTF();
			GameScr.nClasss[j].skillTemplates = new SkillTemplate[d.readByte()];
			for (int k = 0; k < GameScr.nClasss[j].skillTemplates.Length; k++)
			{
				GameScr.nClasss[j].skillTemplates[k] = new SkillTemplate();
				GameScr.nClasss[j].skillTemplates[k].id = d.readByte();
				GameScr.nClasss[j].skillTemplates[k].name = d.readUTF();
				GameScr.nClasss[j].skillTemplates[k].maxPoint = d.readByte();
				GameScr.nClasss[j].skillTemplates[k].manaUseType = d.readByte();
				GameScr.nClasss[j].skillTemplates[k].type = d.readByte();
				GameScr.nClasss[j].skillTemplates[k].iconId = d.readShort();
				GameScr.nClasss[j].skillTemplates[k].damInfo = d.readUTF();
				int lineWidth = 130;
				if (GameCanvas.w == 128 || GameCanvas.h <= 208)
				{
					lineWidth = 100;
				}
				GameScr.nClasss[j].skillTemplates[k].description = mFont.tahoma_7_green2.splitFontArray(d.readUTF(), lineWidth);
				GameScr.nClasss[j].skillTemplates[k].skills = new Skill[d.readByte()];
				for (int l = 0; l < GameScr.nClasss[j].skillTemplates[k].skills.Length; l++)
				{
					GameScr.nClasss[j].skillTemplates[k].skills[l] = new Skill();
					GameScr.nClasss[j].skillTemplates[k].skills[l].skillId = d.readShort();
					GameScr.nClasss[j].skillTemplates[k].skills[l].template = GameScr.nClasss[j].skillTemplates[k];
					GameScr.nClasss[j].skillTemplates[k].skills[l].point = d.readByte();
					GameScr.nClasss[j].skillTemplates[k].skills[l].powRequire = d.readLong();
					GameScr.nClasss[j].skillTemplates[k].skills[l].manaUse = d.readShort();
					GameScr.nClasss[j].skillTemplates[k].skills[l].coolDown = d.readInt();
					GameScr.nClasss[j].skillTemplates[k].skills[l].dx = d.readShort();
					GameScr.nClasss[j].skillTemplates[k].skills[l].dy = d.readShort();
					GameScr.nClasss[j].skillTemplates[k].skills[l].maxFight = d.readByte();
					GameScr.nClasss[j].skillTemplates[k].skills[l].damage = d.readShort();
					GameScr.nClasss[j].skillTemplates[k].skills[l].price = d.readShort();
					GameScr.nClasss[j].skillTemplates[k].skills[l].moreInfo = d.readUTF();
					Skills.add(GameScr.nClasss[j].skillTemplates[k].skills[l]);
				}
			}
		}
	}

	private void createMap(myReader d)
	{
		GameScr.vcMap = d.readByte();
		TileMap.mapNames = new string[d.readUnsignedByte()];
		for (int i = 0; i < TileMap.mapNames.Length; i++)
		{
			TileMap.mapNames[i] = d.readUTF();
		}
		Npc.arrNpcTemplate = new NpcTemplate[d.readByte()];
		for (sbyte b = 0; b < Npc.arrNpcTemplate.Length; b++)
		{
			Npc.arrNpcTemplate[b] = new NpcTemplate();
			Npc.arrNpcTemplate[b].npcTemplateId = b;
			Npc.arrNpcTemplate[b].name = d.readUTF();
			Npc.arrNpcTemplate[b].headId = d.readShort();
			Npc.arrNpcTemplate[b].bodyId = d.readShort();
			Npc.arrNpcTemplate[b].legId = d.readShort();
			Npc.arrNpcTemplate[b].menu = new string[d.readByte()][];
			for (int j = 0; j < Npc.arrNpcTemplate[b].menu.Length; j++)
			{
				Npc.arrNpcTemplate[b].menu[j] = new string[d.readByte()];
				for (int k = 0; k < Npc.arrNpcTemplate[b].menu[j].Length; k++)
				{
					Npc.arrNpcTemplate[b].menu[j][k] = d.readUTF();
				}
			}
		}
		Mob.arrMobTemplate = new MobTemplate[d.readByte()];
		for (sbyte b2 = 0; b2 < Mob.arrMobTemplate.Length; b2++)
		{
			Mob.arrMobTemplate[b2] = new MobTemplate();
			Mob.arrMobTemplate[b2].mobTemplateId = b2;
			Mob.arrMobTemplate[b2].type = d.readByte();
			Mob.arrMobTemplate[b2].name = d.readUTF();
			Mob.arrMobTemplate[b2].hp = d.readInt();
			Mob.arrMobTemplate[b2].rangeMove = d.readByte();
			Mob.arrMobTemplate[b2].speed = d.readByte();
			Mob.arrMobTemplate[b2].dartType = d.readByte();
		}
	}

	private void createData(myReader d, bool isSaveRMS)
	{
		GameScr.vcData = d.readByte();
		if (isSaveRMS)
		{
			Rms.saveRMS("NR_dart", NinjaUtil.readByteArray(d));
			Rms.saveRMS("NR_arrow", NinjaUtil.readByteArray(d));
			Rms.saveRMS("NR_effect", NinjaUtil.readByteArray(d));
			Rms.saveRMS("NR_image", NinjaUtil.readByteArray(d));
			Rms.saveRMS("NR_part", NinjaUtil.readByteArray(d));
			Rms.saveRMS("NR_skill", NinjaUtil.readByteArray(d));
			Rms.DeleteStorage("NRdata");
		}
	}

	private Image createImage(sbyte[] arr)
	{
		try
		{
			return Image.createImage(arr, 0, arr.Length);
		}
		catch (Exception)
		{
		}
		return null;
	}

	public int[] arrayByte2Int(sbyte[] b)
	{
		int[] array = new int[b.Length];
		for (int i = 0; i < b.Length; i++)
		{
			int num = b[i];
			if (num < 0)
			{
				num += 256;
			}
			array[i] = num;
		}
		return array;
	}

	public void readClanMsg(Message msg, int index)
	{
		try
		{
			ClanMessage clanMessage = new ClanMessage();
			sbyte b = (sbyte)(clanMessage.type = msg.reader().readByte());
			clanMessage.id = msg.reader().readInt();
			clanMessage.playerId = msg.reader().readInt();
			clanMessage.playerName = msg.reader().readUTF();
			clanMessage.role = msg.reader().readByte();
			clanMessage.time = msg.reader().readInt() + 1000000000;
			bool flag = false;
			GameScr.isNewClanMessage = false;
			switch (b)
			{
			case 0:
			{
				string text = msg.reader().readUTF();
				GameScr.isNewClanMessage = true;
				if (mFont.tahoma_7.getWidth(text) > Panel.WIDTH_PANEL - 60)
				{
					clanMessage.chat = mFont.tahoma_7.splitFontArray(text, Panel.WIDTH_PANEL - 10);
				}
				else
				{
					clanMessage.chat = new string[1];
					clanMessage.chat[0] = text;
				}
				clanMessage.color = msg.reader().readByte();
				break;
			}
			case 1:
				clanMessage.recieve = msg.reader().readByte();
				clanMessage.maxCap = msg.reader().readByte();
				flag = msg.reader().readByte() == 1;
				if (flag)
				{
					GameScr.isNewClanMessage = true;
				}
				if (clanMessage.playerId != Char.myCharz().charID)
				{
					if (clanMessage.recieve < clanMessage.maxCap)
					{
						clanMessage.option = new string[1] { mResources.donate };
					}
					else
					{
						clanMessage.option = null;
					}
				}
				if (GameCanvas.panel.cp != null)
				{
					GameCanvas.panel.updateRequest(clanMessage.recieve, clanMessage.maxCap);
				}
				break;
			case 2:
				if (Char.myCharz().role == 0)
				{
					GameScr.isNewClanMessage = true;
					clanMessage.option = new string[2]
					{
						mResources.CANCEL,
						mResources.receive
					};
				}
				break;
			}
			if (GameCanvas.currentScreen != GameScr.instance)
			{
				GameScr.isNewClanMessage = false;
			}
			else if (GameCanvas.panel.isShow && GameCanvas.panel.type == 0 && GameCanvas.panel.currentTabIndex == 3)
			{
				GameScr.isNewClanMessage = false;
			}
			ClanMessage.addMessage(clanMessage, index, flag);
		}
		catch (Exception)
		{
			Cout.println("LOI TAI CMD -= " + msg.command);
		}
	}

	public void loadCurrMap(sbyte teleport3)
	{
		Res.outz("is loading map = " + Char.isLoadingMap);
		GameScr.gI().auto = 0;
		GameScr.isChangeZone = false;
		CreateCharScr.instance = null;
		GameScr.info1.isUpdate = false;
		GameScr.info2.isUpdate = false;
		GameScr.lockTick = 0;
		GameCanvas.panel.isShow = false;
		SoundMn.gI().stopAll();
		if (!GameScr.isLoadAllData && !CreateCharScr.isCreateChar)
		{
			GameScr.gI().initSelectChar();
		}
		GameScr.loadCamera(fullmScreen: false, (teleport3 != 1) ? (-1) : Char.myCharz().cx, (teleport3 == 0) ? (-1) : 0);
		TileMap.loadMainTile();
		TileMap.loadMap(TileMap.tileID);
		Res.outz("LOAD GAMESCR 2");
		Char.myCharz().cvx = 0;
		Char.myCharz().statusMe = 4;
		Char.myCharz().currentMovePoint = null;
		Char.myCharz().mobFocus = null;
		Char.myCharz().charFocus = null;
		Char.myCharz().npcFocus = null;
		Char.myCharz().itemFocus = null;
		Char.myCharz().skillPaint = null;
		Char.myCharz().setMabuHold(m: false);
		Char.myCharz().skillPaintRandomPaint = null;
		GameCanvas.clearAllPointerEvent();
		if (Char.myCharz().cy >= TileMap.pxh - 100)
		{
			Char.myCharz().isFlyUp = true;
			Char.myCharz().cx += Res.abs(Res.random(0, 80));
			Service.gI().charMove();
		}
		GameScr.gI().loadGameScr();
		GameCanvas.loadBG(TileMap.bgID);
		Char.isLockKey = false;
		Res.outz("cy= " + Char.myCharz().cy + "---------------------------------------------");
		for (int i = 0; i < Char.myCharz().vEff.size(); i++)
		{
			EffectChar effectChar = (EffectChar)Char.myCharz().vEff.elementAt(i);
			if (effectChar.template.type == 10)
			{
				Char.isLockKey = true;
				break;
			}
		}
		GameCanvas.clearKeyHold();
		GameCanvas.clearKeyPressed();
		GameScr.gI().dHP = Char.myCharz().cHP;
		GameScr.gI().dMP = Char.myCharz().cMP;
		Char.ischangingMap = false;
		GameScr.gI().switchToMe();
		if (Char.myCharz().cy <= 10 && teleport3 != 0 && teleport3 != 2)
		{
			Teleport p = new Teleport(Char.myCharz().cx, Char.myCharz().cy, Char.myCharz().head, Char.myCharz().cdir, 1, isMe: true, (teleport3 != 1) ? teleport3 : Char.myCharz().cgender);
			Teleport.addTeleport(p);
			Char.myCharz().isTeleport = true;
		}
		if (teleport3 == 2)
		{
			Char.myCharz().show();
		}
		if (GameScr.gI().isRongThanXuatHien)
		{
			if (TileMap.mapID == GameScr.gI().mapRID && TileMap.zoneID == GameScr.gI().zoneRID)
			{
				GameScr.gI().callRongThan(GameScr.gI().xR, GameScr.gI().yR);
			}
			if (mGraphics.zoomLevel > 1)
			{
				GameScr.gI().doiMauTroi();
			}
		}
		InfoDlg.hide();
		InfoDlg.show(TileMap.mapName, mResources.zone + " " + TileMap.zoneID, 30);
		GameCanvas.endDlg();
		GameCanvas.isLoading = false;
		Hint.clickMob();
		Hint.clickNpc();
		GameCanvas.debug("SA75x9", 2);
	}

	public void loadInfoMap(Message msg)
	{
		try
		{
			if (mGraphics.zoomLevel == 1)
			{
				SmallImage.clearHastable();
			}
			Char.myCharz().cx = (Char.myCharz().cxSend = (Char.myCharz().cxFocus = msg.reader().readShort()));
			Char.myCharz().cy = (Char.myCharz().cySend = (Char.myCharz().cyFocus = msg.reader().readShort()));
			Char.myCharz().xSd = Char.myCharz().cx;
			Char.myCharz().ySd = Char.myCharz().cy;
			Res.outz("head= " + Char.myCharz().head + " body= " + Char.myCharz().body + " left= " + Char.myCharz().leg + " x= " + Char.myCharz().cx + " y= " + Char.myCharz().cy + " chung toc= " + Char.myCharz().cgender);
			if (Char.myCharz().cx >= 0 && Char.myCharz().cx <= 100)
			{
				Char.myCharz().cdir = 1;
			}
			else if (Char.myCharz().cx >= TileMap.tmw - 100 && Char.myCharz().cx <= TileMap.tmw)
			{
				Char.myCharz().cdir = -1;
			}
			GameCanvas.debug("SA75x4", 2);
			int num = msg.reader().readByte();
			Res.outz("vGo size= " + num);
			if (!GameScr.info1.isDone)
			{
				GameScr.info1.cmx = Char.myCharz().cx - GameScr.cmx;
				GameScr.info1.cmy = Char.myCharz().cy - GameScr.cmy;
			}
			for (int i = 0; i < num; i++)
			{
				Waypoint waypoint = new Waypoint(msg.reader().readShort(), msg.reader().readShort(), msg.reader().readShort(), msg.reader().readShort(), msg.reader().readBoolean(), msg.reader().readBoolean(), msg.reader().readUTF());
				if ((TileMap.mapID != 21 && TileMap.mapID != 22 && TileMap.mapID != 23) || waypoint.minX < 0 || waypoint.minX <= 24)
				{
				}
			}
			Resources.UnloadUnusedAssets();
			GC.Collect();
			GameCanvas.debug("SA75x5", 2);
			num = msg.reader().readByte();
			Mob.newMob.removeAllElements();
			for (sbyte b = 0; b < num; b++)
			{
				Mob mob = new Mob(b, msg.reader().readBoolean(), msg.reader().readBoolean(), msg.reader().readBoolean(), msg.reader().readBoolean(), msg.reader().readBoolean(), msg.reader().readByte(), msg.reader().readByte(), msg.reader().readInt(), msg.reader().readByte(), msg.reader().readInt(), msg.reader().readShort(), msg.reader().readShort(), msg.reader().readByte(), msg.reader().readByte());
				mob.xSd = mob.x;
				mob.ySd = mob.y;
				mob.isBoss = msg.reader().readBoolean();
				if (Mob.arrMobTemplate[mob.templateId].type != 0)
				{
					if (b % 3 == 0)
					{
						mob.dir = -1;
					}
					else
					{
						mob.dir = 1;
					}
					mob.x += 10 - b % 20;
				}
				mob.isMobMe = false;
				BigBoss bigBoss = null;
				BachTuoc bachTuoc = null;
				BigBoss2 bigBoss2 = null;
				NewBoss newBoss = null;
				if (mob.templateId == 70)
				{
					bigBoss = new BigBoss(b, (short)mob.x, (short)mob.y, 70, mob.hp, mob.maxHp, mob.sys);
				}
				if (mob.templateId == 71)
				{
					bachTuoc = new BachTuoc(b, (short)mob.x, (short)mob.y, 71, mob.hp, mob.maxHp, mob.sys);
				}
				if (mob.templateId == 72)
				{
					bigBoss2 = new BigBoss2(b, (short)mob.x, (short)mob.y, 72, mob.hp, mob.maxHp, 3);
				}
				if (mob.isBoss)
				{
					newBoss = new NewBoss(b, (short)mob.x, (short)mob.y, mob.templateId, mob.hp, mob.maxHp, mob.sys);
				}
				if (newBoss != null)
				{
					GameScr.vMob.addElement(newBoss);
				}
				else if (bigBoss != null)
				{
					GameScr.vMob.addElement(bigBoss);
				}
				else if (bachTuoc != null)
				{
					GameScr.vMob.addElement(bachTuoc);
				}
				else if (bigBoss2 != null)
				{
					GameScr.vMob.addElement(bigBoss2);
				}
				else
				{
					GameScr.vMob.addElement(mob);
				}
			}
			for (int j = 0; j < Mob.lastMob.size(); j++)
			{
				string text = (string)Mob.lastMob.elementAt(j);
				if (!Mob.isExistNewMob(text))
				{
					Mob.arrMobTemplate[int.Parse(text)].data = null;
					Mob.lastMob.removeElementAt(j);
					j--;
				}
			}
			if (Char.myCharz().mobMe != null && GameScr.findMobInMap(Char.myCharz().mobMe.mobId) == null)
			{
				Char.myCharz().mobMe.getData();
				Char.myCharz().mobMe.x = Char.myCharz().cx;
				Char.myCharz().mobMe.y = Char.myCharz().cy - 40;
				GameScr.vMob.addElement(Char.myCharz().mobMe);
			}
			num = msg.reader().readByte();
			for (byte b2 = 0; b2 < num; b2++)
			{
			}
			GameCanvas.debug("SA75x6", 2);
			num = msg.reader().readByte();
			Res.outz("NPC size= " + num);
			for (int k = 0; k < num; k++)
			{
				sbyte status = msg.reader().readByte();
				short cx = msg.reader().readShort();
				short num2 = msg.reader().readShort();
				sbyte b3 = msg.reader().readByte();
				short num3 = msg.reader().readShort();
				if (b3 != 6 && ((Char.myCharz().taskMaint.taskId >= 7 && (Char.myCharz().taskMaint.taskId != 7 || Char.myCharz().taskMaint.index > 1)) || (b3 != 7 && b3 != 8 && b3 != 9)) && (Char.myCharz().taskMaint.taskId >= 6 || b3 != 16))
				{
					if (b3 == 4)
					{
						GameScr.gI().magicTree = new MagicTree(k, status, cx, num2, b3, num3);
						Service.gI().magicTree(2);
						GameScr.vNpc.addElement(GameScr.gI().magicTree);
					}
					else
					{
						Npc o = new Npc(k, status, cx, num2 + 3, b3, num3);
						GameScr.vNpc.addElement(o);
					}
				}
			}
			GameCanvas.debug("SA75x7", 2);
			num = msg.reader().readByte();
			Res.outz("item size = " + num);
			for (int l = 0; l < num; l++)
			{
				short itemMapID = msg.reader().readShort();
				short itemTemplateID = msg.reader().readShort();
				int x = msg.reader().readShort();
				int y = msg.reader().readShort();
				int num4 = msg.reader().readInt();
				short r = 0;
				if (num4 == -2)
				{
					r = msg.reader().readShort();
				}
				ItemMap itemMap = new ItemMap(num4, itemMapID, itemTemplateID, x, y, r);
				bool flag = false;
				for (int m = 0; m < GameScr.vItemMap.size(); m++)
				{
					ItemMap itemMap2 = (ItemMap)GameScr.vItemMap.elementAt(m);
					if (itemMap2.itemMapID == itemMap.itemMapID)
					{
						flag = true;
						break;
					}
				}
				if (!flag)
				{
					GameScr.vItemMap.addElement(itemMap);
				}
			}
			TileMap.vCurrItem.removeAllElements();
			if (mGraphics.zoomLevel == 1)
			{
				BgItem.clearHashTable();
			}
			BgItem.vKeysNew.removeAllElements();
			if (!GameCanvas.lowGraphic || (GameCanvas.lowGraphic && TileMap.isVoDaiMap()) || TileMap.mapID == 45 || TileMap.mapID == 46 || TileMap.mapID == 47 || TileMap.mapID == 48)
			{
				short num5 = msg.reader().readShort();
				Res.outz("nItem= " + num5);
				for (int n = 0; n < num5; n++)
				{
					short id = msg.reader().readShort();
					short num6 = msg.reader().readShort();
					short num7 = msg.reader().readShort();
					if (TileMap.getBIById(id) == null)
					{
						continue;
					}
					BgItem bIById = TileMap.getBIById(id);
					BgItem bgItem = new BgItem();
					bgItem.id = id;
					bgItem.idImage = bIById.idImage;
					bgItem.dx = bIById.dx;
					bgItem.dy = bIById.dy;
					bgItem.x = num6 * TileMap.size;
					bgItem.y = num7 * TileMap.size;
					bgItem.layer = bIById.layer;
					if (TileMap.isExistMoreOne(bgItem.id))
					{
						bgItem.trans = ((n % 2 != 0) ? 2 : 0);
						if (TileMap.mapID == 45)
						{
							bgItem.trans = 0;
						}
					}
					Image image = null;
					if (!BgItem.imgNew.containsKey(bgItem.idImage + string.Empty))
					{
						if (mGraphics.zoomLevel == 1)
						{
							image = GameCanvas.loadImage("/mapBackGround/" + bgItem.idImage + ".png");
							if (image == null)
							{
								image = Image.createRGBImage(new int[1], 1, 1, bl: true);
								Service.gI().getBgTemplate(bgItem.idImage);
							}
							BgItem.imgNew.put(bgItem.idImage + string.Empty, image);
						}
						else
						{
							bool flag2 = false;
							sbyte[] array = Rms.loadRMS(mGraphics.zoomLevel + "bgItem" + bgItem.idImage);
							if (array != null)
							{
								if (BgItem.newSmallVersion != null)
								{
									Res.outz("Small  last= " + array.Length % 127 + "new Version= " + BgItem.newSmallVersion[bgItem.idImage]);
									if (array.Length % 127 != BgItem.newSmallVersion[bgItem.idImage])
									{
										flag2 = true;
									}
								}
								if (!flag2)
								{
									image = Image.createImage(array, 0, array.Length);
									if (image != null)
									{
										BgItem.imgNew.put(bgItem.idImage + string.Empty, image);
									}
									else
									{
										flag2 = true;
									}
								}
							}
							else
							{
								flag2 = true;
							}
							if (flag2)
							{
								image = GameCanvas.loadImage("/mapBackGround/" + bgItem.idImage + ".png");
								if (image == null)
								{
									image = Image.createRGBImage(new int[1], 1, 1, bl: true);
									Service.gI().getBgTemplate(bgItem.idImage);
								}
								BgItem.imgNew.put(bgItem.idImage + string.Empty, image);
							}
						}
						BgItem.vKeysLast.addElement(bgItem.idImage + string.Empty);
					}
					if (!BgItem.isExistKeyNews(bgItem.idImage + string.Empty))
					{
						BgItem.vKeysNew.addElement(bgItem.idImage + string.Empty);
					}
					bgItem.changeColor();
					TileMap.vCurrItem.addElement(bgItem);
				}
				for (int num8 = 0; num8 < BgItem.vKeysLast.size(); num8++)
				{
					string text2 = (string)BgItem.vKeysLast.elementAt(num8);
					if (!BgItem.isExistKeyNews(text2))
					{
						BgItem.imgNew.remove(text2);
						if (BgItem.imgNew.containsKey(text2 + "blend" + 1))
						{
							BgItem.imgNew.remove(text2 + "blend" + 1);
						}
						if (BgItem.imgNew.containsKey(text2 + "blend" + 3))
						{
							BgItem.imgNew.remove(text2 + "blend" + 3);
						}
						BgItem.vKeysLast.removeElementAt(num8);
						num8--;
					}
				}
				BackgroudEffect.isFog = false;
				BackgroudEffect.nCloud = 0;
				EffecMn.vEff.removeAllElements();
				BackgroudEffect.vBgEffect.removeAllElements();
				Effect.newEff.removeAllElements();
				short num9 = msg.reader().readShort();
				for (int num10 = 0; num10 < num9; num10++)
				{
					string key = msg.reader().readUTF();
					string value = msg.reader().readUTF();
					keyValueAction(key, value);
				}
				for (int num11 = 0; num11 < Effect.lastEff.size(); num11++)
				{
					string text3 = (string)Effect.lastEff.elementAt(num11);
					if (!Effect.isExistNewEff(text3))
					{
						Effect.removeEffData(int.Parse(text3));
						Effect.lastEff.removeElementAt(num11);
						num11--;
					}
				}
			}
			else
			{
				short num12 = msg.reader().readShort();
				for (int num13 = 0; num13 < num12; num13++)
				{
					short num14 = msg.reader().readShort();
					short num15 = msg.reader().readShort();
					short num16 = msg.reader().readShort();
				}
				short num17 = msg.reader().readShort();
				for (int num18 = 0; num18 < num17; num18++)
				{
					string text4 = msg.reader().readUTF();
					string text5 = msg.reader().readUTF();
				}
			}
			TileMap.bgType = msg.reader().readByte();
			sbyte teleport = msg.reader().readByte();
			loadCurrMap(teleport);
			Char.isLoadingMap = false;
			GameCanvas.debug("SA75x8", 2);
			long nowMapMs = mSystem.currentTimeMillis();
			if (nowMapMs - _lastMapGcAt >= 180000L)
			{
				_lastMapGcAt = nowMapMs;
				Resources.UnloadUnusedAssets();
				GC.Collect();
			}
			Cout.LogError("----------DA CHAY XONG LOAD INFO MAP");
		}
		catch (Exception ex)
		{
			Res.err("LOI TAI LOADMAP INFO " + ex.StackTrace);
			Pk9rXmap.FixBlackScreen();
		}
	}

	private static long _lastMapGcAt;

	public void keyValueAction(string key, string value)
	{
		if (key.Equals("eff"))
		{
			if (Panel.graphics > 0)
			{
				return;
			}
			string[] array = Res.split(value, ".", 0);
			int id = int.Parse(array[0]);
			int layer = int.Parse(array[1]);
			int x = int.Parse(array[2]);
			int y = int.Parse(array[3]);
			int loop;
			int loopCount;
			if (array.Length <= 4)
			{
				loop = -1;
				loopCount = 1;
			}
			else
			{
				loop = int.Parse(array[4]);
				loopCount = int.Parse(array[5]);
			}
			Effect effect = new Effect(id, x, y, layer, loop, loopCount);
			if (array.Length > 6)
			{
				effect.typeEff = int.Parse(array[6]);
				if (array.Length > 7)
				{
					effect.indexFrom = int.Parse(array[7]);
					effect.indexTo = int.Parse(array[8]);
				}
			}
			EffecMn.addEff(effect);
		}
		else if (key.Equals("beff") && Panel.graphics <= 1)
		{
			BackgroudEffect.addEffect(int.Parse(value));
		}
	}

	public void messageNotMap(Message msg)
	{
		GameCanvas.debug("SA6", 2);
		try
		{
			sbyte b = msg.reader().readByte();
			Res.outz("---messageNotMap : " + b);
			switch (b)
			{
			case 16:
				MoneyCharge.gI().switchToMe();
				break;
			case 17:
				GameCanvas.debug("SYB123", 2);
				Char.myCharz().clearTask();
				break;
			case 18:
			{
				GameCanvas.isLoading = false;
				GameCanvas.endDlg();
				int num2 = msg.reader().readInt();
				GameCanvas.inputDlg.show(mResources.changeNameChar, new Command(mResources.OK, GameCanvas.instance, 88829, num2), TField.INPUT_TYPE_ANY);
				break;
			}
			case 20:
				Char.myCharz().cPk = msg.reader().readByte();
				GameScr.info1.addInfo(mResources.PK_NOW + " " + Char.myCharz().cPk, 0);
				break;
			case 35:
				GameCanvas.endDlg();
				GameScr.gI().resetButton();
				GameScr.info1.addInfo(msg.reader().readUTF(), 0);
				break;
			case 36:
				GameScr.typeActive = msg.reader().readByte();
				Res.outz("load Me Active: " + GameScr.typeActive);
				break;
			case 4:
			{
				GameCanvas.debug("SA8", 2);
				GameCanvas.loginScr.savePass();
				GameScr.isAutoPlay = false;
				GameScr.canAutoPlay = false;
				LoginScr.isUpdateAll = true;
				LoginScr.isUpdateData = true;
				LoginScr.isUpdateMap = true;
				LoginScr.isUpdateSkill = true;
				LoginScr.isUpdateItem = true;
				GameScr.vsData = msg.reader().readByte();
				GameScr.vsMap = msg.reader().readByte();
				GameScr.vsSkill = msg.reader().readByte();
				GameScr.vsItem = msg.reader().readByte();
				sbyte b2 = msg.reader().readByte();
				if (GameCanvas.loginScr.isLogin2)
				{
					Rms.saveRMSString("acc", string.Empty);
					Rms.saveRMSString("pass", string.Empty);
				}
				else
				{
					Rms.saveRMSString("userAo" + ServerListScreen.ipSelect, string.Empty);
				}
				if (GameScr.vsData != GameScr.vcData)
				{
					GameScr.isLoadAllData = false;
					Service.gI().updateData();
				}
				else
				{
					try
					{
						LoginScr.isUpdateData = false;
					}
					catch (Exception)
					{
						GameScr.vcData = -1;
						Service.gI().updateData();
					}
				}
				if (GameScr.vsMap != GameScr.vcMap)
				{
					GameScr.isLoadAllData = false;
					Service.gI().updateMap();
				}
				else
				{
					try
					{
						if (!GameScr.isLoadAllData)
						{
							DataInputStream dataInputStream = new DataInputStream(Rms.loadRMS("NRmap"));
							createMap(dataInputStream.r);
						}
						LoginScr.isUpdateMap = false;
					}
					catch (Exception)
					{
						GameScr.vcMap = -1;
						Service.gI().updateMap();
					}
				}
				if (GameScr.vsSkill != GameScr.vcSkill)
				{
					GameScr.isLoadAllData = false;
					Service.gI().updateSkill();
				}
				else
				{
					try
					{
						if (!GameScr.isLoadAllData)
						{
							DataInputStream dataInputStream2 = new DataInputStream(Rms.loadRMS("NRskill"));
							createSkill(dataInputStream2.r);
						}
						LoginScr.isUpdateSkill = false;
					}
					catch (Exception)
					{
						GameScr.vcSkill = -1;
						Service.gI().updateSkill();
					}
				}
				if (GameScr.vsItem != GameScr.vcItem)
				{
					GameScr.isLoadAllData = false;
					Service.gI().updateItem();
				}
				else
				{
					try
					{
						DataInputStream dataInputStream3 = new DataInputStream(Rms.loadRMS("NRitem0"));
						loadItemNew(dataInputStream3.r, 0, isSave: false);
						DataInputStream dataInputStream4 = new DataInputStream(Rms.loadRMS("NRitem1"));
						loadItemNew(dataInputStream4.r, 1, isSave: false);
						DataInputStream dataInputStream5 = new DataInputStream(Rms.loadRMS("NRitem2"));
						loadItemNew(dataInputStream5.r, 2, isSave: false);
						DataInputStream dataInputStream6 = new DataInputStream(Rms.loadRMS("NRitem100"));
						loadItemNew(dataInputStream6.r, 100, isSave: false);
						LoginScr.isUpdateItem = false;
					}
					catch (Exception)
					{
						GameScr.vcItem = -1;
						Service.gI().updateItem();
					}
				}
				if (GameScr.vsData == GameScr.vcData && GameScr.vsMap == GameScr.vcMap && GameScr.vsSkill == GameScr.vcSkill && GameScr.vsItem == GameScr.vcItem)
				{
					if (!GameScr.isLoadAllData)
					{
						GameScr.gI().readDart();
						GameScr.gI().readEfect();
						GameScr.gI().readArrow();
						GameScr.gI().readSkill();
					}
					Service.gI().clientOk();
				}
				sbyte b3 = msg.reader().readByte();
				Res.outz("CAPTION LENT= " + b3);
				GameScr.exps = new long[b3];
				for (int j = 0; j < GameScr.exps.Length; j++)
				{
					GameScr.exps[j] = msg.reader().readLong();
				}
				break;
			}
			case 6:
			{
				Res.outz("GET UPDATE_MAP " + msg.reader().available() + " bytes");
				msg.reader().mark(100000);
				createMap(msg.reader());
				msg.reader().reset();
				sbyte[] data3 = new sbyte[msg.reader().available()];
				msg.reader().readFully(ref data3);
				Rms.saveRMS("NRmap", data3);
				sbyte[] data4 = new sbyte[1] { GameScr.vcMap };
				Rms.saveRMS("NRmapVersion", data4);
				LoginScr.isUpdateMap = false;
				if (GameScr.vsData == GameScr.vcData && GameScr.vsMap == GameScr.vcMap && GameScr.vsSkill == GameScr.vcSkill && GameScr.vsItem == GameScr.vcItem)
				{
					GameScr.gI().readDart();
					GameScr.gI().readEfect();
					GameScr.gI().readArrow();
					GameScr.gI().readSkill();
					Service.gI().clientOk();
				}
				break;
			}
			case 7:
			{
				Res.outz("GET UPDATE_SKILL " + msg.reader().available() + " bytes");
				msg.reader().mark(100000);
				createSkill(msg.reader());
				msg.reader().reset();
				sbyte[] data = new sbyte[msg.reader().available()];
				msg.reader().readFully(ref data);
				Rms.saveRMS("NRskill", data);
				sbyte[] data2 = new sbyte[1] { GameScr.vcSkill };
				Rms.saveRMS("NRskillVersion", data2);
				LoginScr.isUpdateSkill = false;
				if (GameScr.vsData == GameScr.vcData && GameScr.vsMap == GameScr.vcMap && GameScr.vsSkill == GameScr.vcSkill && GameScr.vsItem == GameScr.vcItem)
				{
					GameScr.gI().readDart();
					GameScr.gI().readEfect();
					GameScr.gI().readArrow();
					GameScr.gI().readSkill();
					Service.gI().clientOk();
				}
				break;
			}
			case 8:
				Res.outz("GET UPDATE_ITEM " + msg.reader().available() + " bytes");
				createItemNew(msg.reader());
				break;
			case 10:
				try
				{
					Char.isLoadingMap = true;
					Res.outz("REQUEST MAP TEMPLATE");
					GameCanvas.isLoading = true;
					TileMap.maps = null;
					TileMap.types = null;
					mSystem.gcc();
					GameCanvas.debug("SA99", 2);
					TileMap.tmw = msg.reader().readByte();
					TileMap.tmh = msg.reader().readByte();
					TileMap.maps = new int[TileMap.tmw * TileMap.tmh];
					Res.outz("   M apsize= " + TileMap.tmw * TileMap.tmh);
					for (int i = 0; i < TileMap.maps.Length; i++)
					{
						int num = msg.reader().readByte();
						if (num < 0)
						{
							num += 256;
						}
						TileMap.maps[i] = (ushort)num;
					}
					TileMap.types = new int[TileMap.maps.Length];
					msg = messWait;
					loadInfoMap(msg);
					try
					{
						TileMap.isMapDouble = ((msg.reader().readByte() != 0) ? true : false);
					}
					catch (Exception)
					{
					}
				}
				catch (Exception ex2)
				{
					Cout.LogError("LOI TAI CASE REQUEST_MAPTEMPLATE " + ex2.ToString());
				}
				msg.cleanup();
				messWait.cleanup();
				msg = (messWait = null);
				break;
			case 12:
				GameCanvas.debug("SA10", 2);
				break;
			case 9:
				GameCanvas.debug("SA11", 2);
				break;
			}
		}
		catch (Exception)
		{
			Cout.LogError("LOI TAI messageNotMap + " + msg.command);
		}
		finally
		{
			msg?.cleanup();
		}
	}

	public void messageNotLogin(Message msg)
	{
		try
		{
			sbyte b = msg.reader().readByte();
			Res.outz("---messageNotLogin : " + b);
			if (b != 2)
			{
				return;
			}
			string text = msg.reader().readUTF();
			if (mSystem.isTest)
			{
				text = "88:192.168.1.88:20000:0,53:112.213.85.53:20000:0," + text;
			}
			if (Rms.loadRMSInt("AdminLink") == 1)
			{
				return;
			}
			if (mSystem.clientType == 1)
			{
				ServerListScreen.linkDefault = text;
			}
			else
			{
				ServerListScreen.linkDefault = text;
			}
			ServerListScreen.getServerList(ServerListScreen.linkDefault);
			try
			{
				sbyte b2 = msg.reader().readByte();
				Panel.CanNapTien = b2 == 1;
				sbyte x = msg.reader().readByte();
				Rms.saveRMSInt("AdminLink", x);
			}
			catch (Exception)
			{
			}
		}
		catch (Exception)
		{
		}
		finally
		{
			msg?.cleanup();
		}
	}

	public void messageSubCommand(Message msg)
	{
		try
		{
			GameCanvas.debug("SA12", 2);
			sbyte b = msg.reader().readByte();
			Res.outz("---messageSubCommand : " + b);
			switch (b)
			{
			case 63:
			{
				sbyte b4 = msg.reader().readByte();
				if (b4 > 0)
				{
					InfoDlg.showWait();
					MyVector vPlayerMenu = GameCanvas.panel.vPlayerMenu;
					for (int i = 0; i < b4; i++)
					{
						string caption = msg.reader().readUTF();
						string caption2 = msg.reader().readUTF();
						short menuSelect = msg.reader().readShort();
						Char.myCharz().charFocus.menuSelect = menuSelect;
						Command command = new Command(caption, 11115, Char.myCharz().charFocus);
						command.caption2 = caption2;
						vPlayerMenu.addElement(command);
					}
					InfoDlg.hide();
					GameCanvas.panel.setTabPlayerMenu();
				}
				break;
			}
			case 1:
				GameCanvas.debug("SA13", 2);
				Char.myCharz().nClass = GameScr.nClasss[msg.reader().readByte()];
				Char.myCharz().cTiemNang = msg.reader().readLong();
				Char.myCharz().vSkill.removeAllElements();
				Char.myCharz().vSkillFight.removeAllElements();
				Char.myCharz().myskill = null;
				break;
			case 2:
			{
				GameCanvas.debug("SA14", 2);
				if (Char.myCharz().statusMe != 14 && Char.myCharz().statusMe != 5)
				{
					Char.myCharz().cHP = Char.myCharz().cHPFull;
					Char.myCharz().cMP = Char.myCharz().cMPFull;
					Cout.LogError2(" ME_LOAD_SKILL");
				}
				Char.myCharz().vSkill.removeAllElements();
				Char.myCharz().vSkillFight.removeAllElements();
				sbyte b2 = msg.reader().readByte();
				for (sbyte b3 = 0; b3 < b2; b3++)
				{
					short skillId = msg.reader().readShort();
					Skill skill2 = Skills.get(skillId);
					useSkill(skill2);
				}
				GameScr.gI().sortSkill();
				if (GameScr.isPaintInfoMe)
				{
					GameScr.indexRow = -1;
					GameScr.gI().left = (GameScr.gI().center = null);
				}
				break;
			}
			case 19:
				GameCanvas.debug("SA17", 2);
				Char.myCharz().boxSort();
				break;
			case 21:
			{
				GameCanvas.debug("SA19", 2);
				int num4 = msg.reader().readInt();
				Char.myCharz().xuInBox -= num4;
				Char.myCharz().xu += num4;
				Char.myCharz().xuStr = mSystem.numberTostring(Char.myCharz().xu);
				break;
			}
			case 0:
			{
				GameCanvas.debug("SA21", 2);
				RadarScr.list = new MyVector();
				Teleport.vTeleport.removeAllElements();
				GameScr.vCharInMap.removeAllElements();
				GameScr.vItemMap.removeAllElements();
				Char.vItemTime.removeAllElements();
				GameScr.loadImg();
				GameScr.currentCharViewInfo = Char.myCharz();
				Char.myCharz().charID = msg.reader().readInt();
				Char.myCharz().ctaskId = msg.reader().readByte();
				Char.myCharz().cgender = msg.reader().readByte();
				Char.myCharz().head = msg.reader().readShort();
				Char.myCharz().cName = msg.reader().readUTF();
				Char.myCharz().cPk = msg.reader().readByte();
				Char.myCharz().cTypePk = msg.reader().readByte();
				Char.myCharz().cPower = msg.reader().readLong();
				Char.myCharz().applyCharLevelPercent();
				Char.myCharz().eff5BuffHp = msg.reader().readShort();
				Char.myCharz().eff5BuffMp = msg.reader().readShort();
				Char.myCharz().nClass = GameScr.nClasss[msg.reader().readByte()];
				Char.myCharz().vSkill.removeAllElements();
				Char.myCharz().vSkillFight.removeAllElements();
				GameScr.gI().dHP = Char.myCharz().cHP;
				GameScr.gI().dMP = Char.myCharz().cMP;
				sbyte b6 = msg.reader().readByte();
				for (sbyte b7 = 0; b7 < b6; b7++)
				{
					Skill skill3 = Skills.get(msg.reader().readShort());
					useSkill(skill3);
				}
				GameScr.gI().sortSkill();
				GameScr.gI().loadSkillShortcut();
				Char.myCharz().xu = msg.reader().readLong();
				Char.myCharz().luongKhoa = msg.reader().readInt();
				Char.myCharz().luong = msg.reader().readInt();
				Char.myCharz().xuStr = mSystem.numberTostring(Char.myCharz().xu);
				Char.myCharz().luongStr = mSystem.numberTostring(Char.myCharz().luong);
				Char.myCharz().luongKhoaStr = mSystem.numberTostring(Char.myCharz().luongKhoa);
				Char.myCharz().arrItemBody = new Item[msg.reader().readByte()];
				try
				{
					Char.myCharz().setDefaultPart();
					for (int k = 0; k < Char.myCharz().arrItemBody.Length; k++)
					{
						short num5 = msg.reader().readShort();
						if (num5 == -1)
						{
							continue;
						}
						ItemTemplate itemTemplate = ItemTemplates.get(num5);
						int type = itemTemplate.type;
						Char.myCharz().arrItemBody[k] = new Item();
						Char.myCharz().arrItemBody[k].template = itemTemplate;
						Char.myCharz().arrItemBody[k].quantity = msg.reader().readInt();
						Char.myCharz().arrItemBody[k].info = msg.reader().readUTF();
						Char.myCharz().arrItemBody[k].content = msg.reader().readUTF();
						int num6 = msg.reader().readUnsignedByte();
						if (num6 != 0)
						{
							Char.myCharz().arrItemBody[k].itemOption = new ItemOption[num6];
							for (int l = 0; l < Char.myCharz().arrItemBody[k].itemOption.Length; l++)
							{
								int num7 = msg.reader().readUnsignedByte();
								int param = msg.reader().readUnsignedShort();
								if (num7 != -1)
								{
									Char.myCharz().arrItemBody[k].itemOption[l] = new ItemOption(num7, param);
								}
							}
						}
						switch (type)
						{
						case 0:
							Res.outz("toi day =======================================" + Char.myCharz().body);
							Char.myCharz().body = Char.myCharz().arrItemBody[k].template.part;
							break;
						case 1:
							Char.myCharz().leg = Char.myCharz().arrItemBody[k].template.part;
							Res.outz("toi day =======================================" + Char.myCharz().leg);
							break;
						}
					}
				}
				catch (Exception)
				{
				}
				Char.myCharz().arrItemBag = new Item[msg.reader().readByte()];
				GameScr.hpPotion = 0;
				for (int m = 0; m < Char.myCharz().arrItemBag.Length; m++)
				{
					short num8 = msg.reader().readShort();
					if (num8 == -1)
					{
						continue;
					}
					Char.myCharz().arrItemBag[m] = new Item();
					Char.myCharz().arrItemBag[m].template = ItemTemplates.get(num8);
					Char.myCharz().arrItemBag[m].quantity = msg.reader().readInt();
					Char.myCharz().arrItemBag[m].info = msg.reader().readUTF();
					Char.myCharz().arrItemBag[m].content = msg.reader().readUTF();
					Char.myCharz().arrItemBag[m].indexUI = m;
					sbyte b8 = msg.reader().readByte();
					if (b8 != 0)
					{
						Char.myCharz().arrItemBag[m].itemOption = new ItemOption[b8];
						for (int n = 0; n < Char.myCharz().arrItemBag[m].itemOption.Length; n++)
						{
							int num9 = msg.reader().readUnsignedByte();
							int param2 = msg.reader().readUnsignedShort();
							if (num9 != -1)
							{
								Char.myCharz().arrItemBag[m].itemOption[n] = new ItemOption(num9, param2);
								Char.myCharz().arrItemBag[m].getCompare();
							}
						}
					}
					if (Char.myCharz().arrItemBag[m].template.type == 6)
					{
						GameScr.hpPotion += Char.myCharz().arrItemBag[m].quantity;
					}
				}
				Char.myCharz().arrItemBox = new Item[msg.reader().readByte()];
				GameCanvas.panel.hasUse = 0;
				for (int num10 = 0; num10 < Char.myCharz().arrItemBox.Length; num10++)
				{
					short num11 = msg.reader().readShort();
					if (num11 == -1)
					{
						continue;
					}
					Char.myCharz().arrItemBox[num10] = new Item();
					Char.myCharz().arrItemBox[num10].template = ItemTemplates.get(num11);
					Char.myCharz().arrItemBox[num10].quantity = msg.reader().readInt();
					Char.myCharz().arrItemBox[num10].info = msg.reader().readUTF();
					Char.myCharz().arrItemBox[num10].content = msg.reader().readUTF();
					Char.myCharz().arrItemBox[num10].itemOption = new ItemOption[msg.reader().readByte()];
					for (int num12 = 0; num12 < Char.myCharz().arrItemBox[num10].itemOption.Length; num12++)
					{
						int num13 = msg.reader().readUnsignedByte();
						int param3 = msg.reader().readUnsignedShort();
						if (num13 != -1)
						{
							Char.myCharz().arrItemBox[num10].itemOption[num12] = new ItemOption(num13, param3);
							Char.myCharz().arrItemBox[num10].getCompare();
						}
					}
					GameCanvas.panel.hasUse++;
				}
				Char.myCharz().statusMe = 4;
				int num14 = Rms.loadRMSInt(Char.myCharz().cName + "vci");
				if (num14 < 1)
				{
					GameScr.isViewClanInvite = false;
				}
				else
				{
					GameScr.isViewClanInvite = true;
				}
				short num15 = msg.reader().readShort();
				Char.idHead = new short[num15];
				Char.idAvatar = new short[num15];
				for (int num16 = 0; num16 < num15; num16++)
				{
					Char.idHead[num16] = msg.reader().readShort();
					Char.idAvatar[num16] = msg.reader().readShort();
				}
				for (int num17 = 0; num17 < GameScr.info1.charId.Length; num17++)
				{
					GameScr.info1.charId[num17] = new int[3];
				}
				GameScr.info1.charId[Char.myCharz().cgender][0] = msg.reader().readShort();
				GameScr.info1.charId[Char.myCharz().cgender][1] = msg.reader().readShort();
				GameScr.info1.charId[Char.myCharz().cgender][2] = msg.reader().readShort();
				Char.myCharz().isNhapThe = msg.reader().readByte() == 1;
				Res.outz("NHAP THE= " + Char.myCharz().isNhapThe);
				GameScr.deltaTime = mSystem.currentTimeMillis() - (long)msg.reader().readInt() * 1000L;
				GameScr.isNewMember = msg.reader().readByte();
				Service.gI().updateCaption((sbyte)Char.myCharz().cgender);
				Service.gI().androidPack();
				try
				{
					Char.myCharz().idAuraEff = msg.reader().readShort();
					Char.myCharz().idEff_Set_Item = msg.reader().readSByte();
					Char.myCharz().idHat = msg.reader().readShort();
					if (Char.myCharz().bag >= 201 && Char.myCharz().bag < 2000)
					{
						Effect myBagEff = new Effect(Char.myCharz().bag, Char.myCharz(), 2, -1, 10, 1);
						myBagEff.typeEff = 5;
						Char.myCharz().addEffChar(myBagEff);
					}
					break;
				}
				catch (Exception)
				{
					break;
				}
			}
			case 4:
				GameCanvas.debug("SA23", 2);
				Char.myCharz().xu = msg.reader().readLong();
				Char.myCharz().luong = msg.reader().readInt();
				Char.myCharz().cHP = msg.readInt3Byte();
				Char.myCharz().cMP = msg.readInt3Byte();
				Char.myCharz().luongKhoa = msg.reader().readInt();
				Char.myCharz().xuStr = mSystem.numberTostring(Char.myCharz().xu);
				Char.myCharz().luongStr = mSystem.numberTostring(Char.myCharz().luong);
				Char.myCharz().luongKhoaStr = mSystem.numberTostring(Char.myCharz().luongKhoa);
				break;
			case 5:
			{
				GameCanvas.debug("SA24", 2);
				int cHP = Char.myCharz().cHP;
				Char.myCharz().cHP = msg.readInt3Byte();
				if (Char.myCharz().cHP > cHP && Char.myCharz().cTypePk != 4)
				{
					GameScr.startFlyText("+" + (Char.myCharz().cHP - cHP) + " " + mResources.HP, Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch - 20, 0, -1, mFont.HP);
					SoundMn.gI().HP_MPup();
					if (Char.myCharz().petFollow != null && Char.myCharz().petFollow.smallID == 5003)
					{
						MonsterDart.addMonsterDart(Char.myCharz().petFollow.cmx + ((Char.myCharz().petFollow.dir != 1) ? (-10) : 10), Char.myCharz().petFollow.cmy + 10, isBoss: true, -1, -1, Char.myCharz(), 29);
					}
				}
				if (Char.myCharz().cHP < cHP)
				{
					GameScr.startFlyText("-" + (cHP - Char.myCharz().cHP) + " " + mResources.HP, Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch - 20, 0, -1, mFont.HP);
				}
				GameScr.gI().dHP = Char.myCharz().cHP;
				if (GameScr.isPaintInfoMe)
				{
				}
				break;
			}
			case 6:
			{
				GameCanvas.debug("SA25", 2);
				if (Char.myCharz().statusMe == 14 || Char.myCharz().statusMe == 5)
				{
					break;
				}
				int cMP = Char.myCharz().cMP;
				Char.myCharz().cMP = msg.readInt3Byte();
				if (Char.myCharz().cMP > cMP)
				{
					GameScr.startFlyText("+" + (Char.myCharz().cMP - cMP) + " " + mResources.KI, Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch - 23, 0, -2, mFont.MP);
					SoundMn.gI().HP_MPup();
					if (Char.myCharz().petFollow != null && Char.myCharz().petFollow.smallID == 5001)
					{
						MonsterDart.addMonsterDart(Char.myCharz().petFollow.cmx + ((Char.myCharz().petFollow.dir != 1) ? (-10) : 10), Char.myCharz().petFollow.cmy + 10, isBoss: true, -1, -1, Char.myCharz(), 29);
					}
				}
				if (Char.myCharz().cMP < cMP)
				{
					GameScr.startFlyText("-" + (cMP - Char.myCharz().cMP) + " " + mResources.KI, Char.myCharz().cx, Char.myCharz().cy - Char.myCharz().ch - 23, 0, -2, mFont.MP);
				}
				Res.outz("curr MP= " + Char.myCharz().cMP);
				GameScr.gI().dMP = Char.myCharz().cMP;
				if (GameScr.isPaintInfoMe)
				{
				}
				break;
			}
			case 7:
			{
				Char char9 = GameScr.findCharInMap(msg.reader().readInt());
				if (char9 == null)
				{
					break;
				}
				char9.clanID = msg.reader().readInt();
				if (char9.clanID == -2)
				{
					char9.isCopy = true;
				}
				readCharInfo(char9, msg);
				try
				{
					char9.idAuraEff = msg.reader().readShort();
					char9.idEff_Set_Item = msg.reader().readSByte();
					char9.idHat = msg.reader().readShort();
					if (char9.bag >= 201)
					{
						Effect effect = new Effect(char9.bag, char9, 2, -1, 10, 1);
						effect.typeEff = 5;
						char9.addEffChar(effect);
					}
					else
					{
						char9.removeEffChar(0, 201);
					}
					break;
				}
				catch (Exception)
				{
					break;
				}
			}
			case 8:
			{
				GameCanvas.debug("SA26", 2);
				Char char10 = GameScr.findCharInMap(msg.reader().readInt());
				if (char10 != null)
				{
					char10.cspeed = msg.reader().readByte();
				}
				break;
			}
			case 9:
			{
				GameCanvas.debug("SA27", 2);
				Char char8 = GameScr.findCharInMap(msg.reader().readInt());
				if (char8 != null)
				{
					char8.cHP = msg.readInt3Byte();
					char8.cHPFull = msg.readInt3Byte();
				}
				break;
			}
			case 10:
			{
				GameCanvas.debug("SA28", 2);
				Char char5 = GameScr.findCharInMap(msg.reader().readInt());
				if (char5 != null)
				{
					char5.cHP = msg.readInt3Byte();
					char5.cHPFull = msg.readInt3Byte();
					char5.eff5BuffHp = msg.reader().readShort();
					char5.eff5BuffMp = msg.reader().readShort();
					char5.wp = msg.reader().readShort();
					if (char5.wp == -1)
					{
						char5.setDefaultWeapon();
					}
				}
				break;
			}
			case 11:
			{
				GameCanvas.debug("SA29", 2);
				Char char2 = GameScr.findCharInMap(msg.reader().readInt());
				if (char2 != null)
				{
					char2.cHP = msg.readInt3Byte();
					char2.cHPFull = msg.readInt3Byte();
					char2.eff5BuffHp = msg.reader().readShort();
					char2.eff5BuffMp = msg.reader().readShort();
					char2.body = msg.reader().readShort();
					if (char2.body == -1)
					{
						char2.setDefaultBody();
					}
				}
				break;
			}
			case 12:
			{
				GameCanvas.debug("SA30", 2);
				Char char11 = GameScr.findCharInMap(msg.reader().readInt());
				if (char11 != null)
				{
					char11.cHP = msg.readInt3Byte();
					char11.cHPFull = msg.readInt3Byte();
					char11.eff5BuffHp = msg.reader().readShort();
					char11.eff5BuffMp = msg.reader().readShort();
					char11.leg = msg.reader().readShort();
					if (char11.leg == -1)
					{
						char11.setDefaultLeg();
					}
				}
				break;
			}
			case 13:
			{
				GameCanvas.debug("SA31", 2);
				int num2 = msg.reader().readInt();
				Char @char = ((num2 != Char.myCharz().charID) ? GameScr.findCharInMap(num2) : Char.myCharz());
				if (@char != null)
				{
					@char.cHP = msg.readInt3Byte();
					@char.cHPFull = msg.readInt3Byte();
					@char.eff5BuffHp = msg.reader().readShort();
					@char.eff5BuffMp = msg.reader().readShort();
				}
				break;
			}
			case 14:
			{
				GameCanvas.debug("SA32", 2);
				Char char4 = GameScr.findCharInMap(msg.reader().readInt());
				if (char4 == null)
				{
					break;
				}
				char4.cHP = msg.readInt3Byte();
				sbyte b5 = msg.reader().readByte();
				Res.outz("player load hp type= " + b5);
				if (b5 == 1)
				{
					ServerEffect.addServerEffect(11, char4, 5);
					ServerEffect.addServerEffect(104, char4, 4);
				}
				if (b5 == 2)
				{
					char4.doInjure();
				}
				try
				{
					char4.cHPFull = msg.readInt3Byte();
					break;
				}
				catch (Exception)
				{
					break;
				}
			}
			case 15:
			{
				GameCanvas.debug("SA33", 2);
				Char char3 = GameScr.findCharInMap(msg.reader().readInt());
				if (char3 != null)
				{
					char3.cHP = msg.readInt3Byte();
					char3.cHPFull = msg.readInt3Byte();
					char3.cx = msg.reader().readShort();
					char3.cy = msg.reader().readShort();
					char3.statusMe = 1;
					char3.cp3 = 3;
					ServerEffect.addServerEffect(109, char3, 2);
				}
				break;
			}
			case 35:
			{
				GameCanvas.debug("SY3", 2);
				int num3 = msg.reader().readInt();
				Res.outz("CID = " + num3);
				if (TileMap.mapID == 130)
				{
					GameScr.gI().starVS();
				}
				if (num3 == Char.myCharz().charID)
				{
					Char.myCharz().cTypePk = msg.reader().readByte();
					if (GameScr.gI().isVS() && Char.myCharz().cTypePk != 0)
					{
						GameScr.gI().starVS();
					}
					Res.outz("type pk= " + Char.myCharz().cTypePk);
					Char.myCharz().npcFocus = null;
					if (!GameScr.gI().isMeCanAttackMob(Char.myCharz().mobFocus))
					{
						Char.myCharz().mobFocus = null;
					}
					Char.myCharz().itemFocus = null;
				}
				else
				{
					Char char6 = GameScr.findCharInMap(num3);
					if (char6 != null)
					{
						Res.outz("type pk= " + char6.cTypePk);
						char6.cTypePk = msg.reader().readByte();
						if (char6.isAttacPlayerStatus())
						{
							Char.myCharz().charFocus = char6;
						}
					}
				}
				for (int j = 0; j < GameScr.vCharInMap.size(); j++)
				{
					Char char7 = GameScr.findCharInMap(j);
					if (char7 != null && char7.cTypePk != 0 && char7.cTypePk == Char.myCharz().cTypePk)
					{
						if (!Char.myCharz().mobFocus.isMobMe)
						{
							Char.myCharz().mobFocus = null;
						}
						Char.myCharz().npcFocus = null;
						Char.myCharz().itemFocus = null;
						break;
					}
				}
				Res.outz("update type pk= ");
				break;
			}
			case 61:
			{
				string text = msg.reader().readUTF();
				sbyte[] data = new sbyte[msg.reader().readInt()];
				msg.reader().read(ref data);
				if (data.Length == 0)
				{
					data = null;
				}
				if (text.Equals("KSkill"))
				{
					GameScr.gI().onKSkill(data);
				}
				else if (text.Equals("OSkill"))
				{
					GameScr.gI().onOSkill(data);
				}
				else if (text.Equals("CSkill"))
				{
					GameScr.gI().onCSkill(data);
				}
				break;
			}
			case 23:
			{
				short num = msg.reader().readShort();
				Skill skill = Skills.get(num);
				useSkill(skill);
				if (num != 0 && num != 14 && num != 28)
				{
					GameScr.info1.addInfo(mResources.LEARN_SKILL + " " + skill.template.name, 0);
				}
				break;
			}
			case 62:
				Res.outz("ME UPDATE SKILL");
				read_UpdateSkill(msg);
				break;
			}
		}
		catch (Exception ex5)
		{
			Cout.println("Loi tai Sub : " + ex5.ToString());
		}
		finally
		{
			msg?.cleanup();
		}
	}

	private void useSkill(Skill skill)
	{
		if (Char.myCharz().myskill == null)
		{
			Char.myCharz().myskill = skill;
		}
		else if (skill.template.Equals(Char.myCharz().myskill.template))
		{
			Char.myCharz().myskill = skill;
		}
		Char.myCharz().vSkill.addElement(skill);
		if ((skill.template.type == 1 || skill.template.type == 4 || skill.template.type == 2 || skill.template.type == 3) && (skill.template.maxPoint == 0 || (skill.template.maxPoint > 0 && skill.point > 0)))
		{
			if (skill.template.id == Char.myCharz().skillTemplateId)
			{
				Service.gI().selectSkill(Char.myCharz().skillTemplateId);
			}
			Char.myCharz().vSkillFight.addElement(skill);
		}
	}

	public bool readCharInfo(Char c, Message msg)
	{
		try
		{
			c.clevel = msg.reader().readByte();
			c.isInvisiblez = msg.reader().readBoolean();
			c.cTypePk = msg.reader().readByte();
			Res.outz("ADD TYPE PK= " + c.cTypePk + " to player " + c.charID + " @@ " + c.cName);
			c.nClass = GameScr.nClasss[msg.reader().readByte()];
			c.cgender = msg.reader().readByte();
			c.head = msg.reader().readShort();
			c.cName = msg.reader().readUTF();
			c.cHP = msg.readInt3Byte();
			c.dHP = c.cHP;
			if (c.cHP == 0)
			{
				c.statusMe = 14;
			}
			c.cHPFull = msg.readInt3Byte();
			if (c.cy >= TileMap.pxh - 100)
			{
				c.isFlyUp = true;
			}
			c.body = msg.reader().readShort();
			c.leg = msg.reader().readShort();
			c.bag = msg.reader().readUnsignedByte();
			Res.outz(" body= " + c.body + " leg= " + c.leg + " bag=" + c.bag + "BAG ==" + c.bag + "*********************************");
			c.isShadown = true;
			sbyte b = msg.reader().readByte();
			if (c.wp == -1)
			{
				c.setDefaultWeapon();
			}
			if (c.body == -1)
			{
				c.setDefaultBody();
			}
			if (c.leg == -1)
			{
				c.setDefaultLeg();
			}
			c.cx = msg.reader().readShort();
			c.cy = msg.reader().readShort();
			c.xSd = c.cx;
			c.ySd = c.cy;
			c.eff5BuffHp = msg.reader().readShort();
			c.eff5BuffMp = msg.reader().readShort();
			int num = msg.reader().readByte();
			for (int i = 0; i < num; i++)
			{
				EffectChar effectChar = new EffectChar(msg.reader().readByte(), msg.reader().readInt(), msg.reader().readInt(), msg.reader().readShort());
				c.vEff.addElement(effectChar);
				if (effectChar.template.type == 12 || effectChar.template.type == 11)
				{
					c.isInvisiblez = true;
				}
			}
			return true;
		}
		catch (Exception)
		{
		}
		return false;
	}

	private void readGetImgByName(Message msg)
	{
		try
		{
			string text = msg.reader().readUTF();
			sbyte nFrame = msg.reader().readByte();
			sbyte[] array = null;
			array = NinjaUtil.readByteArray(msg);
			Image img = createImage(array);
			ImgByName.SetImage(text, img, nFrame);
			if (array != null)
			{
				ImgByName.saveRMS(text, nFrame, array);
			}
		}
		catch (Exception)
		{
		}
	}

	private void createItemNew(myReader d)
	{
		try
		{
			loadItemNew(d, -1, isSave: true);
		}
		catch (Exception)
		{
		}
	}

	private void loadItemNew(myReader d, sbyte type, bool isSave)
	{
		try
		{
			d.mark(100000);
			GameScr.vcItem = d.readByte();
			type = d.readByte();
			switch (type)
			{
			case 0:
			{
				GameScr.gI().iOptionTemplates = new ItemOptionTemplate[d.readUnsignedByte()];
				for (int k = 0; k < GameScr.gI().iOptionTemplates.Length; k++)
				{
					GameScr.gI().iOptionTemplates[k] = new ItemOptionTemplate();
					GameScr.gI().iOptionTemplates[k].id = k;
					GameScr.gI().iOptionTemplates[k].name = d.readUTF();
					GameScr.gI().iOptionTemplates[k].type = d.readByte();
				}
				if (isSave)
				{
					d.reset();
					sbyte[] data5 = new sbyte[d.available()];
					d.readFully(ref data5);
					Rms.saveRMS("NRitem0", data5);
				}
				break;
			}
			case 1:
			{
				ItemTemplates.itemTemplates.clear();
				int num = d.readShort();
				for (int i = 0; i < num; i++)
				{
					ItemTemplate it = new ItemTemplate((short)i, d.readByte(), d.readByte(), d.readUTF(), d.readUTF(), d.readByte(), d.readInt(), d.readShort(), d.readShort(), d.readBoolean());
					ItemTemplates.add(it);
				}
				if (isSave)
				{
					d.reset();
					sbyte[] data2 = new sbyte[d.available()];
					d.readFully(ref data2);
					Rms.saveRMS("NRitem1", data2);
				}
				break;
			}
			case 2:
			{
				int num2 = d.readShort();
				int num3 = d.readShort();
				for (int j = num2; j < num3; j++)
				{
					ItemTemplate it2 = new ItemTemplate((short)j, d.readByte(), d.readByte(), d.readUTF(), d.readUTF(), d.readByte(), d.readInt(), d.readShort(), d.readShort(), d.readBoolean());
					ItemTemplates.add(it2);
				}
				if (isSave)
				{
					d.reset();
					sbyte[] data3 = new sbyte[d.available()];
					d.readFully(ref data3);
					Rms.saveRMS("NRitem2", data3);
					sbyte[] data4 = new sbyte[1] { GameScr.vcItem };
					Rms.saveRMS("NRitemVersion", data4);
					LoginScr.isUpdateItem = false;
					if (GameScr.vsData == GameScr.vcData && GameScr.vsMap == GameScr.vcMap && GameScr.vsSkill == GameScr.vcSkill && GameScr.vsItem == GameScr.vcItem)
					{
						GameScr.gI().readDart();
						GameScr.gI().readEfect();
						GameScr.gI().readArrow();
						GameScr.gI().readSkill();
						Service.gI().clientOk();
					}
				}
				break;
			}
			case 100:
				Char.Arr_Head_2Fr = readArrHead(d);
				if (isSave)
				{
					d.reset();
					sbyte[] data = new sbyte[d.available()];
					d.readFully(ref data);
					Rms.saveRMS("NRitem100", data);
				}
				break;
			}
		}
		catch (Exception ex)
		{
			ex.ToString();
		}
	}

	private void readFrameBoss(Message msg, int mobTemplateId)
	{
		try
		{
			int num = msg.reader().readByte();
			int[][] array = new int[num][];
			for (int i = 0; i < num; i++)
			{
				int num2 = msg.reader().readByte();
				array[i] = new int[num2];
				for (int j = 0; j < num2; j++)
				{
					array[i][j] = msg.reader().readByte();
				}
			}
			frameHT_NEWBOSS.put(mobTemplateId + string.Empty, array);
		}
		catch (Exception)
		{
		}
	}

	private int[][] readArrHead(myReader d)
	{
		int[][] array = new int[1][] { new int[2] { 542, 543 } };
		try
		{
			int num = d.readShort();
			array = new int[num][];
			for (int i = 0; i < array.Length; i++)
			{
				int num2 = d.readByte();
				array[i] = new int[num2];
				for (int j = 0; j < num2; j++)
				{
					array[i][j] = d.readShort();
				}
			}
			return array;
		}
		catch (Exception)
		{
			return array;
		}
	}

	public void phuban_Info(Message msg)
	{
		try
		{
			sbyte b = msg.reader().readByte();
			if (b == 0)
			{
				readPhuBan_CHIENTRUONGNAMEK(msg, b);
			}
		}
		catch (Exception)
		{
		}
	}

	private void readPhuBan_CHIENTRUONGNAMEK(Message msg, int type_PB)
	{
		try
		{
			switch (msg.reader().readByte())
			{
			case 0:
			{
				short idmapPaint = msg.reader().readShort();
				string nameTeam = msg.reader().readUTF();
				string nameTeam2 = msg.reader().readUTF();
				int maxPoint = msg.reader().readInt();
				short timeSecond = msg.reader().readShort();
				int maxLife = msg.reader().readByte();
				GameScr.phuban_Info = new InfoPhuBan(type_PB, idmapPaint, nameTeam, nameTeam2, maxPoint, timeSecond);
				GameScr.phuban_Info.maxLife = maxLife;
				GameScr.phuban_Info.updateLife(type_PB, 0, 0);
				break;
			}
			case 1:
			{
				int pointTeam = msg.reader().readInt();
				int pointTeam2 = msg.reader().readInt();
				if (GameScr.phuban_Info != null)
				{
					GameScr.phuban_Info.updatePoint(type_PB, pointTeam, pointTeam2);
				}
				break;
			}
			case 2:
			{
				sbyte b = msg.reader().readByte();
				short type = 0;
				short num = -1;
				switch (b)
				{
				case 1:
					type = 1;
					num = 3;
					break;
				case 2:
					type = 2;
					break;
				}
				num = -1;
				GameScr.phuban_Info = null;
				GameScr.addEffectEnd(type, num, 0, GameCanvas.hw, GameCanvas.hh, 0, 0, -1, null);
				break;
			}
			case 5:
			{
				short timeSecond2 = msg.reader().readShort();
				if (GameScr.phuban_Info != null)
				{
					GameScr.phuban_Info.updateTime(type_PB, timeSecond2);
				}
				break;
			}
			case 4:
			{
				int lifeTeam = msg.reader().readByte();
				int lifeTeam2 = msg.reader().readByte();
				if (GameScr.phuban_Info != null)
				{
					GameScr.phuban_Info.updateLife(type_PB, lifeTeam, lifeTeam2);
				}
				break;
			}
			case 3:
				break;
			}
		}
		catch (Exception)
		{
		}
	}

	public void read_opt(Message msg)
	{
		try
		{
			sbyte sub = msg.reader().readByte();
			if (sub == 0)
			{
				short idHat = msg.reader().readShort();
				Char.myCharz().idHat = idHat;
				SoundMn.gI().getStrOption();
			}
			else if (sub == 2)
			{
				int charId = msg.reader().readInt();
				int durSec = msg.reader().readUnsignedByte();
				short effId = msg.reader().readShort();
				if (durSec <= 0) durSec = 60;
				Char.SetPendingDanhHieu(charId, effId, durSec);
				if (Char.myCharz() != null && charId == Char.myCharz().charID)
				{
					AssemblyCSharp.Mod.GameEvents.OnDanhHieuPacketFromServer(effId);
				}
			}
		}
		catch (Exception)
		{
		}
	}

	public void read_UpdateSkill(Message msg)
	{
		try
		{
			short num = msg.reader().readShort();
			sbyte b = -1;
			try
			{
				b = msg.reader().readSByte();
			}
			catch (Exception)
			{
			}
			switch (b)
			{
			case 0:
			{
				short curExp = msg.reader().readShort();
				for (int m = 0; m < Char.myCharz().vSkill.size(); m++)
				{
					Skill skill4 = (Skill)Char.myCharz().vSkill.elementAt(m);
					if (skill4.skillId == num)
					{
						skill4.curExp = curExp;
						break;
					}
				}
				break;
			}
			case 1:
			{
				sbyte b2 = msg.reader().readByte();
				for (int n = 0; n < Char.myCharz().vSkill.size(); n++)
				{
					Skill skill5 = (Skill)Char.myCharz().vSkill.elementAt(n);
					if (skill5.skillId == num)
					{
						for (int num2 = 0; num2 < 20; num2++)
						{
							string nameImg = "Skills_" + skill5.template.id + "_" + b2 + "_" + num2;
							MainImage imagePath = ImgByName.getImagePath(nameImg, ImgByName.hashImagePath);
						}
						break;
					}
				}
				break;
			}
			case -1:
			{
				Skill skill = Skills.get(num);
				for (int i = 0; i < Char.myCharz().vSkill.size(); i++)
				{
					Skill skill2 = (Skill)Char.myCharz().vSkill.elementAt(i);
					if (skill2.template.id == skill.template.id)
					{
						Char.myCharz().vSkill.setElementAt(skill, i);
						break;
					}
				}
				for (int j = 0; j < Char.myCharz().vSkillFight.size(); j++)
				{
					Skill skill3 = (Skill)Char.myCharz().vSkillFight.elementAt(j);
					if (skill3.template.id == skill.template.id)
					{
						Char.myCharz().vSkillFight.setElementAt(skill, j);
						break;
					}
				}
				for (int k = 0; k < GameScr.onScreenSkill.Length; k++)
				{
					if (GameScr.onScreenSkill[k] != null && GameScr.onScreenSkill[k].template.id == skill.template.id)
					{
						GameScr.onScreenSkill[k] = skill;
						break;
					}
				}
				for (int l = 0; l < GameScr.keySkill.Length; l++)
				{
					if (GameScr.keySkill[l] != null && GameScr.keySkill[l].template.id == skill.template.id)
					{
						GameScr.keySkill[l] = skill;
						break;
					}
				}
				if (Char.myCharz().myskill.template.id == skill.template.id)
				{
					Char.myCharz().myskill = skill;
				}
				GameScr.info1.addInfo(mResources.hasJustUpgrade1 + skill.template.name + mResources.hasJustUpgrade2 + skill.point, 0);
				break;
			}
			}
		}
		catch (Exception)
		{
		}
	}
}
