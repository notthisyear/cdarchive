using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Globalization;
using System.Security.Claims;

namespace CdArchiveBackend.Endpoints
{
    internal sealed class GetArtistEndpoint : IEndpoint
    {
        internal const string EndpointName = "GetArtist";
        
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/{artistId}", async (int artistId, ClaimsPrincipal user, RecordsService recordsService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                Console.WriteLine($"received artist request for user {userId} and record id: {artistId}");
                return Results.Ok();
            }).WithName(EndpointName);
        }
    }
}
