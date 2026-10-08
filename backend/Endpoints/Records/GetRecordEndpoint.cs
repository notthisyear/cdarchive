using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Globalization;
using System.Security.Claims;

namespace CdArchiveBackend.Endpoints
{
    internal sealed class GetRecordEndpoint : IEndpoint
    {
        internal const string EndpointName = "GetRecords";

        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/{releaseId}", async (int releaseId, ClaimsPrincipal user, ReleaseService releaseService) =>
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
                    var record = await releaseService.GetReleaseData(releaseId, userId).ConfigureAwait(false);
                    return Results.Ok(new
                    {
                        record
                    });
                }
                catch (Exception)
                {
                    return Results.InternalServerError("Could not fetch record data");
                }
            }).WithName(EndpointName);
        }
    }
}
