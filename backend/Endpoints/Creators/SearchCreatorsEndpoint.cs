using CdArchiveBackend.Common;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints.Creators
{
    // The user's creators matching each of several names, as { result: { "<name>": [CreatorSummary...] } },
    // and the id of the creator each name clearly stands for (see FindMatchAsync), as { match: { "<name>": id or null } }.
    // QUERY rather than GET, as the names are sent in the body.
    internal sealed class SearchCreatorsEndpoint : IEndpoint
    {
        private const int MaxNamesPerQuery = 20;

        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapMethods("/", [HttpMethods.Query], async (List<CreatorQuery> queries, ClaimsPrincipal user, CreatorService creatorService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                // Keyed by name, so a name asked for twice is looked up as the first query has it
                var distinctQueries = queries
                    .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                    .DistinctBy(x => x.Name)
                    .ToList();

                if (distinctQueries.Count > MaxNamesPerQuery)
                    return Results.BadRequest();

                Dictionary<string, List<CreatorSummary>> result = [];
                Dictionary<string, long?> match = [];
                foreach (var query in distinctQueries)
                {
                    result[query.Name] = await creatorService.SearchCreatorsAsync(userId, query.Name, ct).ConfigureAwait(false);
                    match[query.Name] = await creatorService.FindMatchAsync(userId, query.Name, query.MusicBrainzId, ct).ConfigureAwait(false);
                }

                return Results.Ok(new { result, match });
            });
        }
    }
}
