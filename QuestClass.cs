using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestSystem
{
    public class Config
    {
        public List<QuestsEntry> All;
        public bool HideUnavailableQuests;
        public double questmultiplier;
        public string questregion;
        public int min_avail_quest;
        public int max_avail_quest;
        public int minrefreshsecond;
        public string lastcheck;
        //public List<string> class_list;
        public void lastcheckwrite(DateTime alltimelastcheck)
        {
            lastcheck = alltimelastcheck.ToString();
            return;
        }
        public Config()
        { }
        public Config(int a)
        {
            HideUnavailableQuests = true;
            questmultiplier = 1.0;
            questregion = null;
            min_avail_quest = 5;
            max_avail_quest = 20;
            minrefreshsecond = 86400;
            lastcheck = DateTime.Now.ToString();
            //class_list = new List<string> { "Mage", "Warrior", "Ranger", "Rogue" };
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
