using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TrackPlanner.Data;
using TrackPlanner.Data.Serialization;

namespace TrackPlanner.Data
{
  
    public sealed class UserVisualPreferences
    {
        public Dictionary<SpeedMode, LineDecoration> SpeedStyles { get; set; }
        public LineDecoration ForbiddenStyle { get; set; } 

        public UserVisualPreferences()
        {
            ForbiddenStyle = new LineDecoration()
            {
                Label = "FBD",
                AbgrColor = "0xff0051e6",
                Width = 7,
            };
            SpeedStyles = new Dictionary<SpeedMode, LineDecoration>()
            {
                {
                    SpeedMode.CableFerry, new LineDecoration()
                    {
                        Label = "fer",
                        AbgrColor = "0xfffac2a1",
                        Width = 9
                    }
                },
                {
                    SpeedMode.Walk, new LineDecoration()
                    {
                        Label = "wlk",
                        AbgrColor = "0xff2dc0fb",
                        Width = 6
                    }
                },
                {
                    SpeedMode.UrbanSidewalk, new LineDecoration()
                    {
                        Label = "urb",
                        AbgrColor = "0xffd18802",
                        Width = 6
                    }
                },
                {
                    SpeedMode.CarryBike, new LineDecoration()
                    {
                        Label = "crr",
                        AbgrColor = "0xffbdbdbd",
                        Width = 20
                    }
                },
                {
                    SpeedMode.Unknown, new LineDecoration()
                    {
                        Label = "unk",
                        AbgrColor = "0xff2bb4af",
                        Width = 7
                    }
                },
                {
                    SpeedMode.Paved, new LineDecoration()
                    {
                        Label = "pvd",
                        AbgrColor = "0xff757575",
                        Width = 5
                    }
                },
                {
                    SpeedMode.Sand, new LineDecoration()
                    {
                        Label = "snd",
                        AbgrColor = "0xff80dafa",
                        Width = 16
                    }
                },
                {
                    SpeedMode.Ground, new LineDecoration()
                    {
                        Label = "gnd",
                        AbgrColor = "0xffa4aabc",
                        Width = 7
                    }
                },
                {
                    SpeedMode.Asphalt, new LineDecoration()
                    {
                        Label = "asp",
                        AbgrColor = "0xff000000",
                        Width = 1
                    }
                },
                {
                    SpeedMode.HardBlocks, new LineDecoration()
                    {
                        Label = "blk",
                        AbgrColor = "0xffb0279c",
                        Width = 11
                    }
                }

            };
        }

        public void Check()
        {
            if (SpeedStyles == null)
                throw new ArgumentNullException(nameof(SpeedStyles));

            string missing = String.Join(", ", Enum.GetValues<SpeedMode>().Where(it => !SpeedStyles.ContainsKey(it)));
            if (missing != "")
                throw new ArgumentOutOfRangeException($"Missing styles for: {missing}.");
        }

        public override string ToString()
        {
            return new ProxySerializer().Serialize(this);
        }
    }
}

