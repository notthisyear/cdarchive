using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Globalization;
using System.Security.Claims;

namespace CdArchiveBackend.Endpoints
{
    internal sealed class GetTotalNumberOfRecords : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/total", async (ClaimsPrincipal user, RecordsService recordsService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                try
                {
                    return Results.Ok(new
                    {
                        total = await recordsService.GetTotalNumberOfRecordsForUser(userId).ConfigureAwait(false)
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
