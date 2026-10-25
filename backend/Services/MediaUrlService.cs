using CdArchiveBackend.Data.Database;
using Microsoft.Extensions.Options;
using System;
using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace CdArchiveBackend.Services
{
    using UploadOptions = CdArchiveBackend.Data.Uploads.Options;

    // The URLs the frontend uses to show a MediaFile. Shared files are served by nginx straight
    // from the public image folder. Uploads live outside it and are only reachable through a
    // signed link to the upload endpoint, which carries an expiry time and an HMAC-SHA256 over
    // the file id and that time, so it works in an <img src> without the login token.
    internal sealed class MediaUrlService(IOptions<UploadOptions> options, TimeProvider time)
    {
        // The paths as the browser sees them. nginx serves /images/ from the public image folder
        // and passes /api/ on to us without the prefix.
        private const string SharedImagesPath = "/images/";
        private const string UploadsPath = "/api/uploads/";

        private readonly byte[] _signingKey = options.Value.GetSigningKeyBytes();

        public string GetUrl(MediaFile file)
        {
            if (file.OwnerUserId is null)
                return SharedImagesPath + file.FileName;

            // Expires at the end of tomorrow (UTC), so the link stays the same all day and the
            // browser can keep the image cached. A link is valid for 24 to 48 hours.
            var today = new DateTimeOffset(time.GetUtcNow().UtcDateTime.Date, TimeSpan.Zero);
            var expires = today.AddDays(2).ToUnixTimeSeconds();

            return string.Create(CultureInfo.InvariantCulture,
                $"{UploadsPath}{file.Id}?exp={expires}&sig={Sign(file.Id, expires)}");
        }

        public bool IsValidUploadLink(long mediaFileId, long expires, string signature)
        {
            if (GetSecondsUntil(expires) <= 0)
                return false;

            // Constant-time compare, so the check doesn't reveal how much of a guess was right
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(Sign(mediaFileId, expires)),
                Encoding.ASCII.GetBytes(signature));
        }

        public long GetSecondsUntil(long expires) => expires - time.GetUtcNow().ToUnixTimeSeconds();

        private string Sign(long mediaFileId, long expires)
        {
            var message = Encoding.ASCII.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{mediaFileId}.{expires}"));
            return Base64Url.EncodeToString(HMACSHA256.HashData(_signingKey, message));
        }
    }
}
