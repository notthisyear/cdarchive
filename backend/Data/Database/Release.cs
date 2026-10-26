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

    public enum ReleaseStatus
    {
        Owned,

        // Not in the collection, but on the user's wishlist
        Wanted,

        // Known to exist (e.g. from loading an artist's discography), but not in the collection
        NotOwned
    }

    // A release in one user's collection. Every user has their own rows, even for the same album.
    internal sealed class Release
    {
        public const int MinScore = 1;
        public const int MaxScore = 10;

        public long Id { get; set; }

        public long UserId { get; set; }

        public ReleaseStatus Status { get; set; }

        public required string Title { get; set; }

        public ReleaseType Type { get; set; }

        public ReleaseFormat Format { get; set; }

        // The year this particular release came out, e.g. of a remaster
        public int? ReleaseYear { get; set; }

        // The year the album, film or book first came out. What a collection is usually sorted and
        // shown by.
        public int? FirstReleaseYear { get; set; }

        // For records the sum of the tracks, as far as known. For films the runtime.
        public int? DurationSeconds { get; set; }

        public string? Barcode { get; set; }

        // See ReleaseFingerprint. For telling whether the user already has a release without a
        // MusicBrainz id. Has to be computed again whenever what goes into it changes.
        public required string Fingerprint { get; set; }

        public string? Notes { get; set; }

        // The user's own rating, MinScore to MaxScore. Null until they've rated it.
        public int? Score { get; set; }

        public DateOnly? AcquiredOn { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public List<ReleaseExternalId> ExternalIds { get; set; } = [];

        public List<ReleaseCreator> Credits { get; set; } = [];

        public List<Track> Tracks { get; set; } = [];

        public List<ReleaseImage> Images { get; set; } = [];

        // The image to show, one of Images. Set to the first (the cover) when the release is added;
        // the user may pick another later. If it's gone, the first by SortOrder is shown.
        public long? PreferredImageId { get; set; }

        public MediaFile? PreferredImage { get; set; }
    }
}
