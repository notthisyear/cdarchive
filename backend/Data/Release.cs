using System;
using System.Collections.Generic;

namespace CdArchiveBackend.Data
{
    internal sealed record Release
    {
        public long Id { get; init; }

        public required string Name { get; init; }

        public DateOnly ReleaseDate { get; init; }

        public long LengthSeconds { get; init; }

        public string? ImageUrl { get; init; }

        public string? SpotifyLink { get; init; }

        public string? Barcode { get; init; }
        
        public string? CatalogNumber { get; init; }
        
        public string? Country { get; init; }

        public string? Format { get; init; }

        public long? PreviousReleaseId { get; init; }

        public long? NextReleaseId { get; init; }

        public ICollection<ReleaseTrack> Tracks { get; init; } = [];

        public ICollection<ReleaseArtist> ReleaseArtists { get; init; } = [];

        public Release? PreviousRelease { get; init; }

        public Release? NextRelease { get; init; }
    }
}
