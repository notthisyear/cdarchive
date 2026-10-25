using System.Collections.Generic;

namespace CdArchiveBackend.Data.External.MusicBrainz
{
    public sealed record MusicBrainzReleaseCandidate(string Id,
                                                     string ReleaseGroupId,
                                                     string Title,
                                                     string ArtistCombined,
                                                     string[] Artists,
                                                     string[] ArtistIds,
                                                     string? Date,
                                                     string? ReleaseType,
                                                     string? MediaType,
                                                     string? Country,
                                                     string? Disambiguation,
                                                     int TrackCount);

}
