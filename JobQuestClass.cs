using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestSystem
{
    public class JobConfig
    {
        public List<QuestsEntry> All;
        public bool HideUnavailableQuests;
        public double questmultiplier;
        //public List<string> class_list;
        public JobConfig()
        { }
        public JobConfig(int a)
        {
            HideUnavailableQuests = true;
            questmultiplier = 1.0;
            //class_list = new List<string> { "Mage", "Warrior", "Ranger", "Rogue" };
            All = new List<QuestsEntry> { new QuestsEntry(1), new QuestsEntry(2) };
        }
    }
    public class JobQuestsEntry
    {
        public string DisplayName = "";
        public string RequireGroup = null;
        public int Reward = 0;
        public bool hardmode = false;
        public List<JobSimpleItem> IncludeItems = new List<JobSimpleItem> { };
        public JobQuestsEntry() { }
        public JobQuestsEntry(int a)
        {
            if (a == 1)
            {
                var i1 = new JobSimpleItem(2760);
                var i2 = new JobSimpleItem(2761);
                var i3 = new JobSimpleItem(2762);
                DisplayName = "ExampleNebula";
                RequireGroup = "Mage Level 5";
                Reward = 500000;
                hardmode = true;
                IncludeItems = new List<JobSimpleItem> { i1, i2, i3 };
            }
            if (a == 2)
            {
                DisplayName = "Example2";
                Reward = 20;
                hardmode = false;
                for (int i = 0; i < 10; i++)
                {
                    IncludeItems.Add(new JobSimpleItem(i + 2702));
                }
            }
            if (a == 3)
            {
                var i1 = new JobSimpleItem(2760);
                DisplayName = "Example3";
                Reward = 500000;
                hardmode = false;
                IncludeItems = new List<JobSimpleItem> { i1 };
            }
        }
    }
    public class JobSimpleItem
    {
        public int netID = 0;
        public int stack = 1;
        public int prefix = 0;
        public string name = "";
        public JobSimpleItem() { }
        public JobSimpleItem(int a)
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
