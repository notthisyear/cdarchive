using CdArchiveBackend.Common;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints
{
    // Removes one of the user's releases. Answers with a RemovedRelease: the creators it credited
    // who are on none of the user's releases anymore, which the user may want to remove too.
    internal sealed class RemoveReleaseEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapDelete("/{releaseId:long}", async (long releaseId, ClaimsPrincipal user, ReleaseService releaseService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                // Not found also for another user's release, as for getting one
                var removed = await releaseService.RemoveReleaseAsync(userId, releaseId, ct).ConfigureAwait(false);
                return removed is null ? Results.NotFound() : Results.Ok(removed);
            });
        }
    }
}
