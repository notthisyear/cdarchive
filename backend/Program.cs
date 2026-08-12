using CdArchiveBackend.Data;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Globalization;
using System.Security.Claims;

namespace CdArchiveBackend
{
    public class Program
    {
        private const string GetRecordEndpoint = "GetRecord";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;

            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opt =>
                {
                    opt.TokenValidationParameters = JwtService.GetTokenValidationParameters(configuration);
                });
            builder.Services.AddAuthorization();

            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<RecordsService>();

            var app = builder.Build();

            app.UseAuthentication();
            app.UseAuthorization();

            // POST /auth/login
            app.MapPost("/auth/login", async (LoginRequest loginRequest, UserService userService, JwtService jwtService) =>
            {
                var user = await userService.TryLoginAsync(loginRequest).ConfigureAwait(false);
                if (user == null)
                    return Results.Unauthorized();

                return Results.Ok(new
                {
                    token = jwtService.CreateToken(user.Value)
                });
            });

            // GET /records
            app.MapGet("/records", async (int offset, int limit, ClaimsPrincipal user, RecordsService recordsService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                return Results.Ok(new
                {
                    records = await recordsService.GetRecordSummaries(userId, offset, limit).ConfigureAwait(false)
                });
            }).RequireAuthorization();

            // GET /record/{id}
            app.MapGet("/record/{recordId}", async (int recordId, ClaimsPrincipal user, RecordsService recordsService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                Console.WriteLine($"received record request for user {userId} and record id: {recordId}");
                return Results.Ok(new
                {
                    record = await recordsService.GetRecordData(recordId).ConfigureAwait(false)
                });
            }).RequireAuthorization().WithName(GetRecordEndpoint);

            // POST /records
            app.MapPost("/records", async (RecordData recordData, ClaimsPrincipal user, RecordsService recordsService) =>
            {
                if (!int.TryParse(
                    user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var userId))
                {
                    return Results.BadRequest();
                }

                Console.WriteLine($"received record request for user {userId}, recordData:");
                Console.WriteLine(recordData.ToString());
                
                // Results.CreatedAtRoute(GetRecordEndpoint, new { id = record.id }, record);
                return Results.Ok();
            }).RequireAuthorization();


            app.Run();

        }
    }
}
