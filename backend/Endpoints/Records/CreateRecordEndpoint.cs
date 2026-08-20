using CdArchiveBackend.Data;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints
{
    internal sealed class CreateRecordEndpoint : IEndpoint
    {
        private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());
        private readonly Lock _requestIdLock = new();
        private ulong _nextRequestId = 0;

        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPost("/", async (RecordData recordData, ClaimsPrincipal user, ArtistService artistService, RecordsService recordsService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                if (!recordData.Validate())
                    return Results.BadRequest();

                Console.WriteLine($"received record request for user {userId}:");
                // recordData.DebugPrint();

                List<Artist> exactlyMatchingArtists = [];
                Dictionary<string, List<string>> artistsNeedingConfirmation = [];
                try
                {
                    var artistsNames = recordData.Summary.Artists.Select(x => x.Name).Distinct();
                    Console.WriteLine($"Found {artistsNames.Count()} matches");
                    foreach (var artistName in recordData.Summary.Artists)
                    {
                        var actualArtists = await artistService.GetArtistsByName(artistName.Name).ConfigureAwait(false);
                        if (actualArtists.Count == 1 && actualArtists[0].Name.Equals(artistName.Name))
                        {
                            exactlyMatchingArtists.Add(actualArtists[0]);
                            Console.WriteLine("exact match");
                        }
                        else
                        {
                            artistsNeedingConfirmation.Add(artistName.Name, [.. actualArtists.Select(x => x.Name)]);
                            Console.WriteLine("need confirmation");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    return Results.InternalServerError("Could not fetch artists");
                }

                // Here, we need to ask the user to confirm the artists
                if (artistsNeedingConfirmation.Count > 0)
                {
                    ulong requestId;
                    lock (_requestIdLock)
                        requestId = _nextRequestId++;

                    _memoryCache.Set(requestId, recordData);
                    Console.WriteLine("sending response");
                    return Results.Ok(new
                    {
                        status = "artist_clarification",
                        operationId = requestId,
                        question = artistsNeedingConfirmation
                    });
                }

                // Results.CreatedAtRoute(GetRecordEndpoint, new { id = record.id }, record);
                return Results.Ok();
            });
        }
    }
}
