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
    internal sealed class GetAlbumArtworkEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet(
                "/album/stream", (string? mbid, ClaimsPrincipal user, CoverArtService coverArtService, CancellationToken ct) =>
                {
                    if (!user.TryExtractUserId(out _))
                        return Results.BadRequest();

                    if (string.IsNullOrEmpty(mbid))
                        return Results.BadRequest();

                    return TypedResults.ServerSentEvents(StreamLookup(mbid, coverArtService, ct));
                });
        }

        private static async IAsyncEnumerable<SseItem<object>> StreamLookup(string mbid,
                                                                            CoverArtService coverArtService,
                                                                            [EnumeratorCancellation] CancellationToken ct)
        {
            var channel = Channel.CreateUnbounded<SseItem<object>>();
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await coverArtService.GetFrontCoverAsync(
                        mbid,
                        channelProgress: new ChannelProgress(channel.Writer), ct: ct).ConfigureAwait(false);
                    
                    if (result.GetResult == Utilities.HttpGetResult.RateLimitedOrServiceUnavailable)
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "CovertArtArchive unavailable" }, "error"));
                    else if (result.ArtworkData != null)
                        channel.Writer.TryWrite(new SseItem<object>(result.ArtworkData, "result"));
                    else
                        channel.Writer.TryWrite(new SseItem<object>(new { message = "Fetching covert art failed" }, "error"));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    channel.Writer.TryWrite(new SseItem<object>(new { message = "CovertArtArchive is unavailable" }, "error"));
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
