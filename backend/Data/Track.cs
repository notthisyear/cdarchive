namespace CdArchiveBackend.Data
{
    public readonly record struct Track
    {
        public int TrackNumber { get; init; }

        public string Title { get; init; }

        public int DurationSeconds { get; init; }

        public string ToString(string indent)
        {
            return $"{indent}TrackNumber: {TrackNumber}\n{indent}Title: {Title}\n{indent}DurationSeconds: {DurationSeconds}";
        }
    }
}
