namespace TrackPlanner.Mapping.Data
{
    public readonly struct CityInfo
    {
        public CityRank Rank { get; }
        public string Name { get; }
        public long Node { get; }

        public CityInfo(CityRank rank, string name, long node)
        {
            Rank = rank;
            Name = name;
            Node = node;
        }

        public void Deconstruct(out CityRank rank, out string name, out long node)
        {
            rank = this.Rank;
            name = this.Name;
            node = this.Node;
        }
    }
}