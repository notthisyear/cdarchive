using CdArchiveBackend.Data.Database;
using System;

namespace CdArchiveBackend.Data.DTO
{
    // An image as the frontend shows it, with the credit its license asks for
    public sealed record ImageInfo(
        string Url,
        string? Author,
        string? License,
        string? LicenseUrl,
        string? SourcePageUrl,
        bool AttributionRequired);

    // An image to attach to something being saved.
    //  Source = Upload: FileIndex is the index of the file among the files sent with the request.
    //  Source = CoverArtArchive/Wikimedia: ExternalId is the MusicBrainz id the image was looked up
    //  by, a release's for a cover and an artist's for a photo. The backend looks it up again rather
    //  than trusting a URL from the client.
    public sealed record NewImage(MediaSource Source, string? ExternalId, int? FileIndex)
    {
        public bool Validate(int fileCount)
            => Source switch
            {
                MediaSource.Upload => ExternalId is null && FileIndex >= 0 && FileIndex < fileCount,
                // A GUID, which also keeps it safe to put in a URL
                MediaSource.CoverArtArchive or MediaSource.Wikimedia => FileIndex is null && Guid.TryParse(ExternalId, out _),
                _ => false
            };
    }
}
