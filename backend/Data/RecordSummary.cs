namespace CdArchiveBackend.Data
{
    public readonly record struct RecordSummary
    {
        public long Id { get; init; }

        public string Name { get; init; }

        public Artist Artist { get; init; }

        public int Year { get; init; }

        public string? ImageUrl { get; init; }

        public string? SpotifyLink { get; init; }

        public string ToString(string indent)
        {
            return $"{indent}ID: {Id}\n{indent}Name: {Name}\n{indent}Artist:\n{Artist.ToString($"{indent}\t")}\n{indent}Year: {Year}\n{indent}ImageUrl: {ImageUrl}\n{indent}SpotifyLink: {SpotifyLink}";
        }
    }
}
