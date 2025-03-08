using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Data;
using System.IO;
using TShockAPI;
using TShockAPI.DB;
using Terraria;

namespace InventoryManager
{
    public static class Database
    {
        static IDbConnection database;

        public static void Connect()
        {
            Directory.CreateDirectory(Path.Combine(TShock.SavePath, "Inventories"));

            database = new SqliteConnection(string.Format("uri=file://{0},Version=3", Path.Combine(TShock.SavePath, "Inventories", "Inventories.sqlite")));

            SqlTableCreator sqlcreator = new SqlTableCreator(database,
                database.GetSqlType() == SqlType.Sqlite ? new SqliteQueryCreator() : new MysqlQueryCreator());

            sqlcreator.EnsureTableStructure(new SqlTable("Inventories",
                new SqlColumn("Name", MySqlDbType.String) { Unique = true },
                new SqlColumn("Author", MySqlDbType.String),
                new SqlColumn("Private", MySqlDbType.Int32),
                new SqlColumn("Usernames", MySqlDbType.String),
                new SqlColumn("Character", MySqlDbType.String)
                ));
        }

        public static void Disconnect()
        {
            database.Close();
        }

        public static void Save(ref Inventory inv)
        {
            database.Query("INSERT INTO Inventories (Name, Author, Private, Character, Usernames) VALUES (@0, @1, @2, @3, @4)",
                inv.Name,
                inv.Author,
                inv.IsPrivate ? 1 : 0,
                JsonConvert.SerializeObject(inv.Character),
                "[]"
                );
        }

        public static bool Delete(string name)
        {
            return database.Query("DELETE FROM Inventories WHERE Name = @0", name) != -1;
        }

        public static Inventory Load(string name)
        {
            using (QueryResult r = database.QueryReader("SELECT * FROM Inventories WHERE Name=@0", name))
            {
                if (r.Read())
                    return new Inventory()
                    {
                        Name = r.Get<string>("Name"),
                        Author = r.Get<string>("Author"),
                        IsPrivate = r.Get<int>("Private") == 1,
                        Character = JsonConvert.DeserializeObject<Character>(r.Get<string>("Character")),
                        Usernames = JsonConvert.DeserializeObject<List<string>>(r.Get<string>("Usernames"))
                    };
            }
            return new Inventory();
        }

        public static bool UpdatePrivate(string name, bool isPrivate)
        {
            int result = database.Query("UPDATE Inventories SET Private=@1 WHERE Name=@0",
                name,
                isPrivate ? 1 : 0
                );
            return result > 0;
        }

        public static bool UpdateUsernames(ref Inventory inv)
        {
            int result = database.Query("UPDATE Inventories SET Usernames=@1 WHERE Name=@0",
                inv.Name,
                JsonConvert.SerializeObject(inv.Usernames)
                );
            return result > 0;
        }

        public static void UpdateInventory(ref Inventory inv)
        {
            database.Query("UPDATE Inventories SET Character=@1 WHERE Name=@0",
                   inv.Name,
                   JsonConvert.SerializeObject(inv.Character)
                   );
        }

        public static List<string> GetList(string name, string author, bool? isPrivate, string username)
        {
            var list = new List<string>();

            if (name != null)
                list.Add($"INSTR(Name, '{name}') > 0");

            if (author != null)
                list.Add($"Author = '{author}'");

            if (isPrivate != null)
                list.Add($"Private = {(isPrivate.Value ? 1 : 0)}");

            if (username != null)
                list.Add($"INSTR(Usernames, '{username}') > 0");

            string condition = list.Count > 0 ? $" WHERE {string.Join(" OR ", list)}" : "";
            list.Clear();

            using (QueryResult r = database.QueryReader($"SELECT * FROM Inventories{condition}"))
            {
                while (r.Read())
                    list.Add(r.Get<string>("Name"));
            }
            return list;
        }
    }

    public struct Inventory
    {
        public List<string> Usernames;
        public Character Character;
        public string Name;
        public string Author;
        public bool IsPrivate;

        public Inventory(TSPlayer p, string name)
        {
            IsPrivate = false;
            Name = name;
            Author = p.Account.Name;
            Usernames = new List<string>();
            Character = new Character(p.TPlayer);
        }

        public bool HasPermission(TSPlayer player)
        {
            return !IsPrivate || Usernames.Contains(player.Account.Name) || CanManage(player);
        }

        public bool CanManage(TSPlayer player)
        {
            return Author == player.Account.Name || player.HasPermission("diogen.invmanager.admin");
        }

        public void Clear()
        {
            Usernames.Clear();
            Character.Items.Clear();
        }
    }

    public struct Character
    {
        public List<ItemSlot> Items;
        public Skin Skin;

        public Character(Player p)
        {
            Items = new List<ItemSlot>();
            Skin = new Skin(p);
            short slot = -1;

            foreach (var i in p.inventory)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                {
                    continue;
                }
                Items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }
            foreach (var i in p.armor)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                {
                    continue;
                }
                Items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }
            foreach (var i in p.dye)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                {
                    continue;
                }
                Items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }
            foreach (var i in p.miscEquips)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                {
                    continue;
                }
                Items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }
            foreach (var i in p.miscDyes)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                {
                    continue;
                }
                Items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }
        }

        public void Send(TSPlayer p)
        {
            foreach (var i in Items)
            {
                i.Send(p);
            }
            Skin.Send(p);
        }

        public void Clear(TSPlayer p)
        {
            foreach (var i in Items)
                new ItemSlot(i.Slot, 0, 0, 0).Send(p);
        }
    }

    public struct Skin
    {
        public byte Variant;
        public byte Hair;
        public byte HairDye;
        public byte HideV1;
        public byte HideV2;
        public byte HideMisc;
        public RGB HairC;
        public RGB SkinC;
        public RGB EyeC;
        public RGB ShirtC;
        public RGB UnderShirtC;
        public RGB PantsC;
        public RGB ShoeC;
        public short Mana;
        public short HP;

        public Skin(Player p)
        {
            Variant = (byte)p.skinVariant;
            Hair = (byte)p.hair;
            HairDye = p.hairDye;
            HideMisc = p.hideMisc;
            HairC = new RGB(p.hairColor);
            SkinC = new RGB(p.skinColor);
            EyeC = new RGB(p.eyeColor);
            ShirtC = new RGB(p.shirtColor);
            UnderShirtC = new RGB(p.underShirtColor);
            PantsC = new RGB(p.pantsColor);
            ShoeC = new RGB(p.shoeColor);

            var hideV = p.hideVisibleAccessory;
            HideV1 = new BitsByte(hideV[0], hideV[1], hideV[2], hideV[3], hideV[4], hideV[5], hideV[6], hideV[7]);
            HideV2 = new BitsByte(hideV[8], hideV[9]);

            HP = (short)p.statLifeMax;
            Mana = (short)p.statManaMax;
        }

        public void Send(TSPlayer p)
        {
            p.TPlayer.skinVariant = Variant;
            p.TPlayer.hair = Hair;
            p.TPlayer.hairDye = HairDye;
            p.TPlayer.hideMisc = HideMisc;
            p.TPlayer.hairColor = HairC.GetColor;
            p.TPlayer.skinColor = SkinC.GetColor;
            p.TPlayer.eyeColor = EyeC.GetColor;
            p.TPlayer.shirtColor = ShirtC.GetColor;
            p.TPlayer.underShirtColor = UnderShirtC.GetColor;
            p.TPlayer.pantsColor = PantsC.GetColor;
            p.TPlayer.shoeColor = ShoeC.GetColor;

            BitsByte hideV1 = HideV1, hideV2 = HideV2;
            for (byte i = 0; i < 10; i++)
                p.TPlayer.hideVisibleAccessory[i] = i < 8 ? hideV1[i] : hideV2[i - 8];

            TSPlayer.All.SendData(PacketTypes.PlayerInfo, null, p.Index);

            if (Mana > 0)
            {
                p.TPlayer.statManaMax = p.TPlayer.statMana = Mana;
                TSPlayer.All.SendData(PacketTypes.PlayerMana, null, p.Index);
            }
            if (HP > 0)
            {
                p.TPlayer.statLifeMax = p.TPlayer.statLife = HP;
                TSPlayer.All.SendData(PacketTypes.PlayerHp, null, p.Index);
            }
        }
    }

    public struct ItemSlot
    {
        public short Slot;
        public byte Prefix;
        public int NetID;
        public int Stack;

        public ItemSlot(short slot, int netID, int stack, byte prefix)
        {
            Slot = slot;
            NetID = netID;
            Stack = stack;
            Prefix = prefix;
        }

        public void Send(TSPlayer p)
        {
            Item i = null;
            if (Slot < NetItem.InventoryIndex.Item2)
            {
                //0-58
                i = p.TPlayer.inventory[Slot];
            }
            else if (Slot < NetItem.ArmorIndex.Item2)
            {
                //59-78
                i = p.TPlayer.armor[Slot - NetItem.ArmorIndex.Item1];
            }
            else if (Slot < NetItem.DyeIndex.Item2)
            {
                //79-88
                i = p.TPlayer.dye[Slot - NetItem.DyeIndex.Item1];
            }
            else if (Slot < NetItem.MiscEquipIndex.Item2)
            {
                //89-93
                i = p.TPlayer.miscEquips[Slot - NetItem.MiscEquipIndex.Item1];
            }
            else if (Slot < NetItem.MiscDyeIndex.Item2)
            {
                //93-98
                i = p.TPlayer.miscDyes[Slot - NetItem.MiscDyeIndex.Item1];
            }
            if (i == null)
            {
                return;
            }
            i.netDefaults(NetID);
            i.stack = Stack;
            i.prefix = Prefix;
            TSPlayer.All.SendData(PacketTypes.PlayerSlot, i.Name, p.Index, Slot, Prefix);
        }
    }

    public struct RGB
    {
        public byte R;
        public byte G;
        public byte B;

        public Color GetColor => new Color(R, G, B);

        public RGB (Color color)
        {
            R = color.R;
            G = color.G;
            B = color.B;
        }
    }
}
