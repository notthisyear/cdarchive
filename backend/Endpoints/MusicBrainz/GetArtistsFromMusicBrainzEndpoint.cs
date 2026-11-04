using CdArchiveBackend.Common;
using CdArchiveBackend.Services.External;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace CdArchiveBackend.Endpoints
{
    internal sealed class GetArtistsFromMusicBrainzEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet(
                "/artists/stream", (string? name, ClaimsPrincipal user, MusicBrainzService musicBrainzService, CancellationToken ct) =>
                {
                    if (!user.TryExtractUserId(out _))
                        return Results.BadRequest();

                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(name.Trim()))
                        return Results.BadRequest();

                    return TypedResults.ServerSentEvents(StreamLookup(name, musicBrainzService, ct));
                });
        }

        private static async IAsyncEnumerable<SseItem<object>> StreamLookup(string name,
                                                                            MusicBrainzService musicBrainzService,
                                                                            [EnumeratorCancellation] CancellationToken ct)
        {
            var channel = Channel.CreateUnbounded<SseItem<object>>();
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await musicBrainzService.SearchArtistsAsync(name, channelProgress: new ChannelProgress(channel.Writer), ct: ct).ConfigureAwait(false);

                    if (result.GetResult == Utilities.HttpGetResult.RateLimitedOrServiceUnavailable)
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "MusicBrainz is unavailable" }, "error"));
                    else
                        channel.Writer.TryWrite(new SseItem<object>(result.Artists, "result"));
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
