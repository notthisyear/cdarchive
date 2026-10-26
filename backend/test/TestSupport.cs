using CdArchiveBackend;
using CdArchiveBackend.Common;
using CdArchiveBackend.Interfaces;
using CdArchiveBackend.Services;
using CdArchiveBackend.Services.External;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveTests
{
    using CoverArtArchiveOptions = CdArchiveBackend.Data.External.CoverArtArchive.Options;
    using ImageOptions = CdArchiveBackend.Data.ImageStore.Options;
    using MusicBrainzOptions = CdArchiveBackend.Data.External.MusicBrainz.Options;
    using UploadOptions = CdArchiveBackend.Data.Uploads.Options;
    using WikimediaOptions = CdArchiveBackend.Data.External.Wikimedia.Options;

    // The backend's services wired up as in the app, but against an in-memory database, a fake
    // file system and fake external services. Each NewRequest() is like one HTTP request: its own
    // DatabaseContext (on the same database) and its own scoped services.
    internal sealed class ServiceHarness : IDisposable
    {
        public const string SharedFolder = "/images";
        public const string UserFolder = "/uploads";

        private readonly string _databaseName = Guid.NewGuid().ToString();
        private readonly SaveChangesFailer _saveChangesFailer = new();
        private readonly List<DatabaseContext> _contexts = [];

        public FakeFileSystem FileSystem { get; } = new();

        public FakeHttpHandler Http { get; } = new();

        public MemoryCache Cache { get; } = new(new MemoryCacheOptions());

        public MediaUrlService MediaUrls { get; } = new(
            Options.Create(new UploadOptions { SigningKey = Convert.ToBase64String(new byte[32]) }),
            TimeProvider.System);

        // Makes every SaveChanges fail, as when the database is down or a constraint is broken
        public bool FailSaves
        {
            get => _saveChangesFailer.Fail;
            set => _saveChangesFailer.Fail = value;
        }

        // For setting up and checking the database outside of any request
        public DatabaseContext CreateDbContext()
        {
            var context = new DatabaseContext(new DbContextOptionsBuilder<DatabaseContext>()
                .UseInMemoryDatabase(_databaseName)
                .AddInterceptors(_saveChangesFailer)
                .Options);
            _contexts.Add(context);
            return context;
        }

        public TestRequest NewRequest()
        {
            var db = CreateDbContext();

            // One attempt and no waiting between requests, so failures show at once
            var musicBrainz = new MusicBrainzService(
                new HttpClient(Http) { BaseAddress = new Uri("https://musicbrainz.org/ws/2/") },
                new MusicBrainzRateLimiter(0),
                Options.Create(new MusicBrainzOptions { MaxNumberOfAttempts = 1 }),
                Cache);

            var coverArt = new CoverArtService(
                new HttpClient(Http) { BaseAddress = new Uri("https://coverartarchive.org/") },
                new CoverArtArchiveRateLimiter(0),
                Options.Create(new CoverArtArchiveOptions { MaxNumberOfAttempts = 1 }),
                Cache);

            var wikimedia = new WikimediaService(
                new HttpClient(Http),
                new WikimediaRateLimiter(0),
                Options.Create(new WikimediaOptions
                {
                    MaxNumberOfAttempts = 1,
                    WikidataApiUrl = "https://www.wikidata.org/w/",
                    CommonsApiUrl = "https://commons.wikimedia.org/w/"
                }),
                Cache);

            var mediaFiles = new MediaFileService(
                new HttpClient(Http),
                db,
                FileSystem,
                Options.Create(new ImageOptions { Shared = SharedFolder, User = UserFolder }),
                musicBrainz,
                coverArt,
                wikimedia);

            return new(db, mediaFiles, new CreatorService(db, mediaFiles, MediaUrls, musicBrainz), new ReleaseService(db, mediaFiles, MediaUrls));
        }

        public static FormFileCollection Files(params byte[][] contents)
        {
            var files = new FormFileCollection();
            foreach (var content in contents)
                files.Add(new FormFile(new MemoryStream(content), 0, content.Length, "files", "image"));
            return files;
        }

        public void Dispose()
        {
            foreach (var context in _contexts)
                context.Dispose();
            Cache.Dispose();
        }

        private sealed class SaveChangesFailer : SaveChangesInterceptor
        {
            public bool Fail { get; set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
                                                                                   InterceptionResult<int> result,
                                                                                   CancellationToken cancellationToken = default)
                => Fail ? throw new DbUpdateException("Simulated failure") : ValueTask.FromResult(result);
        }
    }

    internal sealed record TestRequest(DatabaseContext Db, MediaFileService MediaFiles, CreatorService Creators, ReleaseService Releases);

    // Images, and what MusicBrainz, the Cover Art Archive and Wikimedia answer about one release
    // and one artist
    internal static class ExternalServiceStubs
    {
        // Just enough of each format for MediaFileService to recognize it
        public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];
        public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2];
        public static readonly byte[] Webp = [.. "RIFF\0\0\0\0WEBPVP8 "u8];

        #region A release's cover
        public const string ReleaseMbid = "8a5d9f5a-3c0e-4b5e-9a39-1f2d3c4b5a69";
        public const string CoverArtArchiveReleaseUrl = $"https://coverartarchive.org/release/{ReleaseMbid}";
        public const string CoverUrl = $"https://coverartarchive.org/release/{ReleaseMbid}/1-500.jpg";
        public const string OriginalCoverUrl = $"https://coverartarchive.org/release/{ReleaseMbid}/1.jpg";
        public const string ReleasePageUrl = $"https://musicbrainz.org/release/{ReleaseMbid}";

        public static void CoverArtArchiveHasTheCover(FakeHttpHandler http)
        {
            http.Respond(CoverArtArchiveReleaseUrl, HttpStatusCode.OK, $$"""
                {
                    "images": [{
                        "front": true,
                        "approved": true,
                        "image": "{{OriginalCoverUrl}}",
                        "thumbnails": { "500": "{{CoverUrl}}" }
                    }],
                    "release": "{{ReleasePageUrl}}"
                }
                """);
        }
        #endregion

        #region An artist and their photo
        // MusicBrainz knows the artist's Wikidata entity, which names a file on Wikimedia Commons,
        // which has the URLs and the license
        public const string ArtistMbid = "e1f1e33e-2e4c-4d43-b91b-7064068d3283";
        public const string WikidataEntityId = "Q109359";
        public const string CommonsFileName = "Kiss Destroyer Tour 1976.jpg";
        public const string MusicBrainzArtistUrl = $"https://musicbrainz.org/ws/2/artist/{ArtistMbid}?fmt=json";
        public const string MusicBrainzArtistRelationsUrl = $"https://musicbrainz.org/ws/2/artist/{ArtistMbid}?inc=url-rels&fmt=json";
        public const string WikidataClaimsUrl = $"https://www.wikidata.org/w/api.php?action=wbgetclaims&entity={WikidataEntityId}&property=P18&format=json";
        public const string PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ab/Kiss_Destroyer_Tour_1976.jpg/500px-Kiss_Destroyer_Tour_1976.jpg";
        public const string OriginalPhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/a/ab/Kiss_Destroyer_Tour_1976.jpg";
        public const string PhotoPageUrl = "https://commons.wikimedia.org/wiki/File:Kiss_Destroyer_Tour_1976.jpg";
        public const string LicenseUrl = "https://creativecommons.org/licenses/by-sa/4.0";
        public static readonly string CommonsImageInfoUrl =
            "https://commons.wikimedia.org/w/api.php?action=query&format=json&formatversion=2" +
            "&prop=imageinfo&iiprop=url%7Cextmetadata&iiurlwidth=500" +
            "&titles=" + Uri.EscapeDataString("File:" + CommonsFileName);

        public static void MusicBrainzKnowsTheArtist(FakeHttpHandler http, string name, string sortName)
        {
            http.Respond(MusicBrainzArtistUrl, HttpStatusCode.OK, $$"""
                { "id": "{{ArtistMbid}}", "name": "{{name}}", "sort-name": "{{sortName}}", "type": "Group", "country": "US", "score": 0 }
                """);
        }

        public static void MusicBrainzKnowsTheWikidataEntity(FakeHttpHandler http)
        {
            http.Respond(MusicBrainzArtistRelationsUrl, HttpStatusCode.OK, $$"""
                {
                    "relations": [
                        { "type": "official homepage", "url": { "resource": "https://www.kissonline.com/" } },
                        { "type": "wikidata", "url": { "resource": "https://www.wikidata.org/wiki/{{WikidataEntityId}}" } }
                    ]
                }
                """);
        }

        public static void WikidataNamesTheCommonsFile(FakeHttpHandler http)
        {
            http.Respond(WikidataClaimsUrl, HttpStatusCode.OK, $$"""
                {
                    "claims": {
                        "P18": [{ "mainsnak": { "datavalue": { "value": "{{CommonsFileName}}" } }, "rank": "normal" }]
                    }
                }
                """);
        }

        public static void CommonsHasTheImageInfo(FakeHttpHandler http, string thumbUrl = PhotoUrl)
        {
            http.Respond(CommonsImageInfoUrl, HttpStatusCode.OK, $$"""
                {
                    "query": {
                        "pages": [{
                            "imageinfo": [{
                                "url": "{{OriginalPhotoUrl}}",
                                "thumburl": "{{thumbUrl}}",
                                "descriptionurl": "{{PhotoPageUrl}}",
                                "extmetadata": {
                                    "Artist": { "value": "<a href=\"https://commons.wikimedia.org/wiki/User:Jane\">Jane Doe</a>" },
                                    "LicenseShortName": { "value": "CC BY-SA 4.0" },
                                    "LicenseUrl": { "value": "{{LicenseUrl}}" },
                                    "AttributionRequired": { "value": "true" }
                                }
                            }]
                        }]
                    }
                }
                """);
        }

        // The whole chain answers, ending with the photo itself
        public static void WikimediaHasThePhoto(FakeHttpHandler http, byte[]? photo = null)
        {
            MusicBrainzKnowsTheWikidataEntity(http);
            WikidataNamesTheCommonsFile(http);
            CommonsHasTheImageInfo(http);
            http.Respond(PhotoUrl, HttpStatusCode.OK, photo ?? Jpeg);
        }
        #endregion
    }

    internal sealed class FakeFileSystem : IFileSystem
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public HashSet<string> Directories { get; } = [];

        public bool FailWrites { get; set; }

        public string? FailDeletesOf { get; set; }

        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
        {
            if (FailWrites)
                throw new IOException("Disk full");

            Files[path] = bytes;
            return Task.CompletedTask;
        }

        public void CreateDirectory(string path) => Directories.Add(path);

        public void DeleteFile(string path)
        {
            if (path == FailDeletesOf)
                throw new IOException("In use");

            Files.Remove(path);
        }
    }

    // Answers each URL with what a test told it to, and 404 for the rest. URLs are compared as
    // Uri.AbsoluteUri on both sides, so escaping ("%3A" or ":") doesn't matter.
    internal sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, byte[] Content)> _responses = [];

        public List<string> RequestedUrls { get; } = [];

        public void Respond(string url, HttpStatusCode status, byte[]? content = null)
            => _responses[new Uri(url).AbsoluteUri] = (status, content ?? []);

        public void Respond(string url, HttpStatusCode status, string json)
            => Respond(url, status, Encoding.UTF8.GetBytes(json));

        public bool WasRequested(string url)
            => RequestedUrls.Contains(new Uri(url).AbsoluteUri);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            RequestedUrls.Add(url);

            var (status, content) = _responses.TryGetValue(url, out var response) ? response : (HttpStatusCode.NotFound, []);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new ByteArrayContent(content) });
        }
    }
}
