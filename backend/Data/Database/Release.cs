using System;
using System.Collections.Generic;

namespace CdArchiveBackend.Data.Database
{
    public enum ReleaseType
    {
        Record,
        Book,
        Film 
    }

    public enum ReleaseFormat
    {
        Unknown,
        Cassette,
        Vinyl,
        Cd,
        Hardcover,
        Paperback,
        Vhs,
        Dvd,
        BluRay,
        Digital,
        Other,
    }

    internal sealed class Release
    {
        public long Id { get; set; }

        public required string Title { get; set; }

        public ReleaseType Type { get; set; }

        public ReleaseFormat Format { get; set; }

        public DateOnly? ReleaseDate { get; set; }

        public string? Barcode { get; set; }

        public long? DefaultImageId { get; set; }

        public MediaFile? DefaultImage { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public List<ReleaseExternalId> ExternalIds { get; set; } = [];

        public List<ReleaseCreator> Credits { get; set; } = [];

        public List<Track> Tracks { get; set; } = [];
        
        public long? PreviousReleaseId { get; set; }

        public long? NextReleaseId { get; set; }

        public Release? PreviousRelease { get; set; }

        public Release? NextRelease { get; set; }
    }
}
