using System;

namespace CdArchiveBackend.Data.Database
{
    internal sealed class UserData
    {
        public long Id { get; set; }

        public required string Username { get; set; }

        public required string Email { get; set; }

        public required string PasswordHash { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
