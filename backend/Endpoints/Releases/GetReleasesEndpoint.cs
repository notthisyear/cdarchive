using CdArchiveBackend.Common;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints
{
    // One page of the user's collection, as { releases: [ReleaseSummary...] }
    internal sealed class GetReleasesEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/", async (int offset, int limit, ClaimsPrincipal user, ReleaseService releaseService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                if (offset < 0 || limit < 1 || limit > ReleaseService.MaxPageSize)
                    return Results.BadRequest();

                return Results.Ok(new
                {
                    releases = await releaseService.GetReleasesForUserAsync(userId, offset, limit, ct).ConfigureAwait(false)
                });
            });
        }
    }
}
