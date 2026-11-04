using CdArchiveBackend.Common;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints
{
    // One release in the user's collection, as ReleaseDetails
    internal sealed class GetReleaseEndpoint : IEndpoint
    {
        internal const string EndpointName = "GetRelease";

        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/{releaseId:long}", async (long releaseId, ClaimsPrincipal user, ReleaseService releaseService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                // Not found also for another user's release, so nobody can tell which ids exist
                var release = await releaseService.GetReleaseAsync(userId, releaseId, ct).ConfigureAwait(false);
                return release is null ? Results.NotFound() : Results.Ok(release);
            }).WithName(EndpointName);
        }
    }
}
