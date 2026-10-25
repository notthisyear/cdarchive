using CdArchiveBackend.Data.Database;

namespace CdArchiveBackend.Data.External.MusicBrainz
{
    internal sealed record MusicBrainzArtist(
        string Id,
        string Name,
        string? SortName,
        CreatorType? Type,
        string Country,
        string Disambiguation);
}
