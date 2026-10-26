using CdArchiveBackend.Common;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;

namespace CdArchiveBackend.Endpoints
{
    using AddReleaseError = ReleaseService.AddReleaseError;

    // Takes a multipart form: the NewReleaseRequest as JSON in a "request" part, and uploaded
    // images (if any) as file parts, which the request's images refer to by index.
    // The form is read by hand rather than bound with [FromForm]: that would require antiforgery
    // tokens, which guard against cookie auth being abused, and we use bearer tokens.
    internal sealed class AddReleaseEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPost("/", async (HttpRequest request,
                                             ClaimsPrincipal user,
                                             ReleaseService releaseService,
                                             IOptions<JsonOptions> jsonOptions,
                                             CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                if (!request.HasFormContentType)
                    return Results.BadRequest();

                var form = await request.ReadFormAsync(ct).ConfigureAwait(false);

                NewReleaseRequest? release;
                try
                {
                    release = JsonSerializer.Deserialize<NewReleaseRequest>(form["request"].ToString(), jsonOptions.Value.SerializerOptions);
                }
                catch (JsonException)
                {
                    return Results.BadRequest();
                }

                if (release is null || !release.Validate(form.Files.Count))
                    return Results.BadRequest();

                ReleaseService.AddReleaseResult result;
                try
                {
                    result = await releaseService.AddReleaseAsync(userId, release, form.Files, ct).ConfigureAwait(false);
                }
                catch (DbUpdateException)
                {
                    // Most likely the same image was stored by another request just now, so ours
                    // broke the unique index on its SHA-1. Trying again reuses that file.
                    return Results.Conflict(new { message = "An image was saved elsewhere at the same time, try again" });
                }

                return result.Error switch
                {
                    AddReleaseError.None => Results.CreatedAtRoute(
                        GetReleaseEndpoint.EndpointName,
                        new { releaseId = result.ReleaseId },
                        new { id = result.ReleaseId }),
                    AddReleaseError.AlreadyInCollection => Results.Conflict(new { message = "This release is already in your collection", releaseId = result.ReleaseId }),
                    AddReleaseError.UnknownCreator => Results.BadRequest(new { message = "One of the artists isn't in your collection" }),
                    AddReleaseError.ImageNotFound => Results.BadRequest(new { message = "The cover could not be found anymore" }),
                    AddReleaseError.ImageServiceUnavailable => Results.Json(new { message = "Could not get the cover right now, try again later" }, statusCode: StatusCodes.Status503ServiceUnavailable),
                    _ => Results.BadRequest(new { message = "Images must be JPEG, PNG or WebP files of at most 8 MiB" })
                };
            });
        }
    }
}
