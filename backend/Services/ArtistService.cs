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

        public async Task<List<Artist>> GetArtistsByName(string name)
        {
            return await _dbContext.Artists.Where(x => EF.Functions.ILike(x.Name, EscapeLikePattern(name) + "%", @"\"))
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public async Task<long> AddArtist(string name, string coverImage = "")
        {
            var newArtist = new Artist() { Name = name, CoverImage = coverImage };
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
