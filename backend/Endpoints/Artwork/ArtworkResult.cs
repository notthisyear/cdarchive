using CdArchiveBackend.Data.External;
using static CdArchiveBackend.Common.Utilities;

namespace CdArchiveBackend.Endpoints.Artwork
{
    internal readonly record struct ArtworkResult(HttpGetResult GetResult, ArtworkData? ArtworkData);
}
