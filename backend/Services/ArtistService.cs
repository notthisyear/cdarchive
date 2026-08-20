using CdArchiveBackend.Data;
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

        private static string EscapeLikePattern(string s)
            => s.Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_");
    }
}
