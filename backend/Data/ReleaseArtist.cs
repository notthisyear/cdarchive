namespace CdArchiveBackend.Data
{
    internal sealed record ReleaseArtist
    {
        public long ReleaseId { get; init; }

        public long ArtistId { get; init; }

        public Release Release { get; init; } = null!;

        public Artist Artist { get; init; } = null!;
    }
}
