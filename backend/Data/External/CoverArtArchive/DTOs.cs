using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CdArchiveBackend.Data.External.CoverArtArchive
{
    internal sealed record CoverArtArchiveImageDto([property: JsonPropertyName("front")] bool Front,
                                                   [property: JsonPropertyName("approved")] bool Approved,
                                                   [property: JsonPropertyName("image")] string? Image,
                                                   [property: JsonPropertyName("thumbnails")] Dictionary<string, string>? Thumbnails);

    internal sealed record CoverArtArchiveResponseDto([property: JsonPropertyName("images")] List<CoverArtArchiveImageDto>? Images,
                                                      [property: JsonPropertyName("release")] string? ReleasePageUrl);

}
