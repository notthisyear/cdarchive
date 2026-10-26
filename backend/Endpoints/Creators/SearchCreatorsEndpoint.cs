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
    // The user's creators matching each of several names, as { result: { "<name>": [CreatorSummary...] } }.
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

                var names = queries
                    .Select(x => x.Name)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct()
                    .ToList();

                if (names.Count > MaxNamesPerQuery)
                    return Results.BadRequest();

                Dictionary<string, List<CreatorSummary>> result = [];
                foreach (var name in names)
                    result[name] = await creatorService.SearchCreatorsAsync(userId, name, ct).ConfigureAwait(false);

                return Results.Ok(new { result });
            });
        }
    }
}
