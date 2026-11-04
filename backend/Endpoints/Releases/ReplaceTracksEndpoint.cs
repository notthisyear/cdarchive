using CdArchiveBackend.Common;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints
{
    // Replaces the track list of one of the user's releases, as edited on its page. A release linked
    // to MusicBrainz loses its link (see ReleaseService.ReplaceTracksAsync). Answers with the
    // ReleaseDetails as they are now.
    internal sealed class ReplaceTracksEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPut("/{releaseId:long}/tracks", async (long releaseId, List<ReleaseTrack>? tracks, ClaimsPrincipal user, ReleaseService releaseService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                if (!ReleaseRules.AreValidTracks(tracks))
                    return Results.BadRequest(new { message = "Each track needs a title and its own position" });

                return UpdateReleaseEndpoint.ToHttpResult(await releaseService.ReplaceTracksAsync(userId, releaseId, tracks!, ct).ConfigureAwait(false));
            });
        }
    }
}
