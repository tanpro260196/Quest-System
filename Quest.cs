using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using TShockAPI;
using Terraria;
using Newtonsoft.Json;
using TerrariaApi.Server;
using Wolfje.Plugins.SEconomy;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using TShockAPI.DB;
using System.Data;
using Terraria.Localization;
using Microsoft.Xna.Framework;
using System.Text.RegularExpressions;

namespace Quest
{
    [ApiVersion(2, 1)]
    public class Quest : TerrariaPlugin
    {
        public static IDbConnection QuestDB;
        private Config config;
        public override Version Version
        {
            get { return new Version("1.0.0.0"); }
        }
        public override string Name
        {
            get { return "Quest System"; }
        }
        public override string Author
        {
            get { return "BMS"; }
        }
        public override string Description
        {
            get { return "Quest monitoring system. Require SEconomy."; }
        }
        public Quest(Main game) : base(game)
        {
            Order = 1;
        }
        public override void Initialize()
        {
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
            TShockAPI.Commands.ChatCommands.Add(new Command("quest.use", Quest_return, "quest", "q"));
            TShockAPI.Commands.ChatCommands.Add(new Command("quest.use", questsearch, "questsearch", "qs"));
            TShockAPI.Commands.ChatCommands.Add(new Command("quest.admin", ForceUpdate, "forcequestupdate", "fqu"));
            TShockAPI.Commands.ChatCommands.Add(new Command("quest.admin", ReloadConfig, "reloadquest", "rq"));
            ReadConfig();

            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] host = TShock.Config.MySqlHost.Split(':');
                    QuestDB = new MySqlConnection()
                    {
                        ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                            host[0],
                            host.Length == 1 ? "3306" : host[1],
                            TShock.Config.MySqlDbName,
                            TShock.Config.MySqlUsername,
                            TShock.Config.MySqlPassword)
                    };
                    break;
                case "sqlite":
                    string sql = Path.Combine(TShock.SavePath, "Quest.sqlite");
                    QuestDB = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
                    break;
            }
            SqlTableCreator sqlcreator = new SqlTableCreator(QuestDB,
                QuestDB.GetSqlType() == SqlType.Sqlite ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());
            sqlcreator.EnsureTableStructure(new SqlTable("QuestCount",
               new SqlColumn("ID", MySqlDbType.Int32) { Unique = true, Primary = true, AutoIncrement = true },
               new SqlColumn("QuestName", MySqlDbType.String, 200),
               new SqlColumn("Accounts", MySqlDbType.String, 100),
               new SqlColumn("Status", MySqlDbType.String, 100),
               new SqlColumn("LastRefresh", MySqlDbType.String, 200)));

            sqlcreator.EnsureTableStructure(new SqlTable("QuestHistory",
               new SqlColumn("Time", MySqlDbType.String, 200),
               new SqlColumn("Account", MySqlDbType.VarChar) { Length = 50 },
               new SqlColumn("QuestName", MySqlDbType.String, 100),
               new SqlColumn("WorldID", MySqlDbType.Int32),
               new SqlColumn("Reward", MySqlDbType.String, 100)));

            UpdateDB();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            }
            base.Dispose(disposing);
        }
        #region DailyCheck
        private DateTime LastCheck2 = DateTime.UtcNow;
        private Dictionary<string, int> TimeLeft = new Dictionary<string, int>();
        private void OnUpdate(EventArgs args)
        {
            if ((DateTime.UtcNow - LastCheck2).TotalSeconds >= config.minrefreshsecond)
            {
                LastCheck2 = DateTime.UtcNow;
                DailyUpdate();
            }
        }
        public void DailyUpdate()
        {
            TShock.Utils.Broadcast("[Quest System] Generating new quest list. Lag may result from this.", Color.LightBlue);
            Random rnd = new Random();
            int quest_quantity = rnd.Next(config.min_avail_quest, config.max_avail_quest);
            int i = 0;
            while (i < quest_quantity)
            {
                i = 0;
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {
                    while (reader.Read())
                    {
                        int refreshint = 0;
                        foreach (var questitem in config.All)
                        {
                            if (questitem.DisplayName == reader.Get<string>("QuestName"))
                            {
                                refreshint = questitem.refreshtime;
                            }
                        }
                        int timepassed = Convert.ToInt32((DateTime.UtcNow - DateTime.Parse(reader.Get<string>("LastRefresh"))).TotalSeconds);
                        if ((timepassed >= (refreshint * config.minrefreshsecond)))
                        {
                            var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Disabled", reader.Get<int>("ID"));
                            var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, reader.Get<int>("ID"));
                            var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.UtcNow, reader.Get<int>("ID"));
                        }
                    }
                }
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {
                    while (reader.Read())
                    {
                        if (reader.Get<string>("Status") == "Enabled")
                        {
                            i++;
                        }
                    }
                }
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {
                    while ((reader.Read()) && (i < quest_quantity))
                    {
                        bool hmcheck = false;
                        int refreshint = 0;
                        foreach (var questitem in config.All)
                        {
                            if (questitem.DisplayName == reader.Get<string>("QuestName"))
                            {
                                hmcheck = questitem.hardmode;
                                refreshint = questitem.refreshtime;
                            }
                        }
                        if (Main.hardMode)
                        {
                            if (((rnd.Next(1, 100) >= config.percent_not_choosen)) && (reader.Get<string>("Status") == "Disabled"))
                            {
                                i++;
                                var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Enabled", reader.Get<int>("ID"));
                                var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, reader.Get<int>("ID"));
                                var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.UtcNow, reader.Get<int>("ID"));
                            }

                        }
                        else if (!Main.hardMode)
                        {
                            if (((rnd.Next(1, 100) >= config.percent_not_choosen)) && (reader.Get<string>("Status") == "Disabled"))
                            {
                                i++;
                                var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Enabled", reader.Get<int>("ID"));
                                var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, reader.Get<int>("ID"));
                                var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.UtcNow, reader.Get<int>("ID"));
                            }
                        }
                    }
                }
            }
            TShock.Utils.Broadcast("[Quest System] Available Quest List Has Changed. Please check which Quest is available today using: /quest list.", Color.LightBlue);
            TShock.Utils.Broadcast("[Quest System] Total Number of Quests Available today is: " + Convert.ToString(i) + ".", Color.LightBlue);
        }
        private void ForceUpdate(CommandArgs args)
        {
            TShock.Utils.Broadcast("[Quest System] Generating new quest list. Lag may result from this.", Color.LightBlue);
            Random rnd = new Random();
            int quest_quantity = rnd.Next(config.min_avail_quest, config.max_avail_quest);
            int i = 0;
            while (i < quest_quantity)
            {
                i = 0;
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {
                    while (reader.Read())
                    {
                        int refreshint = 0;
                        foreach (var questitem in config.All)
                        {
                            if (questitem.DisplayName == reader.Get<string>("QuestName"))
                            {
                                refreshint = questitem.refreshtime;
                            }
                        }
                        int timepassed = Convert.ToInt32((DateTime.UtcNow - DateTime.Parse(reader.Get<string>("LastRefresh"))).TotalSeconds);
                        if ((timepassed >= refreshint * config.minrefreshsecond))
                        {
                            var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Disabled", reader.Get<int>("ID"));
                            var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, reader.Get<int>("ID"));
                            var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.UtcNow, reader.Get<int>("ID"));
                        }
                    }
                }
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {
                    while (reader.Read())
                    {
                        if (reader.Get<string>("Status") == "Enabled")
                        {
                            i++;
                        }
                    }
                }
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {
                    while ((reader.Read()) && (i < quest_quantity))
                    {
                        bool hmcheck = false;
                        int refreshint = 0;
                        foreach (var questitem in config.All)
                        {
                            if (questitem.DisplayName == reader.Get<string>("QuestName"))
                            {
                                hmcheck = questitem.hardmode;
                                refreshint = questitem.refreshtime;
                            }
                        }
                        if (Main.hardMode)
                        {
                            if (((rnd.Next(1, 100) >= config.percent_not_choosen)) && (reader.Get<string>("Status") == "Disabled"))
                            {
                                i++;
                                var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Enabled", reader.Get<int>("ID"));
                                var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, reader.Get<int>("ID"));
                                var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.UtcNow, reader.Get<int>("ID"));
                            }

                        }
                        else if (!Main.hardMode)
                        {
                            if (((rnd.Next(1, 100) >= config.percent_not_choosen)) && (reader.Get<string>("Status") == "Disabled"))
                            {
                                i++;
                                var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Enabled", reader.Get<int>("ID"));
                                var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, reader.Get<int>("ID"));
                                var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.UtcNow, reader.Get<int>("ID"));
                            }
                        }
                    }
                }
            }
            TShock.Utils.Broadcast("[Quest System] Available Quest List Has Changed. Please check which Quest is available today using: /quest list.", Color.LightBlue);
            TShock.Utils.Broadcast("[Quest System] Total Number of Quests Available today is: " + Convert.ToString(i) + ".", Color.LightBlue);  
        }
        #endregion
        #region misc
        private static string ItemToTag(SimpleItem args)
        {
            string ret = ((args.prefix != 0) ? "[i/p" + args.prefix : "[i");
            ret = (args.stack != 1) ? ret + "/s" + args.stack : ret;
            ret = ret + ":" + args.netID + "]";
            if (args.netID == 0) return "";
            return ret;
        }
        private static string itemtotag(int stack, int id, int prefix)
        {

            string ret = ((prefix != 0) ? "[i/p" + prefix : "[i");
            ret = (stack != 1) ? ret + "/s" + stack : ret;
            ret = ret + ":" + id + "]";
            if (id == 0) return "";
            return ret;
        }
        private void UpdateDB()
        {
            foreach (var questitem in config.All)
            {
                bool exist = false;
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {

                    while (reader.Read())
                    {
                        if (questitem.DisplayName == reader.Get<string>("QuestName"))
                        {

                            exist = true;

                        };
                    }
                }
                if (exist == false)
                {
                    var add = QuestDB.Query("INSERT INTO QuestCount (QuestName, Status, LastRefresh) VALUES (@0, @1, @2);", questitem.DisplayName, "Disabled", DateTime.UtcNow);
                }
            }
        }
        #endregion
        #region checkDB
        private string ReadDB(string QuestName)
        {
            string listaccounts = "";
            using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
            {

                while (reader.Read())
                {
                    if (QuestName == reader.Get<string>("QuestName"))
                    {
                        listaccounts = reader.Get<string>("Accounts");
                    }
                }
            }
            return listaccounts;
        }
        private bool GetEnabledStatus(string QuestName)
        {
            using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
            {
                while (reader.Read())
                {
                    if (QuestName == reader.Get<string>("QuestName"))
                    {
                        if ((reader.Get<string>("Status") == "Enabled"))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        private int GetID(string QuestName)
        {
            int ID = -1;
            using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
            {

                while (reader.Read())
                {
                    if (QuestName == reader.Get<string>("QuestName"))
                    {
                        ID = reader.Get<int>("ID");
                    }
                }
            }
            return ID;
        }
        private string GetLastCheck(string QuestName)
        {
            string time = null;
            using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
            {

                while (reader.Read())
                {
                    if (QuestName == reader.Get<string>("QuestName"))
                    {
                        time = reader.Get<string>("LastRefresh");
                    }
                }
            }
            return time;
        }
        private int CheckCompletion(int userID, string QuestName)
        {
            int totalcount = 0;
            using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
            {
                while (reader.Read())
                {
                    if ((reader.Get<string>("QuestName") == QuestName))
                    {
                        string listaccount = (reader.Get<string>("Accounts"));
                        if (listaccount != null)
                        {
                            totalcount = Regex.Matches(listaccount, Convert.ToString(userID)).Count;
                        }
                        return totalcount;
                    }
                }
            }
            return -1;
        }
        private bool checkregion(TSPlayer ply, string regionname)
        {
            if (regionname == null)
            {
                return true;
            }
            else if ((TShock.Regions.GetRegionByName(regionname) == null))
            {
                ply.SendMessage("Invalid region!", Color.LightBlue);
                return false;
            }
            Region region = TShock.Regions.GetRegionByName(regionname);
            if (region.InArea(ply.TileX, ply.TileY))
            {
                return true;
            }
            return false;

        }
        #endregion
        private void questsearch(CommandArgs args)
        {
            if ((args.Parameters.Count < 1))
            {
                args.Player.SendMessage("Search for a Quest: /questsearch <search term> or /qs <search term>", Color.LightBlue);

                return;
            }
            int number = args.Parameters.Count;
            string medname = args.Parameters[0];
            int indexlocation = 1;

            for (int i = 1; i < number; i++)
            {
                int checknumber = 0;
                if (!Int32.TryParse(args.Parameters[1], out checknumber))
                {
                    medname = medname + " " + args.Parameters[i];
                }
                if (Int32.TryParse(args.Parameters[1], out checknumber))
                {
                    indexlocation = i;
                }
            }
            int pageNumber = 0;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, indexlocation, args.Player, out pageNumber))
                return;
            var lines = new List<string> { };
            bool foundsth = false;
            foreach (var questlist in config.All)
            {
                if (questlist.DisplayName.ToLower().Contains(medname.ToLower()) && GetEnabledStatus(questlist.DisplayName))
                {
                    int ID = GetID(questlist.DisplayName);
                    foundsth = true;
                    DateTime lastcheck = DateTime.Parse(GetLastCheck(questlist.DisplayName));
                    int refreshint = questlist.refreshtime;
                    int timeleft = Convert.ToInt32((refreshint * config.minrefreshsecond - (DateTime.UtcNow - lastcheck).TotalSeconds) / config.minrefreshsecond);
                    if (timeleft <= 0)
                    {
                        timeleft = 0;
                        QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Disabled", ID);

                    }
                    else if (timeleft <= 1)
                    { timeleft = 1; }
                    int remain = questlist.maxredeem - CheckCompletion(args.Player.User.ID, questlist.DisplayName);
                    string total = "* ID: " + ID + " - ";
                    string left = "";
                    double reward = config.questmultiplier * questlist.Reward;
                    if (questlist.maxredeem == -1)
                    {
                        left = "∞";
                    }
                    if (questlist.Reward != -1)
                    {
                        left = Convert.ToString(remain);
                    }

                    total = total + "Redeem(s) [Remained|Max]: [" + left + "|" + questlist.maxredeem + "] - Expired in " + timeleft + " day(s) - " + questlist.DisplayName + " - Reward: " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(Math.Ceiling(reward))) + " - ";
                    foreach (var item in questlist.IncludeItems)
                    {
                        total = total + ItemToTag(item);
                    }

                    if (args.Player.Group.HasPermission(questlist.RequirePermission))
                    {
                        lines.Add(total);
                    }
                    else if (!config.HideUnavailableQuests)
                    {
                        string perm = args.Player.Group.HasPermission(questlist.RequirePermission) ? "[Available]" : "[Shortage]";
                        lines.Add(perm + total);
                    }
                }
            }
            PaginationTools.SendPage(args.Player, pageNumber, lines,
                                         new PaginationTools.Settings
                                         {
                                             HeaderFormat = "Search result for \"" + medname + "\" ({0}/{1}):",
                                             FooterFormat = "Type /qs " + medname + " {{0}} for more.".SFormat(Commands.Specifier),
                                             MaxLinesPerPage = 9
                                         }
                                        );
            if (!foundsth)
            {
                args.Player.SendMessage("We didn't find anything, use /quest list to search manually?", Color.LightBlue);
            }
        }
        private void Quest_return(CommandArgs args)
        {
            if ((args.Parameters.Count < 1))
            {
                args.Player.SendMessage("Redeem a Quest: /quest <quest ID>", Color.LightBlue);
                args.Player.SendMessage("Quest List: /quest list or /quest l", Color.LightBlue);
                args.Player.SendMessage("Search for a Quest: /questsearch <search term> or /qs <search term>", Color.LightBlue);
                return;
            }
            if ((args.Parameters[0] == "list") || (args.Parameters[0] == "l"))
            {
                int pageNumber = 1;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                    return;
                var lines = new List<string>{};
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {
                    var item_in_config = new QuestsEntry();
                    var lastcheck = new DateTime();
                    int refreshint = 0;
                    while (reader.Read())
                    {
                        if (reader.Get<string>("Status") == "Enabled")
                        {
                            foreach (var questitem in config.All)
                            {
                                if (questitem.DisplayName == reader.Get<string>("QuestName"))
                                {
                                    item_in_config = questitem;
                                    lastcheck = DateTime.Parse(reader.Get<string>("LastRefresh"));
                                    refreshint = questitem.refreshtime;
                                }
                            }
                            double timeleft = ((refreshint * config.minrefreshsecond - (DateTime.UtcNow - lastcheck).TotalSeconds) / config.minrefreshsecond);
                            if (timeleft <=0)
                            {
                                timeleft = 0;
                                QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Disabled", reader.Get<int>("ID"));

                            }
                            else if (timeleft <=1)
                            { timeleft = 1; }
                            string listaccount = (reader.Get<string>("Accounts"));
                            int totalcount = 0;
                            string left = null;
                            double reward = config.questmultiplier * item_in_config.Reward;
                            if (listaccount != null)
                            {
                                totalcount = Regex.Matches(listaccount, Convert.ToString(args.Player.User.ID)).Count;
                            }
                            if (item_in_config.maxredeem == -1)
                            {
                                left = "∞";
                            }
                            else
                            {
                                left = Convert.ToString(item_in_config.maxredeem - totalcount);
                            }
                            string newline = "* ID: " + reader.Get<int>("ID") +" - [" + left + " redeem(s) left] - ["+ Math.Ceiling(timeleft) + " day(s) left] - Reward: " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(Math.Ceiling(reward))) + " - ";
                            foreach (var item in item_in_config.IncludeItems)
                            {
                                newline = newline + ItemToTag(item);
                            }
                            newline = newline + " - Quest Name: " + reader.Get<string>("QuestName");
                            if (args.Player.Group.HasPermission(item_in_config.RequirePermission))
                            {
                                lines.Add(newline);
                            }
                        }
                    }
                }
                PaginationTools.SendPage(args.Player, pageNumber, lines,
                                         new PaginationTools.Settings
                                         {
                                             HeaderFormat = "Quest Menu ({0}/{1}):",
                                             FooterFormat = "Type {0}quest l {{0}} for more options.".SFormat(Commands.Specifier),
                                             MaxLinesPerPage = 9
                                         }
                                        );
                return;
            }
            int testint = 0;
            if ((args.Parameters[0] != "l") && (args.Parameters[0] != "list") && (!int.TryParse(args.Parameters[0], out testint)))
            {
                args.Player.SendMessage("Incorrect Syntax. Try again", Color.LightBlue);
                return;
            }
            int id = Convert.ToInt32(args.Parameters[0]);
            bool found = false;
            string accountlist_buy = "";
            string questname = "";
            using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
            {
                while (reader.Read())
                {
                    if (reader.Get<int>("ID") == id) 
                    {
                        found = true;
                        accountlist_buy = reader.Get<string>("Accounts");
                        questname = reader.Get<string>("QuestName");
                    }
                }
            }
            if ((!found))
            {
                args.Player.SendMessage("Incorrect ID. Try again", Color.LightBlue);
                return;
            }
            if (!GetEnabledStatus(questname))
            {
                args.Player.SendMessage("This quest is not available today. Please come back later.", Color.LightBlue);
                return;
            }
            int time_completed = CheckCompletion(args.Player.User.ID, questname);
            var thingneedtotake = new QuestsEntry();
            foreach (var i1 in config.All)
            {
                if (i1.DisplayName ==questname)
                {
                    thingneedtotake = i1;
                }
            }
            if (!args.Player.Group.HasPermission(thingneedtotake.RequirePermission))
            {
                args.Player.SendMessage("You are not allow to do this Quest.", Color.LightBlue);
                return;
            }
            if ((thingneedtotake.maxredeem != -1) && (time_completed >= thingneedtotake.maxredeem))
            {
                args.Player.SendMessage("Maximum number of completion reached. You cannot do this quest anymore.", Color.LightBlue);
                return;
            }
            if ((!checkregion(args.Player, config.questregion)))
            {
                args.Player.SendMessage("You are not in the Quest region.", Color.LightBlue);
                return;
            }

         

            var UsernameBankAccount = SEconomyPlugin.Instance.GetBankAccount(args.Player.Name);
            var playeramount = UsernameBankAccount.Balance;
            Money amount = thingneedtotake.Reward;
            Money amount2 = -thingneedtotake.Reward;
            var amount3 = Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(-amount2));
            var Journalpayment = Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToReceiver;

            if (args.Player == null || UsernameBankAccount == null)
            {
                args.Player.SendMessage("Can't find the account for " + args.Player.Name + ".", Color.LightBlue);
                return;
            }
            bool exist = false;
            foreach (var item_in_config in thingneedtotake.IncludeItems)
            {
                bool exist2 = false;
                var items_in_inventory = new Item();
                int prefix_in_config = item_in_config.prefix;
                
                for (int i = 0; i < 50; i++)
                {
                    items_in_inventory = args.Player.TPlayer.inventory[i];
                    //If prefix = 0 in config, ignore prefix.
                    if (item_in_config.prefix == 0)
                    {
                        prefix_in_config = items_in_inventory.prefix;
                    }
                    // Loops through the player's inventory
                    if ((items_in_inventory.netID == item_in_config.netID) && (items_in_inventory.stack >= item_in_config.stack) && items_in_inventory.prefix == prefix_in_config)
                    {
                        exist = true;
                        exist2 = true;
                        break;
                    }
                }
                if (!exist2)
                {
                    args.Player.SendMessage("You don't have the required item "+ ItemToTag(item_in_config) + " in your inventory!",Color.LightCyan );
                    return;
                }
            }

        

            if (exist == true)
            {
                foreach (var item_in_config in thingneedtotake.IncludeItems)
                {
                    bool collection_success = false;
                    var items_in_inventory = new Item();
                    int prefix_in_config = item_in_config.prefix;
                    for (int i = 0; i < 50; i++)
                    {
                        items_in_inventory = args.Player.TPlayer.inventory[i];
                        //If prefix = 0 in config, ignore prefix.
                        if (item_in_config.prefix == 0)
                        {
                            prefix_in_config = items_in_inventory.prefix;
                        }
                        // Loops through the player's inventory
                        if ((items_in_inventory.netID == item_in_config.netID) && (items_in_inventory.stack >= item_in_config.stack) && items_in_inventory.prefix == prefix_in_config)
                        {
                            collection_success = true;
                            args.Player.TPlayer.inventory[i].stack -= item_in_config.stack;
                            NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, NetworkText.Empty, args.Player.Index, i);
                            break;
                        }
                    }

                    if (!collection_success)
                    {
                        Item take = TShock.Utils.GetItemById(item_in_config.netID);
                        args.Player.SendMessage("Don't take the item "+ ItemToTag(item_in_config) + " out of your inventory! Transaction Cancelled.", Color.LightCyan);
                        return;
                    }
                }
                double payment = amount * config.questmultiplier;
                int paid = Convert.ToInt32(Math.Ceiling(payment));
                SEconomyPlugin.Instance.WorldAccount.TransferToAsync(UsernameBankAccount, paid,
                                                               Journalpayment, string.Format("Completed quest ID {0} for {1}", id, Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid))),
                                                               string.Format("Quest Completed: " + thingneedtotake.DisplayName));
                args.Player.SendMessage("[Quest System] You have completed Quest "+ thingneedtotake.DisplayName + " for "+ Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid)) + "!", Color.LightBlue);
                TShock.Log.ConsoleInfo("[Quest System] {0} has completed Quest {1} for {2}.", args.Player.Name, thingneedtotake.DisplayName, Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid)));
                var num = QuestDB.Query("INSERT INTO QuestHistory (Time, Account, QuestName, WorldID, Reward) VALUES (@0, @1, @2, @3, @4);", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), args.Player.Name, thingneedtotake.DisplayName, Main.worldID, Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid)));


                string newaccountlist = accountlist_buy + args.Player.User.ID + ",";
                if ((thingneedtotake.maxredeem != -1))
                {
                    var update = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", newaccountlist, id);
                }

            }

        }
        #region config
        private void CreateConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "Quest.json");
            try
            {
                using (var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using (var sr = new StreamWriter(stream))
                    {
                        config = new Config(1);
                        var configString = JsonConvert.SerializeObject(config, Formatting.Indented);
                        sr.Write(configString);
                    }
                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.Message);
            }
        }
        private bool ReadConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "Quest.json");
            try
            {
                if (File.Exists(filepath))
                {
                    using (var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            var configString = sr.ReadToEnd();
                            config = JsonConvert.DeserializeObject<Config>(configString);
                            foreach (var element in config.All)
                            {
                                foreach (var element2 in element.IncludeItems)
                                {
                                    element2.Full();
                                }
                            }
                        }
                        stream.Close();
                    }
                    return true;
                }
                else
                {
                    TShock.Log.ConsoleError("Create new config file for Quest System.");
                    CreateConfig();
                    return false;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.Message);
            }
            return false;
        }
        private void ReloadConfig(CommandArgs args)
        {

            if (ReadConfig())
            {
                UpdateDB();
                args.Player.SendInfoMessage("Load success.");
                return;
            }
            args.Player.SendErrorMessage("Load fails. Check log for more details.");
        }
        #endregion
    }
    public class ReadDB
    {

        public int counts;
        public int pricedb;
        public string actdb;
        public string dnames;

    }
    public class Config
    {
        public List<QuestsEntry> All;
        public bool HideUnavailableQuests;
        public double questmultiplier;
        public string questregion;
        public int min_avail_quest;
        public int max_avail_quest;
        public int percent_not_choosen;
        public int minrefreshsecond;
        public Config()
        { }
        public Config(int a)
        {
            HideUnavailableQuests = true;
            questmultiplier = 1.0;
            questregion = null;
            min_avail_quest = 5;
            max_avail_quest = 20;
            percent_not_choosen = 70;
            minrefreshsecond = 86400;
            All = new List<QuestsEntry> { new QuestsEntry(1), new QuestsEntry(2) };
        }
    }
    public class QuestsEntry
    {
        public string DisplayName = "";
        public string RequirePermission = "";
        public int Reward = 0;
        public int maxredeem = 5;
        public bool hardmode = false;
        public int refreshtime = 1;
        public List<SimpleItem> IncludeItems = new List<SimpleItem> { };
        public QuestsEntry() { }
        public QuestsEntry(int a)
        {
            if (a == 1)
            {
                var i1 = new SimpleItem(2760);
                var i2 = new SimpleItem(2761);
                var i3 = new SimpleItem(2762);
                DisplayName = "ExampleNebula";
                RequirePermission = "quest.use";
                Reward = 500000;
                maxredeem = -1;
                hardmode = true;
                refreshtime = 5;
                IncludeItems = new List<SimpleItem> { i1, i2, i3 };
            }
            if (a == 2)
            {
                DisplayName = "Example2";
                Reward = 20;
                maxredeem = 5;
                hardmode = false;
                for (int i = 0; i < 10; i++)
                {
                    IncludeItems.Add(new SimpleItem(i + 2702));
                }
            }
            if (a == 3)
            {
                var i1 = new SimpleItem(2760);
                DisplayName = "Example3";
                Reward = 500000;
                maxredeem = 5;
                hardmode = false;
                IncludeItems = new List<SimpleItem> { i1 };
            }
        }
    }
    public class SimpleItem
    {
        public int netID = 0;
        public int stack = 1;
        public int prefix = 0;
        public string name = "";
        public SimpleItem() { }
        public SimpleItem(int a)
        {
            this.name = TShockAPI.Utils.Instance.GetItemByIdOrName(a.ToString())[0].Name;
            this.netID = TShockAPI.Utils.Instance.GetItemByIdOrName(a.ToString())[0].type;
        }
        public void Full()
        {
            this.netID = TShockAPI.Utils.Instance.GetItemByIdOrName((netID != 0) ? netID.ToString() : name)[0].type;
            this.name = TShockAPI.Utils.Instance.GetItemByIdOrName(netID.ToString())[0].Name;
        }
    }



}