using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuestSystem
{
    public class RankConfig
    {
        public List<RankQuestsEntry> All;
        public double questmultiplier;
        public RankConfig()
        { }
        public RankConfig(int a)
        {
            questmultiplier = 1.0;
            All = new List<RankQuestsEntry> { new RankQuestsEntry(1), new RankQuestsEntry(2) };
        }
    }
    public class RankQuestsEntry
    {
        public string startrank = "start";
        public string finalrank = "final";
        public int price = 10000;
        public int manaup = 1;
        public int hpup = 1;
        public string buffname = null;
        public bool hardmode = false;
        public List<RankSimpleItem> IncludeItems = new List<RankSimpleItem> { };
        public RankQuestsEntry() { }
        public RankQuestsEntry(int a)
        {
            if (a == 1)
            {
                var i1 = new RankSimpleItem(2760);
                var i2 = new RankSimpleItem(2761);
                var i3 = new RankSimpleItem(2762);
                startrank = "start";
                finalrank = "final";
                price = 20000;
                hardmode = true;
                IncludeItems = new List<RankSimpleItem> { i1, i2, i3 };
            }
            if (a == 2)
            {
                price = 100;
                hardmode = false;
                for (int i = 0; i < 10; i++)
                {
                    IncludeItems.Add(new RankSimpleItem(i + 2702));
                }
            }
            if (a == 3)
            {
                var i1 = new RankSimpleItem(2760);
                startrank = "start2";
                finalrank = "final2";
                hardmode = false;
                IncludeItems = new List<RankSimpleItem> { i1 };
            }
        }
    }
    public class RankSimpleItem
    {
        public int netID = 0;
        public int stack = 1;
        public int prefix = 0;
        public string name = "";
        public RankSimpleItem() { }
        public RankSimpleItem(int a)
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
