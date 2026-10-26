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

namespace CdArchiveBackend.Endpoints.Creators
{
    using AddCreatorError = CreatorService.AddCreatorError;

    // Takes a multipart form: the NewCreatorRequest as JSON in a "request" part, and an uploaded
    // image (if any) as a file part, which the request's image refers to by index.
    // The form is read by hand rather than bound with [FromForm]: that would require antiforgery
    // tokens, which guard against cookie auth being abused, and we use bearer tokens.
    internal sealed class AddCreatorEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPost("/", async (HttpRequest request,
                                             ClaimsPrincipal user,
                                             CreatorService creatorService,
                                             IOptions<JsonOptions> jsonOptions,
                                             CancellationToken ct) =>
            {
                if (!user.TryExtractUserId(out var userId))
                    return Results.BadRequest();

                if (!request.HasFormContentType)
                    return Results.BadRequest();

                var form = await request.ReadFormAsync(ct).ConfigureAwait(false);

                NewCreatorRequest? creator;
                try
                {
                    creator = JsonSerializer.Deserialize<NewCreatorRequest>(form["request"].ToString(), jsonOptions.Value.SerializerOptions);
                }
                catch (JsonException)
                {
                    return Results.BadRequest();
                }

                if (creator is null || !creator.Validate(form.Files.Count))
                    return Results.BadRequest();

                CreatorService.AddCreatorResult result;
                try
                {
                    result = await creatorService.AddCreatorAsync(userId, creator, form.Files, ct).ConfigureAwait(false);
                }
                catch (DbUpdateException)
                {
                    // Most likely the same image was stored by another request just now (e.g. two
                    // users adding the same artist photo), so ours broke the unique index on its
                    // SHA-1. Trying again reuses that file.
                    return Results.Conflict(new { message = "The image was saved elsewhere at the same time, try again" });
                }

                return result.Error switch
                {
                    AddCreatorError.None => Results.CreatedAtRoute(GetCreatorEndpoint.EndpointName, new { creatorId = result.CreatorId }),
                    AddCreatorError.AlreadyInCollection => Results.Conflict(new { message = "The artist is already in your collection", creatorId = result.CreatorId }),
                    AddCreatorError.ImageNotFound => Results.BadRequest(new { message = "The image could not be found anymore" }),
                    AddCreatorError.ImageServiceUnavailable => Results.Json(new { message = "Could not get the image right now, try again later" }, statusCode: StatusCodes.Status503ServiceUnavailable),
                    _ => Results.BadRequest(new { message = "The image must be a JPEG, PNG or WebP file of at most 8 MiB" })
                };
            });
        }
    }
}
