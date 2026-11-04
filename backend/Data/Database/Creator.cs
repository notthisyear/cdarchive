using System;
using System.Collections.Generic;

namespace CdArchiveBackend.Data.Database
{
    public enum CreatorType
    {
        Group,
        Person,
        Author,
        Filmmaker,
        Other
    }
    
    // A creator in one user's collection. Every user has their own rows, even for the same artist.
    internal sealed class Creator
    {
        public long Id { get; set; }

        public long UserId { get; set; }

        public required string Name { get; set; }

        public required string SortName { get; set; }

        public CreatorType Type { get; set; }

        // ISO 3166-1 alpha-2, or MusicBrainz's XE (Europe) and XW (Worldwide)
        public string? Country { get; set; }

        public string? Notes { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public List<CreatorImage> Images { get; set; } = [];

        // The image to show, one of Images. Set to the first when the creator is added; the user
        // may pick another later. If it's gone, the first by SortOrder is shown.
        public long? PreferredImageId { get; set; }

        public MediaFile? PreferredImage { get; set; }

        public List<CreatorExternalId> ExternalIds { get; set; } = [];

        public List<ReleaseCreator> ReleaseCredits { get; set; } = [];
    }
}
