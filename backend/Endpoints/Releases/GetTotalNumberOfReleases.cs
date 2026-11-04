using CdArchiveBackend.Common;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Security.Claims;

namespace CdArchiveBackend.Endpoints
{
    internal sealed class GetTotalNumberOfReleases : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/total", async (ClaimsPrincipal user, ReleaseService recordsService) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                try
                {
                    return Results.Ok(new
                    {
                        total = await recordsService.GetTotalNumberOfReleasesForUser(userId).ConfigureAwait(false)
                    });
                }
                catch (Exception)
                {
                    return Results.InternalServerError("Could not fetch record collection");
                }
            }).RequireAuthorization();
        }
    }
}
