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
                database.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

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
                inv.name,
                inv.author,
                inv.isPrivate ? 1 : 0,
                JsonConvert.SerializeObject(inv.character),
                "[]"
                );
        }

        public static void UpdInventory(ref Inventory inv)
        {
            database.Query("UPDATE Inventories SET Character=@1 WHERE Name=@0",
                   inv.name,
                   JsonConvert.SerializeObject(inv.character)
                   );
        }

        public static Inventory Load(string name)
        {
            using (QueryResult r = database.QueryReader("SELECT * FROM Inventories WHERE Name=@0", name))
            {
                if (r.Read())
                {
                    return new Inventory()
                    {
                        name = r.Get<string>("Name"),
                        author = r.Get<string>("Author"),
                        isPrivate = r.Get<int>("Private") == 1,
                        character = JsonConvert.DeserializeObject<Character>(r.Get<string>("Character")),
                        usernames = JsonConvert.DeserializeObject<List<string>>(r.Get<string>("Usernames"))
                    };
                }
            }

            return new Inventory();
        }

        public static bool Delete(string name)
        {
            return database.Query("DELETE FROM Inventories WHERE Name = @0", name) != -1;
        }

        public static bool UpdPrivate(string name, bool isPrivate)
        {
            int result = database.Query("UPDATE Inventories SET Private=@1 WHERE Name=@0",
                name,
                isPrivate ? 1 : 0
                );

            return result > 0;
        }

        public static bool UpdUsernames(ref Inventory inv)
        {
            int result = database.Query("UPDATE Inventories SET Usernames=@1 WHERE Name=@0",
                inv.name,
                JsonConvert.SerializeObject(inv.usernames)
                );

            return result > 0;
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
                {
                    list.Add(r.Get<string>("Name"));
                }
            }

            return list;
        }
    }

    public struct Inventory
    {
        public Inventory(TSPlayer p, string name)
        {
            isPrivate = false;
            this.name = name;
            author = p.Account.Name;
            usernames = new List<string>();
            character = new Character(p.TPlayer);
        }

        public void Clear()
        {
            usernames.Clear();
            character.items.Clear();
        }

        public List<string> usernames;
        public Character character;
        public string name;
        public string author;
        public bool isPrivate;
    }

    public struct Character
    {
        public Character(Player p)
        {
            items = new List<ItemSlot>();
            skin = new Skin(p);
            short slot = -1;

            foreach (var i in p.inventory)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                    continue;

                items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }

            foreach (var i in p.armor)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                    continue;

                items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }

            foreach (var i in p.dye)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                    continue;

                items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }

            foreach (var i in p.miscEquips)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                    continue;

                items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }

            foreach (var i in p.miscDyes)
            {
                slot++;
                if (!i.active || i.netID == 0 || i.stack == 0)
                    continue;

                items.Add(new ItemSlot(slot, i.netID, i.stack, i.prefix));
            }
        }

        public void Send(TSPlayer p)
        {
            foreach (var i in items)
                i.Send(p);

            skin.Send(p);
        }

        public void Clear(TSPlayer p)
        {
            foreach (var i in items)
                new ItemSlot(i.slot, 0, 0, 0).Send(p);
        }

        public List<ItemSlot> items;
        public Skin skin;
    }

    public struct Skin
    {
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
            p.TPlayer.hairColor = HairC.GetColor();
            p.TPlayer.skinColor = SkinC.GetColor();
            p.TPlayer.eyeColor = EyeC.GetColor();
            p.TPlayer.shirtColor = ShirtC.GetColor();
            p.TPlayer.underShirtColor = UnderShirtC.GetColor();
            p.TPlayer.pantsColor = PantsC.GetColor();
            p.TPlayer.shoeColor = ShoeC.GetColor();

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
    }

    public struct ItemSlot
    {
        public ItemSlot(short slot, int netId, int stack, byte prefix)
        {
            this.slot = slot;
            this.netId = netId;
            this.stack = stack;
            this.prefix = prefix;
        }

        public void Send(TSPlayer p)
        {
            Item i = null;

            if (slot < NetItem.InventoryIndex.Item2)
            {
                //0-58
                i = p.TPlayer.inventory[slot];
            }
            else if (slot < NetItem.ArmorIndex.Item2)

            {
                //59-78
                i = p.TPlayer.armor[slot - NetItem.ArmorIndex.Item1];
            }
            else if (slot < NetItem.DyeIndex.Item2)

            {
                //79-88
                i = p.TPlayer.dye[slot - NetItem.DyeIndex.Item1];
            }
            else if (slot < NetItem.MiscEquipIndex.Item2)
            {
                //89-93
                i = p.TPlayer.miscEquips[slot - NetItem.MiscEquipIndex.Item1];
            }
            else if (slot < NetItem.MiscDyeIndex.Item2)
            {
                //93-98
                i = p.TPlayer.miscDyes[slot - NetItem.MiscDyeIndex.Item1];
            }

            if (i == null)
                return;

            i.netDefaults(netId);
            i.stack = stack;
            i.prefix = prefix;

            TSPlayer.All.SendData(PacketTypes.PlayerSlot, i.Name, p.Index, slot, prefix);
        }

        public short slot;
        public byte prefix;
        public int netId;
        public int stack;
    }

    public struct RGB
    {
        public RGB (Color c)
        {
            R = c.R;
            G = c.B;
            B = c.B;
        }

        public Color GetColor()
        {
            return new Color(R, G, B);
        }

        public byte R;
        public byte G;
        public byte B;
    }
}
