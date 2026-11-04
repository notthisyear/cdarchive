using CdArchiveBackend.Common;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading;

namespace CdArchiveBackend.Endpoints
{
    // Adds an image the user uploads to one of their releases, as the one to show (see
    // ReleaseService.AddUploadedImageAsync). Takes a multipart form with the image as its only file.
    // The form is read by hand rather than bound with [FromForm], which would require antiforgery
    // tokens; we use bearer tokens. Answers with the ReleaseDetails as they are now.
    internal sealed class AddReleaseImageEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPost("/{releaseId:long}/images", async (long releaseId, HttpRequest request, ClaimsPrincipal user, ReleaseService releaseService, CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                if (!request.HasFormContentType)
                    return Results.BadRequest();

                var form = await request.ReadFormAsync(ct).ConfigureAwait(false);
                if (form.Files.Count != 1)
                    return Results.BadRequest(new { message = "Send one image" });

                try
                {
                    return UpdateReleaseEndpoint.ToHttpResult(await releaseService.AddUploadedImageAsync(userId, releaseId, form.Files, ct).ConfigureAwait(false));
                }
                catch (DbUpdateException)
                {
                    // Most likely the same file was stored by another request just now, so ours broke
                    // the unique index on its SHA-1. Trying again reuses that file.
                    return Results.Conflict(new { message = "The image was saved elsewhere at the same time, try again" });
                }
            });
        }
    }
}
