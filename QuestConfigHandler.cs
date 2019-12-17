using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;

namespace QuestSystem
{
    public partial class Quest
    {
        private void CreateConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "Quest.json");
            try
            {
                using (var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using (var sr = new StreamWriter(stream))
                    {
                        if (File.Exists(filepath))
                        { }
                        else
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
        private void ReloadConfig(ReloadEventArgs args)
        {

            if (ReadConfig() && ReadRankConfig())
            {
                UpdateDB();
                args.Player.SendSuccessMessage("[Quest System] Reload success.");
                return;
            }
            args.Player.SendErrorMessage("[Quest System] Load fails. Check log for more details.");
        }

        //----------------------------------------
        //----------------------------------------
    }
}
