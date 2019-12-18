using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI.DB;

namespace QuestSystem
{
    public partial class Quest
    {
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

        private static string ItemToTag(JobSimpleItem args)
        {
            string ret = ((args.prefix != 0) ? "[i/p" + args.prefix : "[i");
            ret = (args.stack != 1) ? ret + "/s" + args.stack : ret;
            ret = ret + ":" + args.netID + "]";
            if (args.netID == 0) return "";
            return ret;
        }
    }
}
