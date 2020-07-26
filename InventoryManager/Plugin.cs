using System;
using TerrariaApi.Server;
using TShockAPI;
using Terraria;
using System.Collections.Generic;
using Terraria.GameContent.Events;
using Terraria.Social;
using System.Linq;
using System.IO;

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
            var backpack = Backpacks.Find(x => x.plr.Index == e.Who);

            if (backpack != null)
            {
                if (!Main.ServerSideCharacter)
                    SendWorldInfo(backpack.plr, true);

                backpack.Clear();

                if (!Main.ServerSideCharacter)
                    SendWorldInfo(backpack.plr, false);

                Backpacks.Remove(backpack);
            }
        }

        void Command(CommandArgs e)
        {
            if (e.Player.Account == null)
            {
                e.Player.SendErrorMessage("Log in to your account, please.");
                return;
            }

            switch (e?.Parameters?.Count < 1 ? "help" : e.Parameters[0])
            {
                case "load":
                    {
                        if (e.Parameters.Count < 2)
                            e.Player.SendInfoMessage("Syntax: /inv load Name");
                        else
                        {
                            var name = string.Join(" ", e.Parameters.Skip(1));
                            var inv = Database.Load(name);

                            if (inv.name == null)
                                e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                            else if (inv.isPrivate && inv.author != e.Player.Account.Name && !inv.usernames.Contains(e.Player.Account.Name) && !e.Player.HasPermission("diogen.invmanager.admin"))
                                e.Player.SendErrorMessage($"You do not have permission to load this inventory.");
                            else
                            {
                                var backpack = Backpacks.Find(x => x.plr == e.Player);

                                if (backpack == null)   
                                    Backpacks.Add(backpack = new Backpack(ref inv, e.Player));
                                else
                                    backpack.newInv = inv;

                                if (!Main.ServerSideCharacter)
                                    SendWorldInfo(backpack.plr, true);

                                backpack.Send();

                                if (!Main.ServerSideCharacter)
                                    SendWorldInfo(backpack.plr, false);

                                e.Player.SendSuccessMessage($"Inventory \"{name}\" successfully loaded!");
                            }
                        }
                        break;
                    }
                case "save":
                    {
                        if (e.Parameters.Count < 2)
                            e.Player.SendInfoMessage("Syntax: /inv save Name");
                        else
                        {
                            var name = string.Join(" ", e.Parameters.Skip(1));
                            var inv = Database.Load(name);

                            if (inv.name == null)
                            {
                                inv = new Inventory(e.Player, name);
                                Database.Save(ref inv);
                            }
                            else
                            {
                                if (inv.author != e.Player.Account.Name && !e.Player.HasPermission("diogen.invmanager.admin"))
                                {
                                    e.Player.SendErrorMessage($"You do not have permission to change this inventory.");
                                    return;
                                }

                                inv.character = new Character(e.TPlayer);
                                Database.UpdInventory(ref inv);
                            }
                            
                            e.Player.SendSuccessMessage($"Inventory \"{name}\" successfully saved!");
                        }
                        break;
                    }
                case "priv":
                    {
                        if (e.Parameters.Count < 3 || (e.Parameters[1] != "on" && e.Parameters[1] != "off"))
                            e.Player.SendInfoMessage("Syntax: /inv priv [on|off] Name");
                        else
                        {
                            var name = string.Join(" ", e.Parameters.Skip(2));
                            bool isPrivate = e.Parameters[1] == "on";
                            var inv = Database.Load(name);

                            if (inv.name == null)
                                e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                            else if (inv.author != e.Player.Account.Name && !e.Player.HasPermission("diogen.invmanager.admin"))
                                e.Player.SendErrorMessage($"You do not have permission to change this inventory");
                            if (Database.UpdPrivate(name, isPrivate))
                                e.Player.SendSuccessMessage($"The status of the inventory \"{name}\" changed: {(isPrivate ? "private" : "public")}.");
                        }
                        break;
                    }
                case "allow": 
                    {
                        if (e.Parameters.Count < 3)
                            e.Player.SendInfoMessage("Syntax: /inv allow Username Name");
                        else
                        {
                            var username = e.Parameters[1];

                            if (TShock.UserAccounts.GetUserAccountByName(username) == null)
                                e.Player.SendErrorMessage($"User \"{username}\" not found.");
                            else
                            {
                                var name = string.Join(" ", e.Parameters.Skip(2));
                                var inv = Database.Load(name);

                                if (inv.name == null)
                                    e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                                else if (inv.author != e.Player.Account.Name && !e.Player.HasPermission("diogen.invmanager.admin"))
                                    e.Player.SendErrorMessage($"You do not have permission to change this inventory");
                                else if (Database.AllowUser(username, ref inv))
                                    e.Player.SendSuccessMessage("Done!");
                            }
                        }
                        break;
                    }
                case "del":
                    {
                        if (e.Parameters.Count < 2)
                            e.Player.SendInfoMessage("Syntax: /inv del Name");
                        else
                        {
                            var name = string.Join(" ", e.Parameters.Skip(1));
                            var inv = Database.Load(name);

                            if (inv.name == null)
                                e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                            else if (inv.author != e.Player.Account.Name && !e.Player.HasPermission("diogen.invmanager.admin"))
                                e.Player.SendErrorMessage($"You do not have permission to delete this inventory.");
                            if (Database.Delete(name))
                                e.Player.SendSuccessMessage($"Inventory \"{name}\" successfully deleted!");
                        }
                        break;
                    }
                case "rest":
                    {
                        var backpack = Backpacks.Find(x => x.plr == e.Player);

                        if (backpack != null)
                        {
                            if (!Main.ServerSideCharacter)
                                SendWorldInfo(backpack.plr, true);

                            backpack.Clear();

                            if (!Main.ServerSideCharacter)
                                SendWorldInfo(backpack.plr, false);

                            Backpacks.Remove(backpack);
                            e.Player.SendSuccessMessage("Your inventory is back!");
                        }
                        else
                            e.Player.SendErrorMessage("Your backpack not found.");
                        break;
                    }
                case "list":
                    {
                        string name = null, author = null, username = null;
                        var tags = new List<string>();
                        bool? isPrivate = null;
                        int page = 1;

                        if (e.Parameters.Count > 1)
                        {
                            for (int i = 1; i < e.Parameters.Count; i++)
                            {
                                string s = e.Parameters[i];

                                if (s.StartsWith("name:"))
                                {
                                    if (s.Length > 5)
                                        name = s.Split(':')[1];
                                }
                                else if (s.StartsWith("author:"))
                                {
                                    if (s.Length > 7)
                                        author = s.Split(':')[1];
                                }
                                else if (s.StartsWith("user:"))
                                {
                                    if (s.Length > 5)
                                        username = s.Split(':')[1];
                                }
                                if (s.StartsWith("name(_):"))
                                {
                                    if (s.Length > 8)
                                        name = s.Split(':')[1].Replace('_', ' ');
                                }
                                else if (s.StartsWith("author(_):"))
                                {
                                    if (s.Length > 10)
                                        author = s.Split(':')[1].Replace('_', ' ');
                                }
                                else if (s.StartsWith("user(_):"))
                                {
                                    if (s.Length > 8)
                                        username = s.Split(':')[1].Replace('_', ' ');
                                }
                                else if (s == "-public")
                                    isPrivate = false;
                                else if (s == "-private")
                                    isPrivate = true;
                                else
                                    int.TryParse(s, out page);
                            }

                            if (name != null)
                                tags.Add($"called like {name}");

                            if (author != null)
                                tags.Add($"created by {author}");

                            if (isPrivate != null)
                                tags.Add(isPrivate.Value ? "private" : "public");

                            if (username != null)
                                tags.Add($"shared with {username}");
                        }

                        var list = Database.GetList(name, author, isPrivate, username);

                        PaginationTools.SendPage(e.Player, page, PaginationTools.BuildLinesFromTerms(list),
                            new PaginationTools.Settings
                            {
                                HeaderFormat = $"Inventories {string.Join(", ", tags)} ({{0}}/{{1}}):",
                                FooterFormat = $"Type /inv list {string.Join(" ", e.Parameters.Take(e.Parameters.Count - 2))} {{0}} for more.",
                                NothingToDisplayString = "Such inventories don't exist in this universe!"
                            });
                        break;
                    }
                case "info":
                    {
                        if (e.Parameters.Count < 2)
                            e.Player.SendInfoMessage("Syntax: /inv load Name");
                        else
                        {
                            var name = string.Join(" ", e.Parameters.Skip(1));
                            var inv = Database.Load(name);
                            
                            if (inv.name == null)
                                e.Player.SendErrorMessage($"Inventory \"{name}\" not found.");
                            else
                            {
                                e.Player.SendInfoMessage($" — — Inventory \"{name}\" info — —");
                                e.Player.SendInfoMessage($"Author: {inv.author}");
                                e.Player.SendInfoMessage($"Status: {(inv.isPrivate ? "private" : "public")}");
                                e.Player.SendInfoMessage($"Shared with: {(inv.usernames.Count < 1 ? "-" : string.Join(", ", inv.usernames))}");
                                e.Player.SendInfoMessage($"Count of occupied slots: {inv.character.items.Count}");
                            }
                        }
                        break;
                    }
                case "help":
                    {
                        e.Player.SendInfoMessage("Syntax: /inv [load|save|del|allow|rest|priv|info|list] arguments");
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
            public Backpack(ref Inventory newInv, TSPlayer plr)
            {
                oldInv = new Character(plr.TPlayer);
                this.newInv = newInv;
                this.plr = plr;
            }

            public void Send()
            {
                oldInv.Clear(plr);
                newInv.character.Send(plr);
            }

            public void Clear()
            {
                new Character(plr.TPlayer).Clear(plr);
                oldInv.Send(plr);

                oldInv.items.Clear();
                newInv.Clear();
            }

            public Character oldInv;
            public Inventory newInv;
            public TSPlayer plr;
        }

        public static void SendWorldInfo(TSPlayer plr, bool ssc)
        {
            using (var s = new MemoryStream())
            {
                using (var w = new BinaryWriter(s))
                {
                    w.BaseStream.Position = 2;
                    w.Write((byte)PacketTypes.WorldInfo);

                    w.Write((int)Main.time);
                    w.Write(new BitsByte(Main.dayTime, Main.bloodMoon, Main.eclipse));
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

                    int i = 0;
                    for (i = 0; i < 3; i++)
                        w.Write(Main.treeX[i]);
                    for (i = 0; i < 4; i++)
                        w.Write((byte)Main.treeStyle[i]);
                    for (i = 0; i < 3; i++)
                        w.Write(Main.caveBackX[i]);
                    for (i = 0; i < 4; i++)
                        w.Write((byte)Main.caveBackStyle[i]);

                    WorldGen.TreeTops.SyncSend(w);
                    if (!Main.raining)
                        Main.maxRaining = 0f;
                    w.Write(Main.maxRaining);

                    w.Write(new BitsByte(WorldGen.shadowOrbSmashed, NPC.downedBoss1, NPC.downedBoss2, NPC.downedBoss3, Main.hardMode, NPC.downedClown, ssc, NPC.downedPlantBoss));
                    w.Write(new BitsByte(NPC.downedMechBoss1, NPC.downedMechBoss2, NPC.downedMechBoss3, NPC.downedMechBossAny, Main.cloudBGActive >= 1f, WorldGen.crimson, Main.pumpkinMoon, Main.snowMoon));
                    w.Write(new BitsByte(Main.fastForwardTime, Main.slimeRain, NPC.downedSlimeKing, NPC.downedQueenBee, NPC.downedFishron, NPC.downedMartians, NPC.downedAncientCultist));
                    w.Write(new BitsByte(NPC.downedMoonlord, NPC.downedHalloweenKing, NPC.downedHalloweenTree, NPC.downedChristmasIceQueen, NPC.downedChristmasSantank, NPC.downedChristmasTree, NPC.downedGolemBoss, BirthdayParty.PartyIsUp));
                    w.Write(new BitsByte(NPC.downedPirates, NPC.downedFrost, NPC.downedGoblins, Sandstorm.Happening, DD2Event.Ongoing, DD2Event.DownedInvasionT1, DD2Event.DownedInvasionT2, DD2Event.DownedInvasionT3));
                    w.Write(new BitsByte(NPC.combatBookWasUsed, LanternNight.LanternsUp, NPC.downedTowerSolar, NPC.downedTowerVortex, NPC.downedTowerNebula, NPC.downedTowerStardust, Main.forceHalloweenForToday, Main.forceXMasForToday));
                    w.Write(new BitsByte(NPC.boughtCat, NPC.boughtDog, NPC.boughtBunny, NPC.freeCake, Main.drunkWorld, NPC.downedEmpressOfLight, NPC.downedQueenSlime, Main.getGoodWorld));

                    w.Write((short)WorldGen.SavedOreTiers.Copper);
                    w.Write((short)WorldGen.SavedOreTiers.Iron);
                    w.Write((short)WorldGen.SavedOreTiers.Silver);
                    w.Write((short)WorldGen.SavedOreTiers.Gold);
                    w.Write((short)WorldGen.SavedOreTiers.Cobalt);
                    w.Write((short)WorldGen.SavedOreTiers.Mythril);
                    w.Write((short)WorldGen.SavedOreTiers.Adamantite);
                    w.Write((sbyte)Main.invasionType);

                    if (SocialAPI.Network != null)
                        w.Write(SocialAPI.Network.GetLobbyId());
                    else
                        w.Write(0UL);
                    w.Write(Sandstorm.IntendedSeverity);

                    short Length = (short)w.BaseStream.Position;
                    w.BaseStream.Position = 0;
                    w.Write(Length);
                }

                plr.SendRawData(s.ToArray());
            }
        }
    }
}
