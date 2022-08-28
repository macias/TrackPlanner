using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TrackPlanner.WebUI.Server
{
    public static class JsonExtension
    {
        // https://stackoverflow.com/a/62533775/6734314
        public static JToken Jsonize(this IConfiguration configuration)
        {
            if (configuration.GetChildren().Any())
            {
                JObject obj = new JObject();

                foreach (IConfigurationSection child in configuration.GetChildren())
                {
                    if (TryGetIndex(child.Path, out _))
                    {
                        return JsonizeArry(configuration);
                    }
                    else
                    {
                        obj.Add(child.Key, Jsonize(child));
                    }
                }

                return obj;
            }
            else if (configuration is IConfigurationSection section)
            {
                if (bool.TryParse(section.Value, out bool boolean))
                {
                    return new JValue(boolean);
                }
                else if (long.TryParse(section.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
                {
                    return new JValue(integer);
                }
                else if (double.TryParse(section.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
                {
                    return new JValue(real);
                }

                return new JValue(section.Value);
            }

            return new JObject();
        }

        private static bool TryGetIndex(string sectionPath, out int index)
        {
            Match? match = new Regex(":(\\d+)$").Match(sectionPath);
            if (match == null || !match.Success)
            {
                index = default;
                return false;
            }

            // first group is the global one so we skip over it

            string str = match.Groups.Skip<Group>(1).Single().Captures.Single().Value;

            return int.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) && index >= 0;
        }

        private static JToken JsonizeArry(IConfiguration configuration)
        {
            var arr = new JArray();

            foreach (IConfigurationSection child in configuration.GetChildren())
            {
                if (!TryGetIndex(child.Path, out int idx))
                    throw new ArgumentException($"Cannot read index out of {child.Path}");
                while (idx >= arr.Count)
                    arr.Add(null!);
                arr[idx] = Jsonize(child);
            }

            return arr;
        }


    }
}
