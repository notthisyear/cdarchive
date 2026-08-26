using System.Collections.Generic;

namespace CdArchiveBackend.Data.Database
{
    internal sealed class Artist
    {
        public long Id { get; set; }

        public required string Name { get; set; }

        public string? CoverImage { get; set; }

        public string? SpotifyUrl { get; set; }

        public ICollection<ReleaseArtist> ReleaseArtists { get; set; } = [];
    }
}
