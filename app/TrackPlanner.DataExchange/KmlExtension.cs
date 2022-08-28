using SharpKml.Dom;
using SharpKml.Engine;
using System.IO;
using System.Linq;

namespace TrackPlanner.DataExchange
{
    public static class KmlExtension
    {
        public static void Save(this KmlFile kml,string filename)
        {
            using (FileStream stream = new FileStream(System.IO.Path.GetFullPath(filename), FileMode.CreateNew))
            {
                if ((kml.Root as Document)!.Features.Any())
                {
                    kml.Save(stream);
                }
            }
        }
    }
}
