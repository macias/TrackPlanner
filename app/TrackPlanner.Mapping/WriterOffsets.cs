using MathUnit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using OsmSharp;
using OsmSharp.IO.PBF;
using TrackPlanner.Shared;
using TrackPlanner.LinqExtensions;
using TrackPlanner.Mapping.Data;

namespace TrackPlanner.Mapping
{
    public sealed class WriterOffsets<TKey>
    where TKey:notnull
    {
        private readonly BinaryWriter writer;
        private readonly Dictionary<TKey, (long placeholder, long offset)> offsets;

        public WriterOffsets(BinaryWriter writer)
        {
            this.writer = writer;
            this.offsets = new Dictionary<TKey, (long placeholder, long offset)>();
        }

        public void Register(TKey key)
        {
            this.offsets.Add(key, (writer.BaseStream.Position, -1L));
            writer.Write(-1L);
        }

        public long AddOffset(TKey key)
        {
            var pos = this.writer.BaseStream.Position;
            offsets[key] = (this.offsets[key].placeholder, pos);
            return pos;
        }

        public void WriteBackOffsets()
        {
            var current_position = writer.BaseStream.Position;

            foreach (var (placeholder, offset) in this.offsets.Values)
            {
                writer.BaseStream.Seek(placeholder, SeekOrigin.Begin);
                writer.Write(offset);
            }

            writer.BaseStream.Seek(current_position, SeekOrigin.Begin);
        }
    }
}