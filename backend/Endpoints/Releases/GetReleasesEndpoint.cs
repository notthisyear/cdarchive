using CdArchiveBackend.Common;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Security.Claims;

namespace CdArchiveBackend.Endpoints
{
    internal sealed class GetReleasesEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/", async (int offset, int limit, ClaimsPrincipal user, ReleaseService releaseService) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();
               
                try
                {
                    return Results.Ok(new
                    {
                        records = await releaseService.GetReleasesForUser(userId, offset, limit).ConfigureAwait(false)
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
