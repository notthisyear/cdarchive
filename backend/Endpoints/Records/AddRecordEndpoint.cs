using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CdArchiveBackend.Endpoints
{
    using DbRelease = CdArchiveBackend.Data.Database.Release;

    internal sealed class AddRecordEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPost("/add", async (ReleaseData releaseData, ClaimsPrincipal user, ImageDownloadService imageDownloadService, ArtistService artistService, RecordsService recordsService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                if (!releaseData.Validate())
                    return Results.BadRequest();

                releaseData.DebugPrint();

                var imageName = string.Empty;
                if (!string.IsNullOrEmpty(releaseData.Summary.ImageUrl))
                {
                    var result = imageDownloadService.AddDownloadRequest(releaseData.Summary.ImageUrl);
                    if (result == null)
                        return Results.BadRequest("Failed to fetch image");

                    imageName = await result.Task.ConfigureAwait(false);
                    if (string.IsNullOrEmpty(imageName))
                        return Results.BadRequest("Failed to fetch image");
                }

                // Create the actual release
                var releaseId = await recordsService.CreateRecord(new DbRelease()
                {
                    Name = releaseData.Summary.Name,
                    ReleaseDate = new DateOnly(releaseData.Summary.Year, 1, 1),
                    LengthSeconds = releaseData.DurationSeconds,
                    CoverImage = imageName,
                    SpotifyLink = releaseData.Summary.SpotifyLink
                }).ConfigureAwait(false);


                // Get artist ids, create new if needed
                List<long> artistIds = [];
                foreach (var artist in releaseData.Summary.Artists)
                {
                    // TODO: Actually fetch the artist image
                    artistIds.Add(artist.Id ?? await artistService.AddArtist(artist.Name, artist.ImageUrl ?? string.Empty).ConfigureAwait(false));
                }

                // Add artist entries for this particular release 
                await recordsService.AddArtistsForRelease(releaseId, artistIds, deferUpdate: true).ConfigureAwait(false);

                // Add tracks
                await recordsService.AddTracksForRelease(releaseId, releaseData.Tracks, deferUpdate: true).ConfigureAwait(false);

                // Add this release to the current user
                await recordsService.AddReleaseForUser(releaseId, userId, deferUpdate: false).ConfigureAwait(false);

                return Results.CreatedAtRoute(GetRecordEndpoint.EndpointName, new { releaseId });
            });
        }
    }
}
