using System;
using System.Text.RegularExpressions;
using Group = TShockAPI.Group;

namespace QuestSystem
{
    // From EssentialsPlus

    public static class GroupExtensions
    {
        /// <summary>
        ///   Returns the number in the end of the permission.
        /// </summary>
        /// <param name="group">A TShock group.</param>
        /// <param name="root">Root of permission (part before number)</param>
        /// <returns></returns>
        public static double GetDynamicPermission(this Group group, string root)
        {
            double maxbuff = short.MaxValue;
            if (group.HasPermission(root + ".*") || group.HasPermission("*"))
                return maxbuff;

            var max = 0;
            var regex = new Regex("^" + root.Replace(".", @"\.") + @"\.(\d+)$");

            foreach (var permission in group.TotalPermissions)
            {
                var match = regex.Match(permission);
                if (match.Success && match.Value == permission)
                    max = Math.Max(max, Convert.ToInt32(match.Groups[1].Value));
            }

            return max;
        }
    }
}