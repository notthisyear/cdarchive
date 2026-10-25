using System;
using System.ComponentModel.DataAnnotations;

namespace CdArchiveBackend.Data.Uploads
{
    public sealed class Options
    {
        public const string SectionName = "Uploads";

        // Base64 of at least 32 random bytes, for signing links to uploaded files. Kept in user
        // secrets, not in appsettings.json.
        [Required, MinLength(1)]
        public string SigningKey { get; set; } = "";

        public byte[] GetSigningKeyBytes() => Convert.FromBase64String(SigningKey);

        public bool HasValidSigningKey()
        {
            Span<byte> buffer = stackalloc byte[256];
            return Convert.TryFromBase64String(SigningKey, buffer, out var length) && length >= 32;
        }
    }
}
