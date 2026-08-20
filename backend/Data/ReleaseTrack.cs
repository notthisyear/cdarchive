namespace CdArchiveBackend.Data
{
    internal sealed record ReleaseTrack
    {
        public long ReleaseId { get; init; }

        public int TrackNumber { get; init; }

        public required string Title { get; init; }

        public int DurationSeconds { get; init; }

        public Release Release { get; init; } = null!;
    }
}
