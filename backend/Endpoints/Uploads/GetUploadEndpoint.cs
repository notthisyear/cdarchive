using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;

namespace CdArchiveBackend.Endpoints.Uploads
{
    // Serves a file a user uploaded, given a signed link from MediaUrlService. There's no login
    // check: browsers don't send the token for an <img src>, so the signature takes its place.
    // The file itself is sent by nginx, from an internal location over the uploads folder:
    //
    //   location /protected-uploads/ {
    //       internal;
    //       alias /var/www/cdarchive/uploads/;
    //   }
    internal sealed class GetUploadEndpoint : IEndpoint
    {
        private const string ProtectedUploadsLocation = "/protected-uploads/";

        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapGet("/{id:long}", async (long id, long exp, string sig, MediaUrlService mediaUrlService, DatabaseContext db, HttpResponse response) =>
            {
                // Not found rather than forbidden, so nobody can tell which ids exist
                if (!mediaUrlService.IsValidUploadLink(id, exp, sig))
                    return Results.NotFound();

                var file = await db.MediaFiles.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == id && x.OwnerUserId != null)
                    .ConfigureAwait(false);

                if (file is null)
                    return Results.NotFound();

                // The path comes only from the database row, never from the request
                response.Headers["X-Accel-Redirect"] = string.Create(CultureInfo.InvariantCulture,
                    $"{ProtectedUploadsLocation}{file.OwnerUserId}/{file.FileName}");

                // Cache no longer than the link is valid, and only in the browser
                var maxAge = Math.Max(0, mediaUrlService.GetSecondsUntil(exp));
                response.Headers.CacheControl = string.Create(CultureInfo.InvariantCulture, $"private, max-age={maxAge}");

                return Results.Empty;
            });
        }
    }
}
