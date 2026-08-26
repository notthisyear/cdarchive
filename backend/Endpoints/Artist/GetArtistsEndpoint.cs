using CdArchiveBackend.Data.DTO;
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
    internal sealed class GetArtistsEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapMethods(
                "/",
                [HttpMethods.Query],
                async (List<string> artists, ClaimsPrincipal user, ArtistService artistService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                Dictionary<string, List<Artist>> result = [];
                try
                {
                    var artistNames = artists.Distinct();
                    foreach (var artistName in artistNames)
                    {
                        var actualArtists = await artistService.GetArtistsByName(artistName).ConfigureAwait(false);
                        result.Add(artistName, [.. actualArtists.Select(x => new Artist(x.Id, x.Name, x.CoverImage))]);
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
