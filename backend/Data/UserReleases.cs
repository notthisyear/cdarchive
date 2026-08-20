using System;

namespace CdArchiveBackend.Data
{
    internal sealed record UserReleases
    {
        public long UserId { get; init; }

        public long ReleaseId { get; init; }

        public DateOnly AddedDate { get; init; }

        public string? Comment { get; init; }

        public UserData User { get; init; } = null!;

        public Release Release { get; init; } = null!;
    }
}
