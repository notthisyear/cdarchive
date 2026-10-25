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

namespace CdArchiveBackend.Endpoints.Artwork
{
    internal sealed class GetArtistArtworkEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/artist/stream", (string? mbid, ClaimsPrincipal user, MusicBrainzService musicBrainzService, WikimediaService wikimediaService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out _))
                    return Results.BadRequest();

                if (string.IsNullOrEmpty(mbid))
                    return Results.BadRequest();

                return TypedResults.ServerSentEvents(StreamLookup(mbid, musicBrainzService, wikimediaService, ct));
            });
        }

        private static async IAsyncEnumerable<SseItem<object>> StreamLookup(string mbid,
                                                                            MusicBrainzService musicBrainzService,
                                                                            WikimediaService wikimediaService,
                                                                            [EnumeratorCancellation] CancellationToken ct)
        {
            var channel = Channel.CreateUnbounded<SseItem<object>>();
            _ = Task.Run(async () =>
            {
                try
                {
                    var channelProgress = new ChannelProgress(channel.Writer);
                    var entityId = await musicBrainzService.GetWikidataEntityIdAsync(mbid, channelProgress, ct).ConfigureAwait(false);

                    if (string.IsNullOrEmpty(entityId))
                    {
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "Could not get Wikidata ID for artist" }, "error"));
                        return;
                    }

                    var result = await wikimediaService.GetImageAsync(entityId, channelProgress, ct).ConfigureAwait(false);
                    if (result.GetResult == Utilities.HttpGetResult.RateLimitedOrServiceUnavailable)
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "CovertArtArchive unavailable" }, "error"));
                    else if (result.ArtworkData != null)
                        channel.Writer.TryWrite(new SseItem<object>(result.ArtworkData, "result"));
                    else
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "Fetching covert art failed" }, "error"));
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
