namespace CdArchiveBackend.Data.Database
{
    internal sealed class ReleaseArtist
    {
        public long ReleaseId { get; set; }

        public long ArtistId { get; set; }

        public Release Release { get; set; } = null!;

        public Artist Artist { get; set; } = null!;
    }
}
