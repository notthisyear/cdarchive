using CdArchiveBackend.Common;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints.Creators
{
    using RemoveCreatorResult = CreatorService.RemoveCreatorResult;

    // Removes one of the user's creators. Only one no release credits anymore can be removed: 409
    // otherwise, as removing them would take them off those releases.
    internal sealed class RemoveCreatorEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapDelete("/{creatorId:long}", async (long creatorId, ClaimsPrincipal user, CreatorService creatorService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                return await creatorService.RemoveCreatorAsync(userId, creatorId, ct).ConfigureAwait(false) switch
                {
                    RemoveCreatorResult.Removed => Results.NoContent(),
                    RemoveCreatorResult.HasReleases => Results.Conflict(new { message = "The artist is still on a release in your collection" }),
                    _ => Results.NotFound()
                };
            });
        }
    }
}
