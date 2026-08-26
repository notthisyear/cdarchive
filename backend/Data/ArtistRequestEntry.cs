using CdArchiveBackend.Data.DTO;

namespace CdArchiveBackend.Data
{
    public readonly record struct ArtistRequestEntry(long? ArtistId, Artist Artist);
}