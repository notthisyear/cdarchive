using System.Collections.Generic;

namespace CdArchiveBackend.Data
{
    internal sealed record Artist
    {
        public long Id { get; init; }

        public required string Name { get; init; }

        public string? ImageUrl { get; init; }

        public string? SpotifyUrl { get; init; }

        public ICollection<ReleaseArtist> ReleaseArtists { get; init; } = [];
    }
}
