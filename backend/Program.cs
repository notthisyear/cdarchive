using CdArchiveBackend.Endpoints;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

namespace CdArchiveBackend
{
    public class Program
    {
        private const string GetRecordEndpoint = "GetRecord";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var configuration = builder.Configuration;

            // Services
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(opt =>
                {
                    opt.TokenValidationParameters = JwtService.GetTokenValidationParameters(configuration);
                });
            builder.Services.AddAuthorization();

            builder.Services.AddDbContext<DatabaseContext>(
                opt => opt.UseNpgsql(
                    builder.Configuration.GetConnectionString("UserContext"))
                    .UseSnakeCaseNamingConvention());

            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<RecordsService>();
            builder.Services.AddScoped<ArtistService>();

            var app = builder.Build();

            app.UseAuthentication();
            app.UseAuthorization();

            // Endpoints
            List<EndpointGroupBase> endpointGroups = [];
            
            var authEndpointGroup = new EndpointGroupBase("/auth");
            authEndpointGroup.AddEndpoint(new LoginEndpoint());
            endpointGroups.Add(authEndpointGroup);

            var recordsEndpointGroup = new EndpointGroupBase("/records", useAuthorization: true);
            recordsEndpointGroup.AddEndpoint(new GetRecordsEndpoint());
            recordsEndpointGroup.AddEndpoint(new GetRecordEndpoint());
            recordsEndpointGroup.AddEndpoint(new CreateRecordEndpoint());
            endpointGroups.Add(recordsEndpointGroup);

            foreach (var endpointGroup in endpointGroups)
                endpointGroup.AddEndpoints(app);

            app.Run();
        }
    }
}
