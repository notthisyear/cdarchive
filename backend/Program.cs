using CdArchiveBackend.Common;
using CdArchiveBackend.Common.HttpWrappers;
using CdArchiveBackend.Endpoints;
using CdArchiveBackend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;
using System.Net.Http;

namespace CdArchiveBackend
{
    public class Program
    {
        private const string ImageStoreConfigurationKey = "ImageStore";
        private const int MaxImageSizeBytes = 1 << 23; // 8 MiB

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
            builder.Services.AddScoped<ReleaseService>();
            builder.Services.AddScoped<ArtistService>();
            builder.Services.AddSingleton(
                new ImageDownloadService(
                    new FileSystem(),
                    () => new HttpClientWrapper(new HttpClient()),
                    configuration[ImageStoreConfigurationKey] ?? string.Empty)
                );
            // builder.Services.AddSingleton(new RequestStore<ArtistRequestEntry>(60_000));
            builder.WebHost.ConfigureKestrel(options =>
            {
                // Note: Base64 encoded images are ~33% larger than the actual image
                options.Limits.MaxRequestBodySize = (long)(1.33 * MaxImageSizeBytes);
            });
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
            recordsEndpointGroup.AddEndpoint(new AddRecordEndpoint());
            recordsEndpointGroup.AddEndpoint(new GetTotalNumberOfRecords());
            endpointGroups.Add(recordsEndpointGroup);

            var artistsEndpointGroup = new EndpointGroupBase("/artists", useAuthorization: true);
            artistsEndpointGroup.AddEndpoint(new AddArtistEndpoint());
            artistsEndpointGroup.AddEndpoint(new GetArtistEndpoint());
            artistsEndpointGroup.AddEndpoint(new GetArtistsEndpoint());
            endpointGroups.Add(artistsEndpointGroup);

            foreach (var endpointGroup in endpointGroups)
                endpointGroup.AddEndpoints(app);

            app.Run();
        }
    }
}
