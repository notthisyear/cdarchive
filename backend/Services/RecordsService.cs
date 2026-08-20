using CdArchiveBackend.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CdArchiveBackend.Services
{
    internal sealed class RecordsService(DatabaseContext dbContext)
    {
        private readonly DatabaseContext _dbContext = dbContext;

        public async Task<List<UserReleases>> GetRecordsForUser(int userId, int offset, int numberOfRecordsToGet)
        {
            return await _dbContext.UserReleases.Where(x => x.UserId == userId)
                .OrderBy(x => x.Release.Name)
                .Skip(offset)
                .Take(numberOfRecordsToGet)
                .ToListAsync()
                .ConfigureAwait(false);
        }

        public Task<long> CreateRecordForUser(int userId, Release release)
        {
            return Task.FromResult((long)0);
        }

        //public Task<Release> GetRecordData(int recordId)
        //{
        //    if (recordId == 0)
        //        return Task.FromResult(s_animals);
        //    else
        //        return Task.FromResult(s_foxtrot);
        //}
    }
}
