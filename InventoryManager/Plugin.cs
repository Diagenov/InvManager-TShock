using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using TerrariaApi.Server;
using TShockAPI;
using Terraria.GameContent.Events;
using Terraria.Social;
using Terraria;

namespace InventoryManager
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Author => "Lord Diogen";
        public override string Name => "InvManager";
        
        List<Backpack> Backpacks; 

        public Plugin(Main game) : base(game)
        {
            Order = 100;
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                Database.Disconnect();
            }
            base.Dispose(disposing);
        }

        void OnInitialize(EventArgs e)
        {
            Commands.ChatCommands.Add(new Command("diogen.invmanager", Command, "inv"));
            Backpacks = new List<Backpack>();
            Database.Connect();
        }

        void OnLeave(LeaveEventArgs e)
        {
            var backpack = Backpacks.Find(x => x.Player.Index == e.Who);
            if (backpack == null)
            {
                return;
            }
            backpack.Clear();
            Backpacks.Remove(backpack);
        }

        void Command(CommandArgs e)
        {
            if (e.Player.Account == null)
            {
                e.Player.SendErrorMessage("Log in to your account, please.");
                return;
            }
            switch (e.Parameters.Count == 0 ? "help" : e.Parameters[0])
            {
                case "load":
                    {
                        if (e.Parameters.Count < 2 || !e.Player.RealPlayer)
                        {
                            e.Player.SendInfoMessage("Syntax: /inv load Name");
                            return;
                        }
                        var name = string.Join(" ", e.Parameters.Skip(1));
                        var inv = Database.Load(name);

                        if (inv.Name == null)
                        {
                            e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                            return;
                        }
                        if (!inv.HasPermission(e.Player))
                        {
                            e.Player.SendErrorMessage($"You do not have permission to load this inventory.");
                            return;
                        }
                        var backpack = Backpacks.Find(x => x.Player == e.Player);
                        var re_upload = backpack != null;

                        if (!re_upload)
                            Backpacks.Add(backpack = new Backpack(ref inv, e.Player));
                        else
                            backpack.NewInventory = inv;

                        backpack.Send(re_upload);
                        e.Player.SendSuccessMessage($"Inventory \"{name}\" successfully loaded!");
                        break;
                    }
                case "save":
                    {
                        if (e.Parameters.Count < 2 || !e.Player.RealPlayer)
                        {
                            e.Player.SendInfoMessage("Syntax: /inv save Name");
                            return;
                        }
                        var name = string.Join(" ", e.Parameters.Skip(1));
                        var inv = Database.Load(name);

                        if (inv.Name == null)
                        {
                            inv = new Inventory(e.Player, name);
                            Database.Save(ref inv);
                        }
                        else
                        {
                            if (!inv.CanManage(e.Player))
                            {
                                e.Player.SendErrorMessage($"You do not have permission to change this inventory.");
                                return;
                            }
                            inv.Character = new Character(e.TPlayer);
                            Database.UpdateInventory(ref inv);
                        }
                        e.Player.SendSuccessMessage($"Inventory \"{name}\" successfully saved!");
                        break;
                    }
                case "priv":
                    {
                        if (e.Parameters.Count < 3 || (e.Parameters[1] != "on" && e.Parameters[1] != "off"))
                        {
                            e.Player.SendInfoMessage("Syntax: /inv priv [on|off] Name");
                            return;
                        }
                        var name = string.Join(" ", e.Parameters.Skip(2));
                        bool isPrivate = e.Parameters[1] == "on";
                        var inv = Database.Load(name);

                        if (inv.Name == null)
                        {
                            e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                        }
                        else if (inv.Author != e.Player.Account.Name && !e.Player.HasPermission("diogen.invmanager.admin"))
                        {
                            e.Player.SendErrorMessage($"You do not have permission to change this inventory");
                        }
                        else if (Database.UpdatePrivate(name, isPrivate))
                        {
                            e.Player.SendSuccessMessage($"The status of the inventory \"{name}\" changed: {(isPrivate ? "private" : "public")}.");
                        }
                        else
                        {
                            e.Player.SendErrorMessage("Something went wrong...");
                        }
                        break;
                    }
                case "allow":
                case "remove":
                    {
                        if (e.Parameters.Count < 3)
                        {
                            e.Player.SendInfoMessage($"Syntax: /inv {e.Parameters[0]} Username Name");
                            return;
                        }
                        var username = e.Parameters[1];

                        if (TShock.UserAccounts.GetUserAccountByName(username) == null)
                        {
                            e.Player.SendErrorMessage($"User \"{username}\" not found.");
                            return;
                        }
                        var name = string.Join(" ", e.Parameters.Skip(2));
                        var inv = Database.Load(name);

                        if (inv.Name == null)
                        {
                            e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                            return;
                        }
                        if (!inv.CanManage(e.Player))
                        {
                            e.Player.SendErrorMessage($"You do not have permission to change this inventory");
                            return;
                        }
                        if (e.Parameters[0] == "allow")
                        {
                            if (inv.Usernames.Contains(username))
                            {
                                e.Player.SendErrorMessage($"User \"{username}\" already been allowed.");
                                return;
                            }
                            inv.Usernames.Add(username);
                        }
                        else
                        {
                            if (!inv.Usernames.Contains(username))
                            {
                                e.Player.SendErrorMessage($"User \"{username}\" do not been allowed.");
                                return;
                            }
                            inv.Usernames.Remove(username);
                        }
                        if (Database.UpdateUsernames(ref inv))
                        {
                            e.Player.SendSuccessMessage("Done!");
                            return;
                        }
                        e.Player.SendErrorMessage("Something went wrong...");
                        break;
                    }
                case "del":
                    {
                        if (e.Parameters.Count < 2)
                        {
                            e.Player.SendInfoMessage("Syntax: /inv del Name");
                            return;
                        }
                        var name = string.Join(" ", e.Parameters.Skip(1));
                        var inv = Database.Load(name);

                        if (inv.Name == null)
                        {
                            e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                        }
                        else if (inv.Author != e.Player.Account.Name && !e.Player.HasPermission("diogen.invmanager.admin"))
                        {
                            e.Player.SendErrorMessage($"You do not have permission to delete this inventory.");
                        }
                        else if (Database.Delete(name))
                        {
                            e.Player.SendSuccessMessage($"Inventory \"{name}\" successfully deleted!");
                        }
                        else
                        {
                            e.Player.SendErrorMessage("Something went wrong...");
                        }
                        break;
                    }
                case "rest":
                    {
                        if (!e.Player.RealPlayer)
                            return;
                        var backpack = Backpacks.Find(x => x.Player == e.Player);

                        if (backpack != null)
                        {
                            backpack.Clear();
                            Backpacks.Remove(backpack);
                            e.Player.SendSuccessMessage("Your inventory is back!");
                            return;
                        }
                        e.Player.SendErrorMessage("Your backpack not found.");
                        break;
                    }
                case "list":
                    {
                        string name = null, author = null, username = null;
                        bool? isPrivate = null;
                        int page = 1;

                        var tags = new List<string>();
                        var parameters = new List<string>();

                        while (e.Parameters.Count > 1)
                        {
                            var s = e.Parameters[1];
                            e.Parameters.RemoveAt(1);

                            if (int.TryParse(s, out page))
                            {
                                page = Math.Max(page, 1);
                                continue;
                            }
                            else
                            {
                                page = 1;
                            }
                            if (s.StartsWith("name:"))
                            {
                                if (s.Length > "name:".Length)
                                {
                                    name = string.Join(":", s.Split(':').Skip(1));
                                    parameters.Add(s);
                                }
                            }
                            else if (s.StartsWith("author:"))
                            {
                                if (s.Length > "author:".Length)
                                {
                                    author = string.Join(":", s.Split(':').Skip(1));
                                    parameters.Add(s);
                                }
                            }
                            else if (s.StartsWith("user:"))
                            {
                                if (s.Length > "user:".Length)
                                {
                                    username = string.Join(":", s.Split(':').Skip(1));
                                    parameters.Add(s);
                                }
                            }
                            if (s.StartsWith("name(_):"))
                            {
                                if (s.Length > "name(_):".Length)
                                {
                                    name = string.Join(":", s.Split(':').Skip(1)).Replace('_', ' ');
                                    parameters.Add(s);
                                }
                            }
                            else if (s.StartsWith("author(_):"))
                            {
                                if (s.Length > "author(_):".Length)
                                {
                                    author = string.Join(":", s.Split(':').Skip(1)).Replace('_', ' ');
                                    parameters.Add(s);
                                }
                            }
                            else if (s.StartsWith("user(_):"))
                            {
                                if (s.Length > "user(_):".Length)
                                {
                                    username = string.Join(":", s.Split(':').Skip(1)).Replace('_', ' ');
                                    parameters.Add(s);
                                }
                            }
                            else if (s == "-public")
                            {
                                isPrivate = false;
                                parameters.Add(s);
                            }
                            else if (s == "-private")
                            {
                                isPrivate = true;
                                parameters.Add(s);
                            }
                        }
                        if (name != null)
                        {
                            tags.Add($"called like {name}");
                        }
                        if (author != null)
                        {
                            tags.Add($"created by {author}");
                        }
                        if (isPrivate != null)
                        {
                            tags.Add(isPrivate.Value ? "private" : "public");
                        }
                        if (username != null)
                        {
                            tags.Add($"shared with {username}");
                        }
                        var list = Database.GetList(name, author, isPrivate, username);

                        PaginationTools.SendPage(e.Player, page, PaginationTools.BuildLinesFromTerms(list),
                            new PaginationTools.Settings
                            {
                                HeaderFormat = $"Inventories {string.Join(", ", tags)} ({{0}}/{{1}}):",
                                FooterFormat = $"Type /inv {string.Join(" ", parameters.Select(i => $"\"{i}\""))} {{0}} for more.",
                                NothingToDisplayString = "Such inventories don't exist in this universe!"
                            });
                        break;
                    }
                case "info":
                    {
                        if (e.Parameters.Count < 2)
                        {
                            e.Player.SendInfoMessage("Syntax: /inv info Name");
                            return;
                        }
                        var name = string.Join(" ", e.Parameters.Skip(1));
                        var inv = Database.Load(name);

                        if (inv.Name == null)
                        {
                            e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                            return;
                        }
                        e.Player.SendInfoMessage($" — — Inventory \"{name}\" info — —");
                        e.Player.SendInfoMessage($"Author: {inv.Author}");
                        e.Player.SendInfoMessage($"Status: {(inv.IsPrivate ? "private" : "public")}");
                        e.Player.SendInfoMessage($"Shared with: {(inv.Usernames.Count < 1 ? "-" : string.Join(", ", inv.Usernames))}");
                        e.Player.SendInfoMessage($"Count of occupied slots: {inv.Character.Items.Count}");
                        e.Player.SendInfoMessage($"Mana: {inv.Character.Skin.Mana}");
                        e.Player.SendInfoMessage($"HP: {inv.Character.Skin.HP}");
                        break;
                    }
                case "help":
                    {
                        e.Player.SendInfoMessage("Syntax: /inv [load|save|del|allow|remove|rest|apply|priv|info|list] arguments");
                        break;
                    }
                default:
                    {
                        goto case "help";
                    }
            }
        }

        class Backpack
        {
            public Character OldInventory;
            public Inventory NewInventory;
            public TSPlayer Player;

            public Backpack(ref Inventory newInv, TSPlayer plr)
            {
                OldInventory = new Character(plr.TPlayer);
                NewInventory = newInv;
                Player = plr;
            }

            public void Send(bool re_upload = false)
            {
                SendWorldInfo(false);
                if (re_upload)
                    new Character(Player.TPlayer).Clear(Player);
                else
                    OldInventory.Clear(Player);
                NewInventory.Character.Send(Player);
                SendWorldInfo(false);
            }

            public void Clear()
            {
                SendWorldInfo(true);
                new Character(Player.TPlayer).Clear(Player);
                OldInventory.Send(Player);
                OldInventory.Items.Clear();
                NewInventory.Clear();
                SendWorldInfo(false);
            }

            void SendWorldInfo(bool ssc)
            {
                if (Main.ServerSideCharacter)
                    return;

                using (var s = new MemoryStream())
                {
                    using (var w = new BinaryWriter(s))
                    {
                        w.BaseStream.Position = 2;
                       
                        w.Write((byte)PacketTypes.WorldInfo);
                        w.Write((int)Main.time);
                        BitsByte bb1 = 0;
                        {
                            bb1[0] = Main.dayTime;
                            bb1[1] = Main.bloodMoon;
                            bb1[2] = Main.eclipse;
                        }
                        w.Write(bb1);
                        w.Write((byte)Main.moonPhase);
                        w.Write((short)Main.maxTilesX);
                        w.Write((short)Main.maxTilesY);
                        w.Write((short)Main.spawnTileX);
                        w.Write((short)Main.spawnTileY);
                        w.Write((short)Main.worldSurface);
                        w.Write((short)Main.rockLayer);
                        w.Write(Main.worldID);
                        w.Write(Main.worldName);
                        w.Write((byte)Main.GameMode);
                        w.Write(Main.ActiveWorldFileData.UniqueId.ToByteArray());
                        w.Write(Main.ActiveWorldFileData.WorldGeneratorVersion);
                        w.Write((byte)Main.moonType);
                        w.Write((byte)WorldGen.treeBG1);
                        w.Write((byte)WorldGen.treeBG2);
                        w.Write((byte)WorldGen.treeBG3);
                        w.Write((byte)WorldGen.treeBG4);
                        w.Write((byte)WorldGen.corruptBG);
                        w.Write((byte)WorldGen.jungleBG);
                        w.Write((byte)WorldGen.snowBG);
                        w.Write((byte)WorldGen.hallowBG);
                        w.Write((byte)WorldGen.crimsonBG);
                        w.Write((byte)WorldGen.desertBG);
                        w.Write((byte)WorldGen.oceanBG);
                        w.Write((byte)WorldGen.mushroomBG);
                        w.Write((byte)WorldGen.underworldBG);
                        w.Write((byte)Main.iceBackStyle);
                        w.Write((byte)Main.jungleBackStyle);
                        w.Write((byte)Main.hellBackStyle);
                        w.Write(Main.windSpeedTarget);
                        w.Write((byte)Main.numClouds);
                        for (int k = 0; k < 3; k++)
                        {
                            w.Write(Main.treeX[k]);
                        }
                        for (int l = 0; l < 4; l++)
                        {
                            w.Write((byte)Main.treeStyle[l]);
                        }
                        for (int m = 0; m < 3; m++)
                        {
                            w.Write(Main.caveBackX[m]);
                        }
                        for (int n = 0; n < 4; n++)
                        {
                            w.Write((byte)Main.caveBackStyle[n]);
                        }
                        WorldGen.TreeTops.SyncSend(w);
                        w.Write(Main.maxRaining);
                        BitsByte bb2 = 0;
                        {
                            bb2[0] = WorldGen.shadowOrbSmashed;
                            bb2[1] = NPC.downedBoss1;
                            bb2[2] = NPC.downedBoss2;
                            bb2[3] = NPC.downedBoss3;
                            bb2[4] = Main.hardMode;
                            bb2[5] = NPC.downedClown;
                            bb2[6] = ssc;
                            bb2[7] = NPC.downedPlantBoss;
                        }
                        w.Write(bb2);
                        BitsByte bb3 = 0;
                        {
                            bb3[0] = NPC.downedMechBoss1;
                            bb3[1] = NPC.downedMechBoss2;
                            bb3[2] = NPC.downedMechBoss3;
                            bb3[3] = NPC.downedMechBossAny;
                            bb3[4] = Main.cloudBGActive >= 1f;
                            bb3[5] = WorldGen.crimson;
                            bb3[6] = Main.pumpkinMoon;
                            bb3[7] = Main.snowMoon;
                        }
                        w.Write(bb3);
                        BitsByte bb4 = 0;
                        {
                            bb4[1] = Main.fastForwardTimeToDawn;
                            bb4[2] = Main.slimeRain;
                            bb4[3] = NPC.downedSlimeKing;
                            bb4[4] = NPC.downedQueenBee;
                            bb4[5] = NPC.downedFishron;
                            bb4[6] = NPC.downedMartians;
                            bb4[7] = NPC.downedAncientCultist;
                        }
                        w.Write(bb4);
                        BitsByte bb5 = 0;
                        {
                            bb5[0] = NPC.downedMoonlord;
                            bb5[1] = NPC.downedHalloweenKing;
                            bb5[2] = NPC.downedHalloweenTree;
                            bb5[3] = NPC.downedChristmasIceQueen;
                            bb5[4] = NPC.downedChristmasSantank;
                            bb5[5] = NPC.downedChristmasTree;
                            bb5[6] = NPC.downedGolemBoss;
                            bb5[7] = BirthdayParty.PartyIsUp;
                        }
                        w.Write(bb5);
                        BitsByte bb6 = 0;
                        {
                            bb6[0] = NPC.downedPirates;
                            bb6[1] = NPC.downedFrost;
                            bb6[2] = NPC.downedGoblins;
                            bb6[3] = Sandstorm.Happening;
                            bb6[4] = DD2Event.Ongoing;
                            bb6[5] = DD2Event.DownedInvasionT1;
                            bb6[6] = DD2Event.DownedInvasionT2;
                            bb6[7] = DD2Event.DownedInvasionT3;
                        }
                        w.Write(bb6);
                        BitsByte bb7 = 0;
                        {
                            bb7[0] = NPC.combatBookWasUsed;
                            bb7[1] = LanternNight.LanternsUp;
                            bb7[2] = NPC.downedTowerSolar;
                            bb7[3] = NPC.downedTowerVortex;
                            bb7[4] = NPC.downedTowerNebula;
                            bb7[5] = NPC.downedTowerStardust;
                            bb7[6] = Main.forceHalloweenForToday;
                            bb7[7] = Main.forceXMasForToday;
                        }
                        w.Write(bb7);
                        BitsByte bb8 = 0;
                        {
                            bb8[0] = NPC.boughtCat;
                            bb8[1] = NPC.boughtDog;
                            bb8[2] = NPC.boughtBunny;
                            bb8[3] = NPC.freeCake;
                            bb8[4] = Main.drunkWorld;
                            bb8[5] = NPC.downedEmpressOfLight;
                            bb8[6] = NPC.downedQueenSlime;
                            bb8[7] = Main.getGoodWorld;
                        }
                        w.Write(bb8);
                        BitsByte bb9 = 0;
                        {
                            bb9[0] = Main.tenthAnniversaryWorld;
                            bb9[1] = Main.dontStarveWorld;
                            bb9[2] = NPC.downedDeerclops;
                            bb9[3] = Main.notTheBeesWorld;
                            bb9[4] = Main.remixWorld;
                            bb9[5] = NPC.unlockedSlimeBlueSpawn;
                            bb9[6] = NPC.combatBookVolumeTwoWasUsed;
                            bb9[7] = NPC.peddlersSatchelWasUsed;
                        }
                        w.Write(bb9);
                        BitsByte bb10 = 0;
                        {
                            bb10[0] = NPC.unlockedSlimeGreenSpawn;
                            bb10[1] = NPC.unlockedSlimeOldSpawn;
                            bb10[2] = NPC.unlockedSlimePurpleSpawn;
                            bb10[3] = NPC.unlockedSlimeRainbowSpawn;
                            bb10[4] = NPC.unlockedSlimeRedSpawn;
                            bb10[5] = NPC.unlockedSlimeYellowSpawn;
                            bb10[6] = NPC.unlockedSlimeCopperSpawn;
                            bb10[7] = Main.fastForwardTimeToDusk;
                        }
                        w.Write(bb10);
                        BitsByte bb11 = 0;
                        {
                            bb11[0] = Main.noTrapsWorld;
                            bb11[1] = Main.zenithWorld;
                        }
                        w.Write(bb11);
                        w.Write((byte)Main.sundialCooldown);
                        w.Write((byte)Main.moondialCooldown);
                        w.Write((short)WorldGen.SavedOreTiers.Copper);
                        w.Write((short)WorldGen.SavedOreTiers.Iron);
                        w.Write((short)WorldGen.SavedOreTiers.Silver);
                        w.Write((short)WorldGen.SavedOreTiers.Gold);
                        w.Write((short)WorldGen.SavedOreTiers.Cobalt);
                        w.Write((short)WorldGen.SavedOreTiers.Mythril);
                        w.Write((short)WorldGen.SavedOreTiers.Adamantite);
                        w.Write((sbyte)Main.invasionType);
                        if (SocialAPI.Network != null)
                        {
                            w.Write(SocialAPI.Network.GetLobbyId());
                        }
                        else
                        {
                            w.Write(0UL);
                        }
                        w.Write(Sandstorm.IntendedSeverity);
                        
                        ushort Length = (ushort)w.BaseStream.Position;
                        w.BaseStream.Position = 0;
                        w.Write(Length);
                    }
                    Player.SendRawData(s.ToArray());
                }
            }
        }
    }
}
