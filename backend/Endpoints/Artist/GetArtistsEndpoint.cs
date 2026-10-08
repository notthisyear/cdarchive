using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;

namespace CdArchiveBackend.Endpoints
{
    using DtoArtist = CdArchiveBackend.Data.DTO.Artist;

    internal sealed class GetArtistsEndpoint : IEndpoint
    {

        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapMethods(
                "/",
                [HttpMethods.Query],
                async (List<DtoArtist> artists, ClaimsPrincipal user, ArtistService artistService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                Dictionary<string, List<DtoArtist>> result = [];
                try
                {
                    var artistNames = artists.Select(x => x.Name).Distinct();
                    foreach (var artistName in artistNames)
                    {
                        var actualArtists = await artistService.GetArtistsByName(userId, artistName).ConfigureAwait(false);
                        result.Add(artistName, [.. actualArtists.Select(x => new DtoArtist(x.Id, x.Name, x.CoverImage, x.SpotifyUrl))]);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return Results.InternalServerError("Could not fetch artists");
                }

                return Results.Ok(new { result });
            });
        }
    }
}
