using System;

namespace CdArchiveBackend.Data.Database
{
    public sealed class UserData
    {
        public long Id { get; set; }

        public required string Username { get; set; }

        public required string Email { get; set; }

        public required string PasswordHash { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
