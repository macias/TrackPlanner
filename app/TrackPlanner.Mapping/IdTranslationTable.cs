using System.Collections.Generic;


#nullable enable

namespace TrackPlanner.Mapping
{
   // translations between OSM identifiers and grid-based indices
   internal sealed class IdTranslationTable
   {
      private readonly Dictionary<long,WorldIdentifier> osmToGrid;
      private readonly Dictionary<WorldIdentifier,long> gridToOsm;

      public IdTranslationTable()
      {
         this.osmToGrid = new Dictionary<long, WorldIdentifier>();
         this.gridToOsm = new Dictionary< WorldIdentifier,long>();
      }

      public void Add(long osmId,WorldIdentifier worldId)
      {
         this.osmToGrid.Add(osmId,worldId);
         this.gridToOsm.Add(worldId,osmId);
      }

      public WorldIdentifier Get(long osmId)
      {
         return this.osmToGrid[osmId];
      }
   }
}