using System;

namespace CdArchiveBackend.Data.Database
{
    public enum MediaSource
    {
        Upload,
        Wikimedia,
        CoverArtArchive,
        Spotify,
        Other
    }

    // A file we store (an artist photo, a cover...) together with where it came from and the
    // credit its license asks for. Files the user uploaded themselves have no license info.
    internal sealed class MediaFile
    {
        public long Id { get; set; }

        // The user who uploaded it, or null for a file we downloaded that everyone shares. Also
        // decides where the file lives: shared files in the public image folder, uploads in a
        // folder of their own per user, outside of what the web server serves directly.
        public long? OwnerUserId { get; set; }

        // "<guid>.<ext>", in the folder given by OwnerUserId
        public required string FileName { get; set; }

        public required string ContentType { get; set; }

        public int ByteCount { get; set; }

        // Hex SHA-1 of the content, so the same file is only stored once per owner
        public required string Sha1 { get; set; }

        public MediaSource Source { get; set; }

        public string? OriginalUrl { get; set; }

        public string? SourcePageUrl { get; set; }

        public string? Title { get; set; }

        public string? Author { get; set; }

        public string? License { get; set; }

        public string? LicenseUrl { get; set; }

        public bool AttributionRequired { get; set; }

        public DateTimeOffset CreatedAt { get; set; }
    }
}
