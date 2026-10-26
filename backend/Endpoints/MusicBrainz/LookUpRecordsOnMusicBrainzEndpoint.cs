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
using static CdArchiveBackend.Common.Utilities;

namespace CdArchiveBackend.Endpoints
{
    // The releases a MusicBrainz id the user gave us stands for: one release, or all releases of a
    // release group. The same candidates as /records/stream, streamed the same way. An empty list
    // if MusicBrainz has neither.
    internal sealed class LookUpRecordsOnMusicBrainzEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet(
                "/records/lookup/stream",
                (string? mbid, ClaimsPrincipal user, MusicBrainzService musicBrainzService, CancellationToken ct) =>
                {
                    if (!user.TryExtractUserId(out _))
                        return Results.BadRequest();

                    // A GUID, which also keeps it safe to put in a URL
                    if (!Guid.TryParse(mbid, out var id))
                        return Results.BadRequest();

                    // MusicBrainz writes them lower-case, which also makes the cache keys match
                    return TypedResults.ServerSentEvents(StreamLookup(id.ToString("D"), musicBrainzService, ct));
                });
        }

        private static async IAsyncEnumerable<SseItem<object>> StreamLookup(string mbid,
                                                                            MusicBrainzService musicBrainzService,
                                                                            [EnumeratorCancellation] CancellationToken ct)
        {
            var channel = Channel.CreateUnbounded<SseItem<object>>();
            _ = Task.Run(async () =>
            {
                try
                {
                    var result = await musicBrainzService.LookUpReleaseCandidatesAsync(
                        mbid,
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
