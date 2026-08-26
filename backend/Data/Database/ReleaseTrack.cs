namespace CdArchiveBackend.Data.Database
{
    internal sealed class ReleaseTrack
    {
        public long ReleaseId { get; set; }

        public int DiscNumber { get; set; }

        public int TrackNumber { get; set; }

        public required string Title { get; set; }

        public int DurationSeconds { get; set; }

        public Release Release { get; set; } = null!;
    }
}
