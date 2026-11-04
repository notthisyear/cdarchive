using CdArchiveBackend.Common;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints.Creators
{
    // One creator in the user's collection, as a CreatorSummary
    internal sealed class GetCreatorEndpoint : IEndpoint
    {
        internal const string EndpointName = "GetCreator";

        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/{creatorId:long}", async (long creatorId, ClaimsPrincipal user, CreatorService creatorService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                var creator = await creatorService.GetCreatorAsync(userId, creatorId, ct).ConfigureAwait(false);
                return creator is null ? Results.NotFound() : Results.Ok(creator);
            }).WithName(EndpointName);
        }
    }
}
