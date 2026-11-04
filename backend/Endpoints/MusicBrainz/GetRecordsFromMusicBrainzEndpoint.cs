using CdArchiveBackend.Common;
using CdArchiveBackend.Services.External;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static CdArchiveBackend.Common.Utilities;

namespace CdArchiveBackend.Endpoints
{
    internal sealed class GetRecordsFromMusicBrainzEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet(
                "/records/stream",
                (string? title,
                [FromQuery] string[]? artists,
                string? releaseDate,
                int? totalTracks,
                string? releaseType,
                ClaimsPrincipal user,
                MusicBrainzService musicBrainzService,
                CancellationToken ct) =>
                {

                    if (!user.TryExtractUserId(out _))
                        return Results.BadRequest();

                    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(title.Trim()))
                        return Results.BadRequest();

                    if (!DateOnly.TryParseExact(releaseDate ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        return Results.BadRequest();


                    return TypedResults.ServerSentEvents(StreamLookup(title ?? string.Empty, artists ?? [], dt, totalTracks ?? 0, releaseType ?? string.Empty, musicBrainzService, ct));
                });
        }

        private static async IAsyncEnumerable<SseItem<object>> StreamLookup(string title,
                                                                            string[] artist,
                                                                            DateOnly releaseDate,
                                                                            int totalTracks,
                                                                            string releaseType,
                                                                            MusicBrainzService musicBrainzService,
                                                                            [EnumeratorCancellation] CancellationToken ct)
        {
            var channel = Channel.CreateUnbounded<SseItem<object>>();
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await musicBrainzService.SearchReleaseCandidatesAsync(
                        title,
                        artist,
                        releaseDate,
                        totalTracks,
                        releaseType,
                        channelProgress: new ChannelProgress(channel.Writer), ct: ct).ConfigureAwait(false);

                    if (result.GetResult == HttpGetResult.RateLimitedOrServiceUnavailable)
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "MusicBrainz is unavailable" }, "error"));
                    else
                        channel.Writer.TryWrite(new SseItem<object>(result.Candidates, "result"));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    channel.Writer.TryWrite(new SseItem<object>(new { message = "MusicBrainz is unavailable" }, "error"));
                }
                finally
                {
                    channel.Writer.Complete();
                }
            }, ct);

            await foreach (var item in channel.Reader.ReadAllAsync(ct))
                yield return item;
        }
    }
}
