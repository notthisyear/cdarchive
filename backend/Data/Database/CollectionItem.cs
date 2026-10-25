using System;

namespace CdArchiveBackend.Data.Database
{
    internal sealed class CollectionItem
    {
        public long Id { get; set; }

        public long UserId { get; set; }

        public long ReleaseId { get; set; }

        public string? Notes { get; set; }

        public long? CustomImageId { get; set; }

        public MediaFile? CustomImage { get; set; }

        public DateOnly? AcquiredOn { get; set; }

        public DateTimeOffset AddedAt { get; set; }

        public Release Release { get; set; } = default!;
    }
}
