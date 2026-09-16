using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Globalization;
using System.Security.Claims;

namespace CdArchiveBackend.Endpoints
{
    using DtoArtist = CdArchiveBackend.Data.DTO.Artist;

    internal sealed class AddArtistEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPost("/add", async (DtoArtist artist, ClaimsPrincipal user, ImageDownloadService imageDownloadService, ArtistService artistService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                var imageName = string.Empty;
                if (!string.IsNullOrEmpty(artist.ImageUrl))
                {
                    var result = imageDownloadService.AddDownloadRequest(artist.ImageUrl);
                    if (result == null)
                        return Results.BadRequest("Failed to fetch image");

                    imageName = await result.Task.ConfigureAwait(false);
                    if (string.IsNullOrEmpty(imageName))
                        return Results.BadRequest("Failed to fetch image");
                }

                var artistId = await artistService.AddArtist(artist.Name, imageName, artist.SpotifyUrl ?? string.Empty).ConfigureAwait(false);
                return Results.CreatedAtRoute(GetArtistEndpoint.EndpointName, new { artistId });
            });
        }
    }
}
