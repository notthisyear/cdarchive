using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CdArchiveBackend.Endpoints
{
    internal sealed class LoginEndpoint : IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder)
        {
            groupBuilder.MapPost("/login", async (LoginRequest loginRequest, UserService userService, JwtService jwtService) =>
            {
                var user = await userService.TryLoginAsync(loginRequest).ConfigureAwait(false);
                if (user == null)
                    return Results.Unauthorized();

                return Results.Ok(new
                {
                    token = jwtService.CreateToken(user)
                });
            });
        }
    }
}
