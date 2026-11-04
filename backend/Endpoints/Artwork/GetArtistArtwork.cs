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

                // A GUID, which also keeps it safe to put in the MusicBrainz URL
                if (string.IsNullOrEmpty(mbid) || !Guid.TryParse(mbid, out _))
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
                    var (getResult, entityId) = await musicBrainzService.GetWikidataEntityIdAsync(mbid, channelProgress, ct).ConfigureAwait(false);

                    if (getResult == Utilities.HttpGetResult.RateLimitedOrServiceUnavailable)
                    {
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "MusicBrainz is unavailable" }, "error"));
                        return;
                    }

                    // No photo is an answer, not a failure: a null result, which the frontend
                    // shows as "no image found" rather than offering to try again. That's when
                    // MusicBrainz doesn't know the artist or their Wikidata entity, or the
                    // entity has no photo.
                    if (string.IsNullOrEmpty(entityId))
                    {
                        channel.Writer.TryWrite(new SseItem<object>(null!, "result"));
                        return;
                    }

                    var result = await wikimediaService.GetImageAsync(entityId, channelProgress, ct).ConfigureAwait(false);
                    if (result.GetResult == Utilities.HttpGetResult.RateLimitedOrServiceUnavailable)
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "Wikimedia unavailable" }, "error"));
                    else
                        channel.Writer.TryWrite(new SseItem<object>(result.ArtworkData!, "result"));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    channel.Writer.TryWrite(new SseItem<object>(new { message = "External service unavailable" }, "error"));
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
