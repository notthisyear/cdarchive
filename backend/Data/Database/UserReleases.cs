using System;

namespace CdArchiveBackend.Data.Database
{
    internal sealed class UserReleases
    {
        public long UserId { get; set; }

        public long ReleaseId { get; set; }

        public DateOnly AddedDate { get; set; }

        public string? Comment { get; set; }

        public UserData User { get; set; } = null!;

        public Release Release { get; set; } = null!;
    }
}
