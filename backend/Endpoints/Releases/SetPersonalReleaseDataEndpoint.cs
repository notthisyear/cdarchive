using CdArchiveBackend.Common;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints
{
    // Sets the user's own data about one of their releases (PersonalReleaseData): when they got it,
    // their rating, whether they've listened to it and their notes. All of it at once. Answers with
    // the ReleaseDetails as they are now.
    internal sealed class SetPersonalReleaseDataEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPut("/{releaseId:long}/personal", async (long releaseId, PersonalReleaseData? data, ClaimsPrincipal user, ReleaseService releaseService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                if (data is null || !data.Validate())
                    return Results.BadRequest(new { message = "The rating must be 1 to 10, and the notes at most 4000 characters" });

                return UpdateReleaseEndpoint.ToHttpResult(await releaseService.SetPersonalDataAsync(userId, releaseId, data, ct).ConfigureAwait(false));
            });
        }
    }
}
