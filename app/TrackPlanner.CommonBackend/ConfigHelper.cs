using System;
using System.IO;
using System.Reflection;
using TrackPlanner.Data.Serialization;

namespace TrackPlanner.CommonBackend
{
    public class ConfigHelper
    {
        public static string InitializeConfigFile<TConfig>(string configFilename,TConfig defaultConfig)
        {
            string bin_directory = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
            var path = System.IO.Path.Combine(bin_directory, "..", "config",configFilename);
            
            if (!System.IO.File.Exists(path))
            {
                var config_dir = System.IO.Path.GetDirectoryName(path);
                System.IO.Directory.CreateDirectory(config_dir!);
                System.IO.File.WriteAllText(path, new ProxySerializer().Serialize(defaultConfig));
            }

            return path;
        }
    }
}