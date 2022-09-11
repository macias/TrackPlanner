using System;
using System.IO;

namespace TrackPlanner.Mapping.Disk
{
    internal sealed class OffsetKeeper : IDisposable
    {
        private readonly BinaryWriter writer;
        private readonly long offsetPosition;

        public OffsetKeeper(BinaryWriter writer)
        {
            this.writer = writer;
            this.offsetPosition = writer.BaseStream.Position;
            writer.Write(-1L);
        }

        public void Dispose()
        {
                var curr_pos = writer.BaseStream.Position;
                writer.BaseStream.Seek(this.offsetPosition, SeekOrigin.Begin);
                writer.Write(curr_pos);
                writer.BaseStream.Seek(curr_pos, SeekOrigin.Begin);
        }
    }
}
