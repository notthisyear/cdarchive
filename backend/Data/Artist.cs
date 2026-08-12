namespace CdArchiveBackend.Data
{
    public readonly record struct Artist
    {
        public long Id { get; init; }

        public string Name { get; init; }

        public string? ImageUrl { get; init; }

        public string? SpotifyLink { get; init; }

        public string ToString(string indent)
        {
            return $"{indent}ID: {Id}\n{indent}Name: {Name}\n{indent}ImageUrl: {ImageUrl ?? string.Empty}\n{indent}SpotifyLink: {SpotifyLink ?? string.Empty}";
        }
    }
}
