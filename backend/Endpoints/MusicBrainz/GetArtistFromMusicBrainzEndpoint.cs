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
    internal sealed class GetArtistFromMusicBrainzEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet(
                "/artist/stream", (string? mbid, ClaimsPrincipal user, MusicBrainzService musicBrainzService, CancellationToken ct) =>
                {
                    if (!user.TryExtractUserId(out _))
                        return Results.BadRequest();

                    // Note: This mbid should have the shape of a Guid, so we check that before proceeding
                    if (string.IsNullOrEmpty(mbid) || !Guid.TryParse(mbid, out _))
                        return Results.BadRequest();

                    return TypedResults.ServerSentEvents(StreamLookup(mbid, musicBrainzService, ct));
                });
        }

        private static async IAsyncEnumerable<SseItem<object>> StreamLookup(string artistMbid,
                                                                            MusicBrainzService musicBrainzService,
                                                                            [EnumeratorCancellation] CancellationToken ct)
        {
            var channel = Channel.CreateUnbounded<SseItem<object>>();
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await musicBrainzService.GetArtistAsync(artistMbid, new ChannelProgress(channel.Writer), ct).ConfigureAwait(false);

                    // An mbid MusicBrainz doesn't know is an answer, not a failure: a null result,
                    // which the frontend shows as "MusicBrainz doesn't know this artist"
                    if (result.GetResult == Utilities.HttpGetResult.RateLimitedOrServiceUnavailable)
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "MusicBrainz is unavailable" }, "error"));
                    else
                        channel.Writer.TryWrite(new SseItem<object>(result.Artists.Count > 0 ? result.Artists[0] : null!, "result"));
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
