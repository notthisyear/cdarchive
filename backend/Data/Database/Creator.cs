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
    
    internal sealed class Creator
    {
        public long Id { get; set; }
        
        public required string Name { get; set; }
        
        public required string SortName { get; set; }
        
        public CreatorType Type { get; set; }

        // ISO 3166-1 alpha-2, or MusicBrainz's XE (Europe) and XW (Worldwide)
        public string? Country { get; set; }

        public long? DefaultImageId { get; set; }

        public MediaFile? DefaultImage { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public List<CreatorExternalId> ExternalIds { get; set; } = [];

        public List<ReleaseCreator> ReleaseCredits { get; set; } = [];
    }
}
