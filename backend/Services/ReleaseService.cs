using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CdArchiveBackend.Services
{
    using DtoArtist = CdArchiveBackend.Data.DTO.Artist;

    internal sealed class ReleaseService(DatabaseContext dbContext)
    {
        private readonly DatabaseContext _dbContext = dbContext;

        public async Task<int> GetTotalNumberOfReleasesForUser(int userId)
            => await _dbContext.UserReleases.CountAsync(x => x.UserId == userId).ConfigureAwait(false);

        public async Task<List<ReleaseData>> GetReleasesForUser(int userId, int offset, int numberOfRecordsToGet)
        {
            var releases = await _dbContext.UserReleases
                    .Where(x => x.UserId == userId)
                    .Include(x => x.Release)
                        .ThenInclude(x => x.ReleaseArtists)
                            .ThenInclude(x => x.Artist)
                    .Select(x => x.Release)
                    .OrderBy(x => x.ReleaseArtists.First().Artist.Name)
                    .Skip(offset)
                    .Take(numberOfRecordsToGet)
                    .AsSplitQuery()
                    .ToListAsync()
                    .ConfigureAwait(false);

            return [.. releases.Select(x =>
                new ReleaseData(
                    x.Id,
                    new Summary(
                        x.Name,
                        [..x.ReleaseArtists.Select(x => new DtoArtist()
                        {
                            Id = x.Artist.Id,
                            Name = x.Artist.Name,
                            ImageUrl = x.Artist.CoverImage
                        })
                        ],
                        x.ReleaseDate.Year,
                        x.CoverImage,
                        x.SpotifyLink),
                    (int)x.LengthSeconds, [])
                ).OrderBy(x => x.Summary.Artists.First().Name)];
        }

        public async Task<long> CreateRelease(Release release)
        {
            _dbContext.Releases.Add(release);
            await _dbContext.SaveChangesAsync().ConfigureAwait(false);
            return release.Id;
        }

        public Task AddTracksForRelease(long releaseId, List<Track> tracks, bool deferUpdate)
        {
            foreach (var track in tracks)
            {
                _dbContext.ReleaseTracks.Add(new()
                {
                    ReleaseId = releaseId,
                    DiscNumber = track.DiscNumber,
                    TrackNumber = track.TrackNumber,
                    Title = track.Title,
                    DurationSeconds = track.DurationSeconds
                });
            }

            return deferUpdate ? Task.CompletedTask : _dbContext.SaveChangesAsync();
        }

        public Task AddArtistsForRelease(long releaseId, int userId, List<long> artistIds, bool deferUpdate)
        {
            foreach (var artistId in artistIds)
                _dbContext.ReleaseArtists.Add(new() { ReleaseId = releaseId, ArtistId = artistId, UserId = userId });

            return deferUpdate ? Task.CompletedTask : _dbContext.SaveChangesAsync();
        }

        public Task AddReleaseForUser(long releaseId, int userId, bool deferUpdate)
        {
            _dbContext.UserReleases.Add(new() { ReleaseId = releaseId, UserId = userId });
            return deferUpdate ? Task.CompletedTask : _dbContext.SaveChangesAsync();
        }

        public async Task<ReleaseData> GetReleaseData(long releaseId, int? userId)
        {
            var releases = await _dbContext.UserReleases
                    .Where(x => (x.ReleaseId == releaseId) && (!userId.HasValue || (x.UserId == userId)))
                    .Include(x => x.Release)
                        .ThenInclude(x => x.ReleaseArtists)
                            .ThenInclude(x => x.Artist)
                    .Include(x => x.Release)
                        .ThenInclude(x => x.Tracks)
                    .Select(x => x.Release)
                    .Take(1)
                    .AsSplitQuery()
                    .ToListAsync()
                    .ConfigureAwait(false);

            if (releases == null || releases.Count == 0 || releases.First() == null)
                throw new InvalidOperationException();

            var release = releases.First();

            return new ReleaseData(
                release.Id,
                new Summary()
                {
                    Name = release.Name,
                    Year = release.ReleaseDate.Year,
                    ImageUrl = release.CoverImage,
                    SpotifyLink = release.SpotifyLink,
                    Artists = [..release.ReleaseArtists.Select(x => new DtoArtist()
                    {
                        Id = x.Artist.Id,
                        Name = x.Artist.Name,
                        ImageUrl = x.Artist.CoverImage
                    })]
                }, (int)release.LengthSeconds,
                [..release.Tracks.Select(x => new Track()
                {
                    DiscNumber = x.DiscNumber,
                    TrackNumber = x.TrackNumber,
                    Title = x.Title,
                    DurationSeconds = x.DurationSeconds
                })]);
        }

        public Task<List<long>> GetReleasesMatchingName(string recordName)
            => _dbContext.Releases
            .Where(x => x.Name == recordName)
            .Select(x => x.Id).ToListAsync();
    
        public Task<bool> UserHasRelease(long releaseId, int userId)
            => _dbContext.UserReleases.AnyAsync(x => x.ReleaseId == releaseId && x.UserId == userId);
    }
}
