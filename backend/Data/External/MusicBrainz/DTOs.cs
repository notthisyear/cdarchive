using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CdArchiveBackend.Data.External.MusicBrainz
{
    #region Artist data
    public sealed record ArtistDto([property: JsonPropertyName("id")] string Id,
                                   [property: JsonPropertyName("name")] string Name,
                                   [property: JsonPropertyName("score")] int Score,
                                   [property: JsonPropertyName("type")] string? Type,
                                   [property: JsonPropertyName("sort-name")] string? SortName,
                                   [property: JsonPropertyName("country")] string? Country,
                                   [property: JsonPropertyName("disambiguation")] string? Disambiguation);

    public sealed record CreditDto([property: JsonPropertyName("joinphrase")] string? JoinPhrase,
                                   [property: JsonPropertyName("name")] string Name,
                                   [property: JsonPropertyName("artist")] ArtistDto? ArtistDto);

    public sealed record ArtistCandidateDto([property: JsonPropertyName("artists")] List<ArtistDto>? ArtistList);
    #endregion

    #region Release data
    public sealed record ReleaseMediumDto([property: JsonPropertyName("format")] string? Format);

    public sealed record ReleaseGroupRefDto([property: JsonPropertyName("id")] string Id,
                                            [property: JsonPropertyName("primary-type")] string? PrimaryType);

    public sealed record ReleaseCandidateDto([property: JsonPropertyName("id")] string Id,
                                             [property: JsonPropertyName("title")] string Title,
                                             [property: JsonPropertyName("date")] string? Date,
                                             [property: JsonPropertyName("track-count")] int? TrackCount,
                                             [property: JsonPropertyName("country")] string? Country,
                                             [property: JsonPropertyName("disambiguation")] string? Disambiguation,
                                             [property: JsonPropertyName("release-group")] ReleaseGroupRefDto? ReleaseGroup,
                                             [property: JsonPropertyName("artist-credit")] List<CreditDto>? ArtistCredit,
                                             [property: JsonPropertyName("media")] List<ReleaseMediumDto>? Media);

    public sealed record ReleaseCandidateSearchDto([property: JsonPropertyName("count")] int Count,
                                                   [property: JsonPropertyName("releases")] List<ReleaseCandidateDto>? Releases);
    #endregion

    #region Track data
    public sealed record TrackDto([property: JsonPropertyName("title")] string Title,
                                  [property: JsonPropertyName("number")] string? Number,
                                  [property: JsonPropertyName("position")] int? Position,
                                  [property: JsonPropertyName("length")] int? LengthMs);

    public sealed record TracksDto([property: JsonPropertyName("tracks")] List<TrackDto>? Tracks);

    public sealed record TrackListDto([property: JsonPropertyName("media")] List<TracksDto>? Media);
    #endregion

    #region Relations data
    public sealed record RelationsUrlDto([property: JsonPropertyName("resource")] string? Resource);
    
    public sealed record RelationDto([property: JsonPropertyName("type")] string? Type,
                                     [property: JsonPropertyName("url")] RelationsUrlDto? Url);
    public sealed record RelationsDto([property: JsonPropertyName("relations")] List<RelationDto>? Relations);
    #endregion
}
