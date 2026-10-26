using CdArchiveBackend.Common;
using CdArchiveBackend.Endpoints;
using CdArchiveBackend.Endpoints.Artwork;
using CdArchiveBackend.Endpoints.Creators;
using CdArchiveBackend.Endpoints.Uploads;
using CdArchiveBackend.Interfaces;
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
    using UploadOptions = CdArchiveBackend.Data.Uploads.Options;
    using ImageOptions = CdArchiveBackend.Data.ImageStore.Options;
    using CoverArtArchiveOptions = CdArchiveBackend.Data.External.CoverArtArchive.Options;
    using MusicBrainzOptions = CdArchiveBackend.Data.External.MusicBrainz.Options;
    using WikimediaOptions = CdArchiveBackend.Data.External.Wikimedia.Options;

    public class Program
    {

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
            builder.Services.AddScoped<CreatorService>();

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

            builder.Services.AddOptions<ImageOptions>()
              .Bind(builder.Configuration.GetSection(ImageOptions.SectionName))
              .ValidateDataAnnotations()
              .ValidateOnStart();

            builder.Services.AddOptions<UploadOptions>()
               .Bind(builder.Configuration.GetSection(UploadOptions.SectionName))
               .ValidateDataAnnotations()
               .Validate(o => o.HasValidSigningKey(), $"{UploadOptions.SectionName}:SigningKey must be base64 of at least 32 bytes")
               .ValidateOnStart();

            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton<MediaUrlService>();
            builder.Services.AddSingleton<IFileSystem, FileSystem>();

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

            // Downloads the looked-up images (from Wikimedia and the Cover Art Archive, which both
            // want to know who's asking)
            builder.Services.AddHttpClient<MediaFileService>((sp, c) =>
            {
                var options = sp.GetRequiredService<IOptions<WikimediaOptions>>().Value;
                c.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                c.Timeout = TimeSpan.FromSeconds(20);
            });

            builder.WebHost.ConfigureKestrel(options =>
            {
                // Note: Images are sent as files in multipart forms, with a little room for the
                //       rest of the form
                options.Limits.MaxRequestBodySize = MediaFileService.MaxImageSizeBytes + (1 << 20);
            });
            var app = builder.Build();

            app.UseAuthentication();
            app.UseAuthorization();

            // Endpoints
            List<EndpointGroupBase> endpointGroups = [];

            var authEndpointGroup = new EndpointGroupBase("/auth");
            authEndpointGroup.AddEndpoint(new LoginEndpoint());
            endpointGroups.Add(authEndpointGroup);

            // Note: When we add a update release endpoint, we need to validate the preferred
            //       image selection so that we don't accidentally set it to something it shouldn't be.

            var releasesEndpointGroup = new EndpointGroupBase("/releases", useAuthorization: true);
            releasesEndpointGroup.AddEndpoint(new GetReleasesEndpoint());
            releasesEndpointGroup.AddEndpoint(new GetTotalNumberOfReleases());
            releasesEndpointGroup.AddEndpoint(new AddReleaseEndpoint());
            releasesEndpointGroup.AddEndpoint(new GetReleaseEndpoint());
            endpointGroups.Add(releasesEndpointGroup);

            var musicbrainzEndpointGroup = new EndpointGroupBase("/musicbrainz", useAuthorization: true);
            musicbrainzEndpointGroup.AddEndpoint(new GetRecordsFromMusicBrainzEndpoint());
            musicbrainzEndpointGroup.AddEndpoint(new LookUpRecordsOnMusicBrainzEndpoint());
            musicbrainzEndpointGroup.AddEndpoint(new GetTracklistFromMusicBrainzEndpoint());
            musicbrainzEndpointGroup.AddEndpoint(new GetArtistsFromMusicBrainzEndpoint());
            musicbrainzEndpointGroup.AddEndpoint(new GetArtistFromMusicBrainzEndpoint());
            endpointGroups.Add(musicbrainzEndpointGroup);

            var artworkEndpointGroup = new EndpointGroupBase("/artwork", useAuthorization: true);
            artworkEndpointGroup.AddEndpoint(new GetAlbumArtworkEndpoint());
            artworkEndpointGroup.AddEndpoint(new GetArtistArtworkEndpoint());
            endpointGroups.Add(artworkEndpointGroup);

            // Note: When we add a update creator endpoint, we need to validate the preferred
            //       image selection so that we don't accidentally set it to something it shouldn't be.
            var creatorsEndpointGroup = new EndpointGroupBase("/creators", useAuthorization: true);
            creatorsEndpointGroup.AddEndpoint(new AddCreatorEndpoint());
            creatorsEndpointGroup.AddEndpoint(new GetCreatorEndpoint());
            creatorsEndpointGroup.AddEndpoint(new SearchCreatorsEndpoint());
            endpointGroups.Add(creatorsEndpointGroup);

            // Note: Here, there no authorization. Browsers don't send the token for an <img src>, so the links are signed instead
            var uploadsEndpointGroup = new EndpointGroupBase("/uploads");
            uploadsEndpointGroup.AddEndpoint(new GetUploadEndpoint());
            endpointGroups.Add(uploadsEndpointGroup);

            foreach (var endpointGroup in endpointGroups)
                endpointGroup.AddEndpoints(app);

            app.Run();
        }
    }
}
