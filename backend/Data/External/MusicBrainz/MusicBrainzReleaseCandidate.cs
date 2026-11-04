using System.Collections.Generic;

namespace CdArchiveBackend.Data.External.MusicBrainz
{
    public sealed record MusicBrainzReleaseCandidate(string Id,
                                                     string ReleaseGroupId,
                                                     string Title,
                                                     string ArtistCombined,
                                                     // Parallel arrays, one entry per credited artist. A join
                                                     // phrase is what follows the artist's name (" & ", " feat. ").
                                                     string[] Artists,
                                                     string[] ArtistIds,
                                                     string[] JoinPhrases,
                                                     string? FirstReleaseDate,
                                                     string? Date,
                                                     string? ReleaseType,
                                                     string? MediaType,
                                                     string? Country,
                                                     string? Barcode,
                                                     string? Disambiguation,
                                                     int TrackCount);

}
