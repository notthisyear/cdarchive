namespace CdArchiveBackend.Data.External
{
    public sealed record ArtworkData(
        string Title,                // The title of the image
        string Url,                  // a ~500px version, ready to use in <img src>
        string? OriginalUrl,         // full-size file
        string Source,               // "wikimedia" | "coverartarchive"
        string? Author,              // plain text, may be null
        string? License,             // e.g. "CC BY-SA 4.0"
        string? LicenseUrl,
        string? SourcePageUrl,       // where the image (and its credits) live
        bool AttributionRequired);   // when true, show Author + License (linked) next to the image
}
