using CdArchiveBackend.Data.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CdArchiveBackend.Services
{
    internal sealed class CreatorService(DatabaseContext dbContext)
    {
        private readonly DatabaseContext _dbContext = dbContext;

        public async Task<List<Creator>> GetCreatorByName(long userId, string name)
        {
            return await _dbContext.UserCreatorProfiles.Where(
                x =>
                x.UserId == userId && EF.Functions.ILike(x.Creator.SortName, "%" + EscapeLikePattern(name) + "%", @"\"))
                .Select(x => x.Creator)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        //public async Task<Creator?> GetArtistById(long userId, long id)
        //{
        //    var c = await _dbContext.UserCreatorProfiles.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id).ConfigureAwait(false);
        //    return c?.Creator;
        //}

        //public async Task<long> AddCreator(long userId, string name, string defaultImage = "", string spotifyUrl = "")
        //{
        //    var newArtist = new Artist() { UserId = userId, Name = name, CoverImage = coverImage, SpotifyUrl = spotifyUrl };
        //    _dbContext.Artists.Add(newArtist);
        //    _ = await _dbContext.SaveChangesAsync().ConfigureAwait(false);
        //    return newArtist.Id;
        //}

        private static string EscapeLikePattern(string s)
            => s.Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
    }
}
