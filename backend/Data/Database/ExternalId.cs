namespace CdArchiveBackend.Data.Database
{
    public enum ExternalSource
    {
        MusicBrainz,

        // A MusicBrainz release group: the "same" album across its releases (CD, LP, reissues...).
        // Unlike the other ids it's shared by several releases.
        MusicBrainzReleaseGroup,

        Spotify
    }

    // The id a creator has in another service. At most one per service.
    internal sealed class CreatorExternalId
    {
        public long CreatorId { get; set; }

        public ExternalSource Source { get; set; }

        public required string ExternalId { get; set; }

        public Creator Creator { get; set; } = default!;
    }

    // The id a release has in another service. At most one per service.
    internal sealed class ReleaseExternalId
    {
        public long ReleaseId { get; set; }

        public ExternalSource Source { get; set; }

        public required string ExternalId { get; set; }

        public Release Release { get; set; } = default!;
    }
}
