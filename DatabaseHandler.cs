using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TShockAPI;
using TShockAPI.DB;

namespace QuestSystem
{
    public partial class Quest
    {
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
                    var add = QuestDB.Query("INSERT INTO QuestCount (QuestName, Status, LastRefresh) VALUES (@0, @1, @2);", questitem.DisplayName, "Disabled", DateTime.Now);
                }
            }
        }
    }
}
