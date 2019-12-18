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
        private void CreateJobConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "Jobquest.json");
            try
            {
                using (var stream = new FileStream(filepath, FileMode.Create, FileAccess.Write, FileShare.Write))
                {
                    using (var sr = new StreamWriter(stream))
                    {
                        jobconfig = new JobConfig(1);
                        var configString = JsonConvert.SerializeObject(jobconfig, Formatting.Indented);
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
        private bool ReadJobConfig()
        {
            string filepath = Path.Combine(TShock.SavePath, "Jobquest.json");
            try
            {
                if (File.Exists(filepath))
                {
                    using (var stream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var sr = new StreamReader(stream))
                        {
                            var configString = sr.ReadToEnd();
                            jobconfig = JsonConvert.DeserializeObject<JobConfig>(configString);
                            foreach (var element in rankconfig.All)
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
                    TShock.Log.ConsoleInfo("Create new config file for Quest System Job Quest.");
                    CreateJobConfig();
                    return false;
                }
            }
            catch (Exception ex)
            {
                TShock.Log.ConsoleError(ex.Message);
            }
            return false;
        }
    }
}
