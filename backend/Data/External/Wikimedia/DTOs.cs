using CdArchiveBackend.Data.External.MusicBrainz;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CdArchiveBackend.Data.External.Wikimedia
{
    #region Claims
    public sealed record DataValueDto([property: JsonPropertyName("value")] string? Value);
    public sealed record SnakDto([property: JsonPropertyName("datavalue")] DataValueDto? DataValue);
    public sealed record P18Dto([property: JsonPropertyName("mainsnak")] SnakDto? Snak,
                                [property: JsonPropertyName("rank")] string? Rank);
    public sealed record WbClaimDto([property: JsonPropertyName("P18")] List<P18Dto>? P18Claims);
    public sealed record WbGetClaimsDto([property: JsonPropertyName("claims")] WbClaimDto? Claim);
    #endregion

    #region Image query
    public sealed record ExtMetadataValueDto([property: JsonPropertyName("value")] string? Value);
    public sealed record ExtMetadataDto([property: JsonPropertyName("AttributionRequired")] ExtMetadataValueDto? AttributionRequired,
                                        [property: JsonPropertyName("LicenseShortName")] ExtMetadataValueDto? LicenseShortName,
                                        [property: JsonPropertyName("LicenseUrl")] ExtMetadataValueDto? LicenseUrl,
                                        [property: JsonPropertyName("Artist")] ExtMetadataValueDto? Artist);
    public sealed record ImageInfoDto([property: JsonPropertyName("url")] string? Url, 
                                      [property: JsonPropertyName("thumburl")] string? ThumbUrl,
                                      [property: JsonPropertyName("descriptionurl")] string? DescriptionUrl,
                                      [property: JsonPropertyName("extmetadata")] ExtMetadataDto? ExtMetadata);
    public sealed record CommonsPageDto([property: JsonPropertyName("missing")] bool? Missing,
                                        [property: JsonPropertyName("imageinfo")] List<ImageInfoDto?>? ImageInfo);
    public sealed record CommonsQueryDto([property: JsonPropertyName("pages")] List<CommonsPageDto?>? Pages);
    public sealed record CommonsQueryResultDto([property: JsonPropertyName("query")] CommonsQueryDto Query);
    #endregion
}
