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
using Crimson.CustomEvents.Extensions;
using TShockAPI.Hooks;

namespace QuestSystem
{
    [ApiVersion(2, 1)]
    public partial class Quest : TerrariaPlugin
    {
        public static IDbConnection QuestDB;
        private Config config;
        private RankConfig rankconfig;
        private JobConfig jobconfig;
        private DateTime LastCheck2;
        private string rootbuffpermission = "questbuff";
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
            get { return "BMS aka Boss"; }
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
            TShockAPI.Commands.ChatCommands.Add(new Command("quest.use", rankquest, "rank"));
            TShockAPI.Commands.ChatCommands.Add(new Command("quest.use", questsearch, "questsearch", "qs"));
            TShockAPI.Commands.ChatCommands.Add(new Command("quest.job", JobQuest, "jobquest", "jq"));
            TShockAPI.Commands.ChatCommands.Add(new Command("quest.admin", ForceUpdate, "forcequestupdate", "fqu"));
            GeneralHooks.ReloadEvent += ReloadConfig;
            ReadConfig();
            ReadRankConfig();
            ReadJobConfig();
            if (config.lastcheck == null)
            {
                LastCheck2 = DateTime.Now;
                updatelastrefresh();
            }
            else
            { LastCheck2 = DateTime.Parse(config.lastcheck); }

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

            sqlcreator.EnsureTableStructure(new SqlTable("JobQuestHistory",
               new SqlColumn("Time", MySqlDbType.String, 200),
               new SqlColumn("Account", MySqlDbType.VarChar) { Length = 50 },
               new SqlColumn("ID", MySqlDbType.Int32),
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
        private void OnUpdate(EventArgs args)
        {
            if ((DateTime.Now - LastCheck2).TotalSeconds >= config.minrefreshsecond)
            {
                LastCheck2 = DateTime.Now;
                updatelastrefresh();
                DailyUpdate();
            }
        }
        public void updatelastrefresh()
        {
            config.lastcheckwrite(LastCheck2);
            CreateConfig();
            return;
        }
        public void DailyUpdate()
        {
            Random rnd = new Random();
            int quest_quantity = rnd.Next(config.min_avail_quest, config.max_avail_quest);
            int i = 0;
            int total_quest_inDB = 0;

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
                            break;
                        }
                    }
                    int timepassed = Convert.ToInt32((DateTime.Now - DateTime.Parse(reader.Get<string>("LastRefresh"))).TotalSeconds);
                    if ((timepassed >= refreshint * config.minrefreshsecond))
                    {
                        var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Disabled", reader.Get<int>("ID"));
                        var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, reader.Get<int>("ID"));
                        var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.Now, reader.Get<int>("ID"));
                    }
                }
            }
            using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
            {
                while (reader.Read())
                {
                    total_quest_inDB++;
                    if (reader.Get<string>("Status") == "Enabled")
                    {
                        i++;
                    }
                }
            }

            while (i < quest_quantity)
            {
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {
                    bool quest_available_for_enable = false;
                    int nextquest = rnd.Next(1, total_quest_inDB);
                    int q_id = 0;
                    bool hmcheck = false;
                    while (reader.Read())
                    {
                        if ((reader.Get<int>("ID") == nextquest) && (reader.Get<string>("Status") == "Disabled"))
                        {
                            q_id = reader.Get<int>("ID");
                            foreach (var questitem in config.All)
                            {
                                if (questitem.DisplayName == reader.Get<string>("QuestName"))
                                {
                                    hmcheck = questitem.hardmode;
                                    quest_available_for_enable = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (Main.hardMode && quest_available_for_enable)
                    {
                        i++;
                        var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Enabled", q_id);
                        var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, q_id);
                        var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.Now, q_id);
                        continue;
                    }
                    else if ((!Main.hardMode) && !hmcheck && quest_available_for_enable)
                    {
                        i++;
                        var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Enabled", q_id);
                        var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, q_id);
                        var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.Now, q_id);
                        continue;
                    }
                }
            }
            LastCheck2 = DateTime.Now;
            updatelastrefresh();
            TShock.Utils.Broadcast("[Quest System] Available Quest List Has Changed. Please check which Quest is available today using: /quest list.", Color.LightBlue);
            TShock.Utils.Broadcast("[Quest System] Total Number of Quests Available today is: " + Convert.ToString(i).Colorize(Color.Yellow) + ".", Color.LightBlue);
        }
        private void ForceUpdate(CommandArgs args)
        {
            Random rnd = new Random();
            int quest_quantity = rnd.Next(config.min_avail_quest, config.max_avail_quest);
            int i = 0;
            int total_quest_inDB = 0;

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
                            break;
                        }
                    }
                    int timepassed = Convert.ToInt32((DateTime.Now - DateTime.Parse(reader.Get<string>("LastRefresh"))).TotalSeconds);
                    if ((timepassed >= refreshint * config.minrefreshsecond))
                    {
                        var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Disabled", reader.Get<int>("ID"));
                        var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, reader.Get<int>("ID"));
                        var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.Now, reader.Get<int>("ID"));
                    }
                }
            }
            using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
            {
                while (reader.Read())
                {
                    total_quest_inDB++;
                    if (reader.Get<string>("Status") == "Enabled")
                    {
                        i++;
                    }
                }
            }

            while (i < quest_quantity)
            {
                using (var reader = QuestDB.QueryReader("SELECT * FROM QuestCount"))
                {
                    bool quest_available_for_enable = false;
                    int nextquest = rnd.Next(1, total_quest_inDB);
                    int q_id = 0;
                    bool hmcheck = false;
                    while (reader.Read())
                    {
                        if ((reader.Get<int>("ID") == nextquest) && (reader.Get<string>("Status") == "Disabled"))
                        {
                            q_id = reader.Get<int>("ID");
                            foreach (var questitem in config.All)
                            {
                                if (questitem.DisplayName == reader.Get<string>("QuestName"))
                                {
                                    hmcheck = questitem.hardmode;
                                    quest_available_for_enable = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (Main.hardMode && quest_available_for_enable)
                    {
                        i++;
                        var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Enabled", q_id);
                        var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, q_id);
                        var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.Now, q_id);
                        continue;
                    }
                    else if ((!Main.hardMode) && !hmcheck && quest_available_for_enable)
                    {
                        i++;
                        var update_status = QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Enabled", q_id);
                        var update_completionlist = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", null, q_id);
                        var update_lastcheck = QuestDB.Query("UPDATE QuestCount SET LastRefresh=@0 WHERE ID= @1;", DateTime.Now, q_id);
                        continue;
                    }
                }
            }
            LastCheck2 = DateTime.Now;
            updatelastrefresh();
            TShock.Utils.Broadcast("[Quest System] Available Quest List Has Changed. Please check which Quest is available today using: /quest list.", Color.LightBlue);
            TShock.Utils.Broadcast("[Quest System] Total Number of Quests Available today is: " + Convert.ToString(i).Colorize(Color.Yellow) + ".", Color.LightBlue);
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
                    int timeleft = Convert.ToInt32((refreshint * config.minrefreshsecond - (DateTime.Now - lastcheck).TotalSeconds) / config.minrefreshsecond);
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
            //args.Player.SendMessage(args.Player.Group.GetDynamicPermission(rootbuffpermission).ToString(), Color.LightBlue);
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
                var lines = new List<string> { };
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
                            double timeleft = ((refreshint * config.minrefreshsecond - (DateTime.Now - lastcheck).TotalSeconds) / config.minrefreshsecond);
                            if (timeleft <= 0)
                            {
                                timeleft = 0;
                                QuestDB.Query("UPDATE QuestCount SET Status=@0 WHERE ID= @1;", "Disabled", reader.Get<int>("ID"));

                            }
                            else if (timeleft <= 1)
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
                            string newline = "* ID: " + reader.Get<int>("ID").ToString().Colorize(Color.LightBlue) + " - ".Colorize(Color.LightBlue) + left.Colorize(Color.LightBlue) + " redeem(s) left " + "- ".Colorize(Color.LightBlue) + Math.Ceiling(timeleft).ToString().Colorize(Color.LightBlue) + " day(s) left " + "-".Colorize(Color.LightBlue) + " Reward: " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(Math.Ceiling(reward))).ToString().Colorize(Color.LightBlue) + " - ".Colorize(Color.LightBlue);
                            foreach (var item in item_in_config.IncludeItems)
                            {
                                newline = newline + ItemToTag(item);
                            }
                            newline = newline + " -".Colorize(Color.LightBlue) + " Quest Name: " + reader.Get<string>("QuestName").Colorize(Color.LightBlue);
                            if ((args.Player.Group.HasPermission(item_in_config.RequirePermission)) && ((item_in_config.maxredeem - totalcount) != 0))
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
                args.Player.SendMessage("[Quest System] Incorrect Syntax. Try again", Color.LightCoral);
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
                args.Player.SendMessage("[Quest System] Incorrect ID. Try again", Color.LightCoral);
                return;
            }
            if (!GetEnabledStatus(questname))
            {
                args.Player.SendMessage("[Quest System] This quest is not available today. Please come back later.", Color.LightCoral);
                return;
            }
            int time_completed = CheckCompletion(args.Player.User.ID, questname);
            var thingneedtotake = new QuestsEntry();
            foreach (var i1 in config.All)
            {
                if (i1.DisplayName == questname)
                {
                    thingneedtotake = i1;
                }
            }
            if (!args.Player.Group.HasPermission(thingneedtotake.RequirePermission))
            {
                args.Player.SendMessage("[Quest System] You are not allow to do this Quest.", Color.LightCoral);
                return;
            }
            if ((thingneedtotake.maxredeem != -1) && (time_completed >= thingneedtotake.maxredeem))
            {
                args.Player.SendMessage("[Quest System] Maximum number of completion reached. You cannot do this quest anymore.", Color.LightCoral);
                return;
            }
            if ((!checkregion(args.Player, config.questregion)))
            {
                args.Player.SendMessage("[Quest System] You are not in the Quest region.", Color.LightCoral);
                return;
            }



            var UsernameBankAccount = SEconomyPlugin.Instance.GetBankAccount(args.Player.Name);
            //var playeramount = UsernameBankAccount.Balance;
            double level_bonus_percent = args.Player.Group.GetDynamicPermission(rootbuffpermission);
            Money amount = thingneedtotake.Reward;
            //Money amount2 = -thingneedtotake.Reward * (1 + level_bonus_percent / 100);
            //var amount3 = Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(-amount2));
            var Journalpayment = Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToReceiver;

            if (args.Player == null || UsernameBankAccount == null)
            {
                args.Player.SendMessage("[Quest System] Can't find the account for " + args.Player.Name + ".", Color.LightCoral);
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
                    args.Player.SendMessage("[Quest System] You don't have the required item " + ItemToTag(item_in_config) + " in your inventory!", Color.LightCoral);
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
                        args.Player.SendMessage("[Quest System] Don't take the item " + ItemToTag(item_in_config) + " out of your inventory! Transaction Cancelled.", Color.LightCoral);
                        return;
                    }
                }
                double payment = amount * config.questmultiplier * (1 + level_bonus_percent/100);
                //args.Player.SendInfoMessage(payment.ToString());
                int paid = Convert.ToInt32(Math.Ceiling(payment));
                SEconomyPlugin.Instance.WorldAccount.TransferToAsync(UsernameBankAccount, paid,
                                                               Journalpayment, string.Format("Completed quest ID {0} for {1}", id, Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid))),
                                                               string.Format("Quest Completed: " + thingneedtotake.DisplayName));
                if (level_bonus_percent != 0)
                {
                    args.Player.SendMessage("[Quest System] A rank bonus of " + level_bonus_percent.ToString().Colorize(Color.Yellow) + "%".Colorize(Color.Yellow) + " will be added to your reward.", Color.LightBlue);
                }
                args.Player.SendMessage("[Quest System] You have completed Quest " + thingneedtotake.DisplayName.Colorize(Color.Yellow) + " for " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid)).ToString().Colorize(Color.Yellow) + "!", Color.LightBlue);
                TShock.Log.ConsoleInfo("[Quest System] {0} has completed Quest {1} for {2}.", args.Player.Name, thingneedtotake.DisplayName, Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid)));
                var num = QuestDB.Query("INSERT INTO QuestHistory (Time, Account, QuestName, WorldID, Reward) VALUES (@0, @1, @2, @3, @4);", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), args.Player.Name, thingneedtotake.DisplayName, Main.worldID, Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid)));
                string newaccountlist = accountlist_buy + args.Player.User.ID + ",";
                if ((thingneedtotake.maxredeem != -1))
                {
                    var update = QuestDB.Query("UPDATE QuestCount SET Accounts=@0 WHERE ID= @1;", newaccountlist, id);
                }
            }
        }
        private void rankquest(CommandArgs args)
        {
            int rankforkcount = 0;
            var destinationlist = new List<string> { };
            foreach (var quest in rankconfig.All)
            {
                if (quest.startrank == args.Player.Group.Name)
                {
                    destinationlist.Add(quest.finalrank);
                    rankforkcount++;
                }
            }
            if ((rankforkcount > 1) && (args.Parameters.Count == 0 || args.Parameters[0] == "up"))
            {
                args.Player.SendMessage("[Quest System] There are " + rankforkcount + " available Factions, their Quest and Rank-up Command are: ", Color.LightBlue);
                args.Player.SendMessage("", Color.Blue);
                foreach (var classname in destinationlist)
                {
                    foreach (var quest in rankconfig.All)
                    {
                        if ((quest.startrank == args.Player.Group.Name) && (quest.finalrank == classname))
                        {
                            string itemlist = null;
                            foreach (var material in quest.IncludeItems)
                            {
                                itemlist = itemlist + itemtotag(material.stack, material.netID, material.prefix);
                            }
                            args.Player.SendMessage("* Items: " + itemlist, Color.Yellow);
                            args.Player.SendMessage("* /rank \"" + classname + "\" (costs " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(rankconfig.questmultiplier * quest.price)) + ")", Color.Yellow);
                            args.Player.SendMessage("", Color.Blue);
                        }
                    }
                }
                return;
            }

            if (rankforkcount > 1 && (destinationlist.Contains(args.Parameters[0])))
            {
                bool questavail = false;
                RankQuestsEntry questtodo = null;
                foreach (RankQuestsEntry rankquest in rankconfig.All)
                {
                    if ((rankquest.startrank == args.Player.Group.Name) && (rankquest.finalrank == args.Parameters[0]))
                    {
                        questtodo = rankquest;
                        questavail = true;
                    }
                }
                if (!questavail)
                {
                    args.Player.SendMessage("[Quest System] There are no quest available for this rank. You can level up directly using: /rank up.", Color.LightBlue);
                    return;
                }
                if (questtodo.hardmode && !Main.hardMode)
                {
                    args.Player.SendMessage("[Quest System] This rank is not available pre-hardmode. Try again after Hardmode.", Color.LightCoral);
                    return;
                }
                bool confirm_success = false;
                foreach (var iteminlist in questtodo.IncludeItems)
                {
                    bool exist2 = false;
                    var items_in_inventory = new Item();
                    int prefix_in_config = iteminlist.prefix;
                    for (int i = 0; i < 50; i++)
                    {
                        items_in_inventory = args.Player.TPlayer.inventory[i];
                        //If prefix = 0 in config, ignore prefix.
                        if (iteminlist.prefix == 0)
                        {
                            prefix_in_config = items_in_inventory.prefix;
                        }
                        // Loops through the player's inventory
                        if ((items_in_inventory.netID == iteminlist.netID) && (items_in_inventory.stack >= iteminlist.stack) && items_in_inventory.prefix == prefix_in_config)
                        {
                            confirm_success = true;
                            exist2 = true;
                            break;
                        }
                    }
                    if (!exist2)
                    {
                        args.Player.SendMessage("[Quest System] We cannot find this item: " + itemtotag(iteminlist.stack, iteminlist.netID, iteminlist.prefix) + ". There maybe more item(s) missing, please check the quest's requirement again.", Color.LightCoral);
                        confirm_success = false;
                        return;
                    }
                }

                var UsernameBankAccount = SEconomyPlugin.Instance.GetBankAccount(args.Player.Name);
                var playeramount = UsernameBankAccount.Balance;
                Money amount = -questtodo.price;
                Money amount2 = questtodo.price;
                var Journalpayment = Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToReceiver;
                if (args.Player == null || UsernameBankAccount == null)
                {
                    args.Player.SendMessage("[Quest System] Can't find the account for " + args.Player.Name + ".", Color.LightCoral);
                    return;
                }
                if (playeramount < amount2 * rankconfig.questmultiplier)
                {
                    args.Player.SendMessage("[Quest System] You need at least " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(rankconfig.questmultiplier * questtodo.price)).ToString().Colorize(Color.Yellow) + " to become a [" + questtodo.finalrank.Colorize(Color.Yellow) + "]. But you only have " + UsernameBankAccount.Balance.ToString().Colorize(Color.Yellow) + " in your account.", Color.LightBlue);
                    return;
                }

                if (confirm_success)
                {
                    foreach (var iteminlist in questtodo.IncludeItems)
                    {
                        bool exist2 = false;
                        var items_in_inventory = new Item();
                        int prefix_in_config = iteminlist.prefix;

                        for (int i = 0; i < 50; i++)
                        {
                            items_in_inventory = args.Player.TPlayer.inventory[i];
                            //If prefix = 0 in config, ignore prefix.
                            if (iteminlist.prefix == 0)
                            {
                                prefix_in_config = items_in_inventory.prefix;
                            }
                            // Loops through the player's inventory
                            if ((items_in_inventory.netID == iteminlist.netID) && (items_in_inventory.stack >= iteminlist.stack) && items_in_inventory.prefix == prefix_in_config)
                            {
                                confirm_success = true;
                                exist2 = true;
                                args.Player.TPlayer.inventory[i].stack -= iteminlist.stack;
                                NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, NetworkText.Empty, args.Player.Index, i);
                                break;
                            }
                        }
                        if (!exist2)
                        {
                            args.Player.SendMessage("[Quest System] Do not take this item: " + itemtotag(iteminlist.stack, iteminlist.netID, iteminlist.prefix) + " out of your inventory!", Color.LightCoral);
                            confirm_success = false;
                            return;
                        }
                    }
                }
                //If all items is found. Remove from inventory and change group.
                if (confirm_success)
                {
                    int paid = Convert.ToInt32(Math.Ceiling(amount * rankconfig.questmultiplier));
                    SEconomyPlugin.Instance.WorldAccount.TransferToAsync(UsernameBankAccount, paid,
                                                                         Journalpayment, string.Format("Rank up to {0}", questtodo.finalrank),
                                                                         string.Format("Rank " + questtodo.finalrank));
                    TShock.Users.SetUserGroup(args.Player.User, questtodo.finalrank);
                    TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/er \"" + args.Player.Name + "\"" + "-h +" + questtodo.hpup);
                    TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/er \"" + args.Player.Name + "\"" + "-m +" + questtodo.manaup);
                    args.Player.SendMessage("[Quest System] Your HP has increase by " + questtodo.hpup + ".", Color.DeepSkyBlue);
                    args.Player.SendMessage("[Quest System] Your Mana has increase by " + questtodo.manaup + ".", Color.DeepSkyBlue);
                    if (questtodo.buffname != null)
                    {
                        TShockAPI.Commands.HandleCommand(TSPlayer.Server, ".gpermabuff \"" + questtodo.buffname + "\"" + " \"" + args.Player.Name + "\"");
                        args.Player.SendMessage("[Quest System] You has been granted " + questtodo.buffname + " buff permanently.", Color.DeepSkyBlue);
                    }
                    args.Player.SendMessage("[Quest System] Congratulation, You have completed the Faction's quest and become a " + questtodo.finalrank + "!", Color.DeepSkyBlue);
                    TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/firework \"" + args.Player.Name + "\"");
                    TShock.Utils.Broadcast(args.Player.Name + " has become a " + questtodo.finalrank, Color.DeepSkyBlue);
                    args.Player.SendMessage("", Color.DeepSkyBlue);
                    return;
                }
                return;
            }

            if (rankforkcount == 1 && ((args.Parameters.Count == 0) || (args.Parameters[0] == "up")))
            {
                string item_list = null;
                bool quest_available = false;
                bool hardmoderequire = false;
                int price = 0;

                foreach (RankQuestsEntry rankquest in rankconfig.All)
                {
                    if (rankquest.startrank == args.Player.Group.Name)
                    {
                        hardmoderequire = rankquest.hardmode;
                        price = rankquest.price;
                        foreach (var items_config in rankquest.IncludeItems)
                        {
                            quest_available = true;
                            string item_tag = itemtotag(items_config.stack, items_config.netID, items_config.prefix);
                            item_list = item_list + item_tag;
                        }
                    }
                }
                if (hardmoderequire && !Main.hardMode)
                {
                    args.Player.SendMessage("[Quest System] This rank is not available pre-hardmode. Try again after Hardmode.", Color.LightCoral);
                    return;
                }
                if (quest_available)
                {
                    args.Player.SendMessage("* Quest Items list: " + item_list, Color.Yellow);
                    args.Player.SendMessage("* Rank-up command: /rank quest (costs " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(rankconfig.questmultiplier * price)) + ")", Color.Yellow);
                    return;
                }
                return;
            }
            if (rankforkcount == 1 && (args.Parameters[0] == "quest" || args.Parameters[0] == "q"))
            {
                bool questavail = false;
                RankQuestsEntry questtodo = null;
                foreach (RankQuestsEntry rankquest in rankconfig.All)
                {
                    if (rankquest.startrank == args.Player.Group.Name)
                    {
                        questtodo = rankquest;
                        questavail = true;
                    }
                }
                if (questtodo.hardmode && !Main.hardMode)
                {
                    args.Player.SendMessage("[Quest System] This rank is not available pre-hardmode. Try again after Hardmode.", Color.LightCoral);
                    return;
                }
                if (!questavail)
                {
                    args.Player.SendMessage("[Quest System] There are no quest available for this rank. You can level up directly using: /rank up.", Color.LightCoral);
                    return;
                }
                bool confirm_success = false;
                foreach (var iteminlist in questtodo.IncludeItems)
                {
                    bool exist2 = false;
                    var items_in_inventory = new Item();
                    int prefix_in_config = iteminlist.prefix;
                    for (int i = 0; i < 50; i++)
                    {
                        items_in_inventory = args.Player.TPlayer.inventory[i];
                        //If prefix = 0 in config, ignore prefix.
                        if (iteminlist.prefix == 0)
                        {
                            prefix_in_config = items_in_inventory.prefix;
                        }
                        // Loops through the player's inventory
                        if ((items_in_inventory.netID == iteminlist.netID) && (items_in_inventory.stack >= iteminlist.stack) && items_in_inventory.prefix == prefix_in_config)
                        {
                            confirm_success = true;
                            exist2 = true;
                            break;
                        }
                    }
                    if (!exist2)
                    {
                        args.Player.SendMessage("[Quest System] We cannot find this item: " + itemtotag(iteminlist.stack, iteminlist.netID, iteminlist.prefix) + ". There maybe more item(s) missing, please check the quest's requirement again.", Color.LightCoral);
                        confirm_success = false;
                        return;
                    }
                }

                var UsernameBankAccount = SEconomyPlugin.Instance.GetBankAccount(args.Player.Name);
                var playeramount = UsernameBankAccount.Balance;
                Money amount = -questtodo.price;
                Money amount2 = questtodo.price;
                var Journalpayment = Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToReceiver;
                if (args.Player == null || UsernameBankAccount == null)
                {
                    args.Player.SendMessage("[Quest System] Can't find the account for " + args.Player.Name + ".", Color.LightCoral);
                    return;
                }
                if (playeramount < amount2 * rankconfig.questmultiplier)
                {
                    args.Player.SendMessage("[Quest System] You need at least " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(rankconfig.questmultiplier * questtodo.price)).ToString().Colorize(Color.Yellow) + " to become a [" + questtodo.finalrank.Colorize(Color.Yellow) + "]. But you only have " + UsernameBankAccount.Balance.ToString().Colorize(Color.Yellow) + " in your account.", Color.LightCoral);
                    return;
                }

                if (confirm_success)
                {
                    foreach (var iteminlist in questtodo.IncludeItems)
                    {
                        bool exist2 = false;
                        var items_in_inventory = new Item();
                        int prefix_in_config = iteminlist.prefix;

                        for (int i = 0; i < 50; i++)
                        {
                            items_in_inventory = args.Player.TPlayer.inventory[i];
                            //If prefix = 0 in config, ignore prefix.
                            if (iteminlist.prefix == 0)
                            {
                                prefix_in_config = items_in_inventory.prefix;
                            }
                            // Loops through the player's inventory
                            if ((items_in_inventory.netID == iteminlist.netID) && (items_in_inventory.stack >= iteminlist.stack) && items_in_inventory.prefix == prefix_in_config)
                            {
                                confirm_success = true;
                                exist2 = true;
                                args.Player.TPlayer.inventory[i].stack -= iteminlist.stack;
                                NetMessage.SendData((int)PacketTypes.PlayerSlot, -1, -1, NetworkText.Empty, args.Player.Index, i);
                                break;
                            }
                        }
                        if (!exist2)
                        {
                            args.Player.SendMessage("[Quest System] Do not take this item: " + itemtotag(iteminlist.stack, iteminlist.netID, iteminlist.prefix) + " out of your inventory!", Color.LightCoral);
                            confirm_success = false;
                            return;
                        }
                    }
                }
                //If all items is found. Remove from inventory and change group.
                if (confirm_success)
                {
                    int paid = Convert.ToInt32(Math.Ceiling(amount * rankconfig.questmultiplier));
                    SEconomyPlugin.Instance.WorldAccount.TransferToAsync(UsernameBankAccount, paid,
                                                                         Journalpayment, string.Format("Rank up to {0}", questtodo.finalrank),
                                                                         string.Format("Rank " + questtodo.finalrank));
                    TShock.Users.SetUserGroup(args.Player.User, questtodo.finalrank);
                    TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/er \"" + args.Player.Name + "\"" + "-h +" + questtodo.hpup);
                    TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/er \"" + args.Player.Name + "\"" + "-m +" + questtodo.manaup);
                    args.Player.SendMessage("[Quest System] Your HP has increase by " + questtodo.hpup + ".", Color.DeepSkyBlue);
                    args.Player.SendMessage("[Quest System] Your Mana has increase by " + questtodo.manaup + ".", Color.DeepSkyBlue);
                    if (questtodo.buffname != null)
                    {
                        TShockAPI.Commands.HandleCommand(TSPlayer.Server, ".gpermabuff \"" + questtodo.buffname + "\"" + " \"" + args.Player.Name + "\"");
                        args.Player.SendMessage("[Quest System] You has been granted " + questtodo.buffname + " buff permanently.", Color.DeepSkyBlue);
                    }
                    args.Player.SendMessage("[Quest System] Congratulation, You have completed the Faction's quest and become a " + questtodo.finalrank + "!", Color.DeepSkyBlue);
                    TShockAPI.Commands.HandleCommand(TSPlayer.Server, "/firework \"" + args.Player.Name + "\"");
                    TShock.Utils.Broadcast(args.Player.Name + " has become a " + questtodo.finalrank, Color.DeepSkyBlue);
                    args.Player.SendMessage("", Color.DeepSkyBlue);
                    return;
                }
                return;
            }
        }
        private void JobQuest(CommandArgs args)
        {
            if ((args.Parameters.Count < 1))
            {
                args.Player.SendMessage("Redeem a Job Quest: /jobquest <quest ID>", Color.LightBlue);
                args.Player.SendMessage("Job Quest List: /jobquest list or /jq l", Color.LightBlue);
                //args.Player.SendMessage("Search for a Job Quest: /jobquest search <search term> or /jq s <search term>", Color.LightBlue);
                return;
            }
            if ((args.Parameters[0] == "list") || (args.Parameters[0] == "l"))
            {
                int pageNumber = 1;
                if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
                    return;
                var lines = new List<string> { };
                for (int i=0; i< jobconfig.All.Count(); i++)
                {
                    double reward = jobconfig.questmultiplier * jobconfig.All[i].Reward;
                    string newline = "* ID: " + i.ToString().Colorize(Color.LightBlue) + " - ".Colorize(Color.LightBlue) + " Reward: " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(Math.Ceiling(reward))).ToString().Colorize(Color.LightBlue) + " - ".Colorize(Color.LightBlue);
                    foreach (var item in jobconfig.All[i].IncludeItems)
                    {
                        newline = newline + ItemToTag(item);
                    }
                    if ((args.Player.HasPermission(jobconfig.All[i].RequirePermission)) && (!jobconfig.All[i].hardmode || (jobconfig.All[i].hardmode && Main.hardMode)))
                    {
                        lines.Add(newline);
                    }
                }      
                
                PaginationTools.SendPage(args.Player, pageNumber, lines,
                                         new PaginationTools.Settings
                                         {
                                             HeaderFormat = "Job Quest Menu ({0}/{1}):",
                                             FooterFormat = "Type {0}jobquest l {{0}} for more options.".SFormat(Commands.Specifier),
                                             MaxLinesPerPage = 9
                                         }
                                        );
                return;
            }
            
            if ((args.Parameters[0] != "l") && (args.Parameters[0] != "list") && (!int.TryParse(args.Parameters[0], out _)))
            {
                args.Player.SendMessage("[Quest System] Incorrect Syntax. Try again", Color.LightCoral);
                return;
            }
            int id = Convert.ToInt32(args.Parameters[0]);

            if ((jobconfig.All.Count() <= (id - 1)) || (id < 0))
            {
                args.Player.SendMessage("[Quest System] Incorrect ID. Try again", Color.LightCoral);
                return;
            }
            if (!args.Player.HasPermission(jobconfig.All[id].RequirePermission))
            {
                args.Player.SendMessage("[Quest System] You do not met the Job/Level requirement for this Quest.", Color.LightCoral);
                return;
            }
            
            if (jobconfig.All[id].hardmode && !Main.hardMode)
            {
                args.Player.SendMessage("[Quest System] This Quest require Hardmode World. Please try again after WoF.", Color.LightCoral);
                return;
            }

            var UsernameBankAccount = SEconomyPlugin.Instance.GetBankAccount(args.Player.Name);
            //var playeramount = UsernameBankAccount.Balance;
            //double level_bonus_percent = args.Player.Group.GetDynamicPermission(rootbuffpermission);
            Money amount = jobconfig.All[id].Reward;
            //Money amount2 = -thingneedtotake.Reward * (1 + level_bonus_percent / 100);
            //var amount3 = Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(-amount2));
            var Journalpayment = Wolfje.Plugins.SEconomy.Journal.BankAccountTransferOptions.AnnounceToReceiver;

            if (args.Player == null || UsernameBankAccount == null)
            {
                args.Player.SendMessage("[Quest System] Can't find the account for " + args.Player.Name + ".", Color.LightCoral);
                return;
            }
            bool exist = false;
            foreach (var item_in_config in jobconfig.All[id].IncludeItems)
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
                    args.Player.SendMessage("[Quest System] You don't have the required item " + ItemToTag(item_in_config) + " in your inventory!", Color.LightCoral);
                    return;
                }
            }



            if (exist == true)
            {
                foreach (var item_in_config in jobconfig.All[id].IncludeItems)
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
                        args.Player.SendMessage("[Quest System] Don't take the item " + ItemToTag(item_in_config) + " out of your inventory! Transaction Cancelled.", Color.LightCoral);
                        return;
                    }
                }
                double payment = amount * jobconfig.questmultiplier;
                //args.Player.SendInfoMessage(payment.ToString());
                int paid = Convert.ToInt32(Math.Ceiling(payment));
                SEconomyPlugin.Instance.WorldAccount.TransferToAsync(UsernameBankAccount, paid,
                                                               Journalpayment, string.Format("Completed Job Quest ID {0} for {1}", id, Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid))),
                                                               string.Format("Quest Completed: " + jobconfig.All[id].DisplayName));
                QuestDB.Query("INSERT INTO JobQuestHistory (Time, Account, ID, WorldID, Reward) VALUES (@0, @1, @2, @3, @4);", DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss"), args.Player.Name, id, Main.worldID, Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid)));
                args.Player.SendMessage("[Quest System] You have completed Job Quest " + id.ToString().Colorize(Color.Yellow) + " for " + Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid)).ToString().Colorize(Color.Yellow) + "!", Color.LightBlue);
                TShock.Log.ConsoleInfo("[Quest System] {0} has completed Job Quest {1} for {2}.", args.Player.Name, id, Wolfje.Plugins.SEconomy.Money.Parse(Convert.ToString(paid)));
            }
        }
    }
}