using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CdArchiveBackend.Endpoints
{
    using static System.Net.Mime.MediaTypeNames;
    using DbRelease = CdArchiveBackend.Data.Database.Release;

    internal sealed partial class AddRecordEndpoint(string pathToImageStore) : IEndpoint
    {
        private readonly string _pathToImageStore = pathToImageStore;
        private const string ImageTypeCaptureGroupName = "TYPE";

        [GeneratedRegex(@"data:image/(?<" + ImageTypeCaptureGroupName + @">\w+);base64,", RegexOptions.Compiled)]
        private static partial Regex ImageDataPrefixRegex();

        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPost("/add", async (ReleaseData releaseData, ClaimsPrincipal user, ArtistService artistService, RecordsService recordsService) =>
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
                    var isData = releaseData.Summary.ImageUrl.StartsWith("data");
                    bool success;
                    (success, imageName) = isData ?
                        await TryHandleImageData(releaseData.Summary.ImageUrl, _pathToImageStore).ConfigureAwait(false) :
                        await TryHandleImageUrl(releaseData.Summary.ImageUrl, _pathToImageStore).ConfigureAwait(false);

                    if (!success)
                        return Results.BadRequest("Failed to fetch");
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
                    // TODO: Actually fetch the image
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

        private static async Task<(bool success, string imageFileName)> TryHandleImageData(string imageUrl, string pathToImageStore)
        {
            var match = ImageDataPrefixRegex().Match(imageUrl);
            if (!match.Success)
                return (false, string.Empty);

            var imageType = string.Empty;
            foreach (var group in match.Groups.Cast<Group>())
            {
                if (group.Name.Equals(ImageTypeCaptureGroupName, StringComparison.Ordinal))
                {
                    imageType = group.Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(imageType))
                return (false, string.Empty);

            var fileName = $"{Guid.NewGuid()}.{imageType}";
            var path = Path.Combine(pathToImageStore, fileName);
            var data = Convert.FromBase64String(ImageDataPrefixRegex().Replace(imageUrl, ""));

            try
            {
                await File.WriteAllBytesAsync(path, data).ConfigureAwait(false);
                return (true, fileName);
            }
            catch (Exception)
            {
                return (false, string.Empty);
            }
        }

        private static async Task<(bool success, string imageFileName)> TryHandleImageUrl(string imageUrl, string pathToImageStore)
        {
            using var client = new HttpClient();
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(imageUrl);
                if (!response.IsSuccessStatusCode)
                    return (false, string.Empty);
            }
            catch (Exception)
            {
                return (false, string.Empty);
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (string.IsNullOrEmpty(mediaType))
                return (false, string.Empty);

            var fileExtension = mediaType switch
            {
                Image.Jpeg => "jpg",
                Image.Png => "png",
                Image.Tiff => "tiff",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(fileExtension))
                return (false, string.Empty);

            var content = await response.Content.ReadAsByteArrayAsync();
            if (content == default || content.Length == 0)
                return (false, string.Empty);

            var fileName = $"{Guid.NewGuid()}.{fileExtension}";
            var path = Path.Combine(pathToImageStore, fileName);

            try
            {
                await File.WriteAllBytesAsync(path, content);
                return (true, fileName);
            }
            catch (Exception)
            {
                return (false, string.Empty);
            }
        }
    }
}
