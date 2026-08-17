using System;

namespace CdArchiveBackend.Data
{
    public sealed record UserData
    {
        public long Id { get; init; }

        public required string Username { get; init; }

        public required string Email { get; init; }

        public required string PasswordHash { get; init; }

        public DateTime CreatedAt { get; init; }
    }
}
