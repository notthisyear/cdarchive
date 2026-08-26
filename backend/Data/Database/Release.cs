using System;
using System.Collections.Generic;

namespace CdArchiveBackend.Data.Database
{
    internal sealed class Release
    {
        public long Id { get; set; }

        public required string Name { get; set; }

        public DateOnly ReleaseDate { get; set; }

        public long LengthSeconds { get; set; }

        public string? CoverImage { get; set; }

        public string? SpotifyLink { get; set; }

        public string? Barcode { get; set; }

        public string? CatalogNumber { get; set; }

        public string? Country { get; set; }

        public string? Format { get; set; }

        public long? PreviousReleaseId { get; set; }

        public long? NextReleaseId { get; set; }

        public ICollection<ReleaseTrack> Tracks { get; set; } = [];

        public ICollection<ReleaseArtist> ReleaseArtists { get; set; } = [];

        public Release? PreviousRelease { get; set; }

        public Release? NextRelease { get; set; }
    }
}
