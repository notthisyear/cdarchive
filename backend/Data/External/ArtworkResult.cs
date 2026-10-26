using static CdArchiveBackend.Common.Utilities;

namespace CdArchiveBackend.Data.External
{
    internal readonly record struct ArtworkResult(HttpGetResult GetResult, ArtworkData? ArtworkData);
}
