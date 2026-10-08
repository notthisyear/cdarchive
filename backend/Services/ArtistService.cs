using CdArchiveBackend.Data.Database;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CdArchiveBackend.Services
{
    internal sealed class ArtistService(DatabaseContext dbContext)
    {
        private readonly DatabaseContext _dbContext = dbContext;

        public async Task<List<Artist>> GetArtistsByName(long userId, string name)
        {
            return await _dbContext.Artists.Where(x => x.UserId == userId && EF.Functions.ILike(x.Name, "%" + EscapeLikePattern(name) + "%", @"\"))
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<Artist?> GetArtistById(long userId, long id)
            => await _dbContext.Artists.FirstOrDefaultAsync(x => x.UserId == userId && x.Id == id).ConfigureAwait(false);

        public async Task<long> AddArtist(long userId, string name, string coverImage = "", string spotifyUrl = "")
        {
            var newArtist = new Artist() { UserId = userId, Name = name, CoverImage = coverImage, SpotifyUrl = spotifyUrl };
            _dbContext.Artists.Add(newArtist);
            _ = await _dbContext.SaveChangesAsync().ConfigureAwait(false);
            return newArtist.Id;
        }

        private static string EscapeLikePattern(string s)
            => s.Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
    }
}
