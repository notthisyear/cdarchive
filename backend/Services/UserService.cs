using CdArchiveBackend.Common;
using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
namespace CdArchiveBackend.Services
{
    internal sealed class UserService(DatabaseContext dbContext)
    {
        private readonly DatabaseContext _dbContext = dbContext;

        public async Task<UserData?> TryLoginAsync(LoginRequest request)
        {
            try
            {
                var matchingUser = await _dbContext.Users.FirstAsync(x => x.Username.Equals(request.Username)).ConfigureAwait(false);
                var passwordHasher = new PasswordHasher();
                return passwordHasher.VerifyPassword(request.Password, matchingUser.PasswordHash) ? matchingUser : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
