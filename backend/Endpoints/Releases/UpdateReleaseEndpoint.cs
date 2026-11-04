using CdArchiveBackend.Common;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints
{
    using UpdateReleaseError = ReleaseService.UpdateReleaseError;

    // Changes one of the user's releases (see UpdateReleaseRequest). Answers with the ReleaseDetails
    // as they are now.
    internal sealed class UpdateReleaseEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPut("/{releaseId:long}", async (long releaseId, UpdateReleaseRequest? request, ClaimsPrincipal user, ReleaseService releaseService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                if (request is null || !request.Validate())
                    return Results.BadRequest();

                try
                {
                    return ToHttpResult(await releaseService.UpdateReleaseAsync(userId, releaseId, request, ct).ConfigureAwait(false));
                }
                catch (DbUpdateException)
                {
                    // Most likely the same cover was stored by another request just now, so ours
                    // broke the unique index on its SHA-1. Trying again reuses that file.
                    return Results.Conflict(new { message = "The cover was saved elsewhere at the same time, try again" });
                }
            });
        }

        // For every endpoint changing a release
        internal static IResult ToHttpResult(ReleaseService.UpdateReleaseResult result)
            => result.Error switch
            {
                UpdateReleaseError.None => Results.Ok(result.Release),
                // Not found also for another user's release, as for getting one
                UpdateReleaseError.NotFound => Results.NotFound(),
                UpdateReleaseError.AlreadyInCollection => Results.Conflict(new
                {
                    message = "Another release in your collection is already this one",
                    releaseId = result.ExistingReleaseId
                }),
                UpdateReleaseError.UnknownCreator => Results.BadRequest(new { message = "One of the artists isn't in your collection" }),
                UpdateReleaseError.ImageNotFound => Results.BadRequest(new { message = "The cover could not be found anymore" }),
                UpdateReleaseError.ImageServiceUnavailable => Results.Json(new { message = "Could not get the cover right now, try again later" }, statusCode: StatusCodes.Status503ServiceUnavailable),
                _ => Results.BadRequest(new { message = "The cover must be a JPEG, PNG or WebP file of at most 8 MiB" })
            };
    }
}
