using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Globalization;
using System.Security.Claims;

namespace CdArchiveBackend.Endpoints
{
    using DtoArtist = CdArchiveBackend.Data.DTO.Artist;

    internal sealed class GetArtistEndpoint : IEndpoint
    {
        internal const string EndpointName = "GetArtist";

        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/{artistId}", async (long artistId, ClaimsPrincipal user, ArtistService artistService) =>
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
                    var artistOrNull = await artistService.GetArtistById(userId, artistId).ConfigureAwait(false);
                    return Results.Ok(new
                    {
                        success = artistOrNull != null,
                        artist = new DtoArtist()
                        {
                            Name = artistOrNull?.Name ?? string.Empty,
                            Id = artistOrNull?.Id ?? 0,
                            ImageUrl = artistOrNull?.CoverImage ?? string.Empty
                        }
                    });
                }
                catch (Exception)
                {
                    return Results.InternalServerError("Could not fetch artist");
                }
            }).WithName(EndpointName);
        }
    }
}
