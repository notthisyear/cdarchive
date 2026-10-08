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
using System.Threading.Tasks;

namespace CdArchiveBackend.Endpoints
{
    using DbRelease = CdArchiveBackend.Data.Database.Release;
    using ImgDlRes = ImageDownloadService.Result;

    internal sealed class AddRecordEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPost("/add", async (ReleaseData releaseData, ClaimsPrincipal user, ImageDownloadService imageDownloadService, ArtistService artistService, ReleaseService recordsService) =>
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

                var imageDownloadResult = string.IsNullOrEmpty(releaseData.Summary.ImageUrl) ?
                    new(false) :
                    await TryDownloadImage(releaseData.Summary.ImageUrl, imageDownloadService).ConfigureAwait(false);

                if (imageDownloadResult.DownloadFailed)
                    return Results.BadRequest();

                // If this exact record already exists, we can just use that instead
                var (success, matchingId) = await TryCheckForExistingRecord(releaseData, imageDownloadResult, imageDownloadService, recordsService);

                if (!success)
                    return Results.BadRequest();

                if (matchingId.HasValue)
                {
                    // If the didn't already have this release, add it to the collection
                    if (!await recordsService.UserHasRelease(matchingId.Value, userId).ConfigureAwait(false))
                    {
                        await recordsService.AddArtistsForRelease(matchingId.Value, userId, [.. releaseData.Summary.Artists.Select(x => (long)x.Id!)], deferUpdate: true).ConfigureAwait(false);
                        await recordsService.AddReleaseForUser(matchingId.Value, userId, deferUpdate: false).ConfigureAwait(false);
                    }

                    return Results.CreatedAtRoute(GetRecordEndpoint.EndpointName, new { releaseId = matchingId.Value });
                }

                // Create the actual release
                var releaseId = await recordsService.CreateRelease(new DbRelease()
                {
                    Name = releaseData.Summary.Name,
                    ReleaseDate = new DateOnly(releaseData.Summary.Year, 1, 1),
                    LengthSeconds = releaseData.DurationSeconds,
                    CoverImage = imageDownloadResult.ImageName,
                    SpotifyLink = releaseData.Summary.SpotifyLink
                }).ConfigureAwait(false);

                // Add artist entries for this particular release 
                await recordsService.AddArtistsForRelease(releaseId, userId, [.. releaseData.Summary.Artists.Select(x => (long)x.Id!)], deferUpdate: true).ConfigureAwait(false);

                // Add tracks
                await recordsService.AddTracksForRelease(releaseId, releaseData.Tracks, deferUpdate: true).ConfigureAwait(false);

                // Add this release to the current user
                await recordsService.AddReleaseForUser(releaseId, userId, deferUpdate: false).ConfigureAwait(false);

                return Results.CreatedAtRoute(GetRecordEndpoint.EndpointName, new { releaseId });
            });
        }

        private static Task<ImgDlRes> TryDownloadImage(string imageUrl, ImageDownloadService imageDownloadService)
        {
            var r = imageDownloadService.AddDownloadRequest(imageUrl);
            if (r == null)
                return Task.FromResult(new ImgDlRes(true));

            return r.Task;
        }

        private static async Task<(bool success, long? id)> TryCheckForExistingRecord(ReleaseData newReleaseData,
                                                                                      ImgDlRes imageDownloadResult,
                                                                                      ImageDownloadService imageDownloadService,
                                                                                      ReleaseService recordsService)
        {
            var matchingIds = await recordsService.GetReleasesMatchingName(newReleaseData.Summary.Name).ConfigureAwait(false);
            if (matchingIds.Count == 0)
                return (true, null);

            foreach (var id in matchingIds)
            {
                var r = await recordsService.GetReleaseData(id, null);

                // If both the existing and the new release has images, we need to load them to compare them
                if (!string.IsNullOrEmpty(imageDownloadResult.ImageName) && !string.IsNullOrEmpty(r.Summary.ImageUrl))
                {
                    if (!imageDownloadService.TryGetSha1ForLocalImage(imageDownloadResult.ImageName, out string newSha1))
                        return (false, null);
                    if (!imageDownloadService.TryGetSha1ForLocalImage(r.Summary.ImageUrl, out string existingSha1))
                        return (false, null);

                    // If the don't match, they're not an exact match
                    if (!newSha1.Equals(existingSha1, StringComparison.Ordinal))
                        continue;

                    if (newReleaseData.IsSameRelease(r))
                        return (true, id);
                }
                // Other option is that both are lacking an image
                else if (string.IsNullOrEmpty(imageDownloadResult.ImageName) && string.IsNullOrEmpty(r.Summary.ImageUrl))
                {
                    if (newReleaseData.IsSameRelease(r))
                        return (true, id);
                }
            }
            return (true, null);
        }
    }
}
