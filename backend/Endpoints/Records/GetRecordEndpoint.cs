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
            groupBuilder.MapGet("/{recordId}", async (int recordId, ClaimsPrincipal user, RecordsService recordsService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                Console.WriteLine($"received record request for user {userId} and record id: {recordId}");
                return Results.Ok();
                //(new
                //{
                //    record = await recordsService.GetRecordData(recordId).ConfigureAwait(false)
                //});
            }).WithName(EndpointName);
        }
    }
}
