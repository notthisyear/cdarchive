using CdArchiveBackend.Common;
using CdArchiveBackend.Endpoints;
using CdArchiveBackend.Endpoints.Artwork;
using CdArchiveBackend.Endpoints.Uploads;
using CdArchiveBackend.Services;
using CdArchiveBackend.Services.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CdArchiveBackend
{
    using CoverArtArchiveOptions = CdArchiveBackend.Data.External.CoverArtArchive.Options;
    using MusicBrainzOptions = CdArchiveBackend.Data.External.MusicBrainz.Options;
    using UploadOptions = CdArchiveBackend.Data.Uploads.Options;
    using WikimediaOptions = CdArchiveBackend.Data.External.Wikimedia.Options;

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

            builder.Services.ConfigureHttpJsonOptions(o =>
                o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<UserService>();
            builder.Services.AddScoped<ReleaseService>();
            //builder.Services.AddScoped<ArtistService>();
            //builder.Services.AddSingleton(
            //    new ImageDownloadService(
            //        new FileSystem(),
            //        () => new HttpClientWrapper(new HttpClient()),
            //        configuration[ImageStoreConfigurationKey] ?? string.Empty)
            //    );


            builder.Services.AddMemoryCache();
            builder.Services.AddSingleton<MusicBrainzRateLimiter>();
            builder.Services.AddSingleton<CoverArtArchiveRateLimiter>();
            builder.Services.AddSingleton<WikimediaRateLimiter>();

            builder.Services.AddOptions<MusicBrainzOptions>()
                .Bind(builder.Configuration.GetSection(MusicBrainzOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddOptions<CoverArtArchiveOptions>()
                .Bind(builder.Configuration.GetSection(CoverArtArchiveOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddOptions<WikimediaOptions>()
               .Bind(builder.Configuration.GetSection(WikimediaOptions.SectionName))
               .ValidateDataAnnotations()
               .ValidateOnStart();

            // The signing key lives in user secrets ("Uploads:SigningKey")
            builder.Services.AddOptions<UploadOptions>()
               .Bind(builder.Configuration.GetSection(UploadOptions.SectionName))
               .ValidateDataAnnotations()
               .Validate(o => o.HasValidSigningKey(), $"{UploadOptions.SectionName}:SigningKey must be base64 of at least 32 bytes")
               .ValidateOnStart();

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<MediaUrlService>();

            builder.Services.AddHttpClient<MusicBrainzService>((sp, c) =>
            {
                var options = sp.GetRequiredService<IOptions<MusicBrainzOptions>>().Value;
                c.BaseAddress = new Uri(options.ApiUrl);
                c.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                c.Timeout = TimeSpan.FromSeconds(10);
            });

            builder.Services.AddHttpClient<CoverArtService>((sp, c) =>
            {
                var options = sp.GetRequiredService<IOptions<CoverArtArchiveOptions>>().Value;
                c.BaseAddress = new Uri(options.ApiUrl);
                c.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                c.Timeout = TimeSpan.FromSeconds(10);
            });

            builder.Services.AddHttpClient<WikimediaService>((sp, c) =>
            {
                var options = sp.GetRequiredService<IOptions<WikimediaOptions>>().Value;
                c.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                c.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                c.Timeout = TimeSpan.FromSeconds(10);
            });

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

            var releasesEndpointGroup = new EndpointGroupBase("/releases", useAuthorization: true);
            releasesEndpointGroup.AddEndpoint(new GetReleasesEndpoint());
            //releasesEndpointGroup.AddEndpoint(new GetRecordEndpoint());
            //releasesEndpointGroup.AddEndpoint(new AddRecordEndpoint());
            releasesEndpointGroup.AddEndpoint(new GetTotalNumberOfReleases());
            endpointGroups.Add(releasesEndpointGroup);

            var musicbrainzEndpointGroup = new EndpointGroupBase("/musicbrainz", useAuthorization: true);
            musicbrainzEndpointGroup.AddEndpoint(new GetRecordsFromMusicBrainzEndpoint());
            musicbrainzEndpointGroup.AddEndpoint(new GetTracklistFromMusicBrainzEndpoint());
            musicbrainzEndpointGroup.AddEndpoint(new GetArtistsFromMusicBrainzEndpoint());
            musicbrainzEndpointGroup.AddEndpoint(new GetArtistFromMusicBrainzEndpoint());
            endpointGroups.Add(musicbrainzEndpointGroup);

            var artworkEndpointGroup = new EndpointGroupBase("/artwork", useAuthorization: true);
            artworkEndpointGroup.AddEndpoint(new GetAlbumArtworkEndpoint());
            artworkEndpointGroup.AddEndpoint(new GetArtistArtworkEndpoint());
            endpointGroups.Add(artworkEndpointGroup);

            var artistsEndpointGroup = new EndpointGroupBase("/artists", useAuthorization: true);
            //artistsEndpointGroup.AddEndpoint(new AddArtistEndpoint());
            //artistsEndpointGroup.AddEndpoint(new GetArtistEndpoint());
            //artistsEndpointGroup.AddEndpoint(new GetArtistsEndpoint());
            endpointGroups.Add(artistsEndpointGroup);

            // No authorization: browsers don't send the token for an <img src>, so the links are signed instead
            var uploadsEndpointGroup = new EndpointGroupBase("/uploads");
            uploadsEndpointGroup.AddEndpoint(new GetUploadEndpoint());
            endpointGroups.Add(uploadsEndpointGroup);

            foreach (var endpointGroup in endpointGroups)
                endpointGroup.AddEndpoints(app);

            app.Run();
        }
    }
}
