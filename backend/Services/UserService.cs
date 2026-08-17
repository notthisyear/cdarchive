using CdArchiveBackend.Data;
using CdArchiveBackend.Common;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
namespace CdArchiveBackend.Services
{
    internal sealed class UserService(UserContext userDbContext)
    {
        private readonly UserContext _userDbContext = userDbContext;

        public async Task<UserData?> TryLoginAsync(LoginRequest request)
        {
            try
            {
                var matchingUser = await _userDbContext.Users.FirstAsync(x => x.Username.Equals(request.Username)).ConfigureAwait(false);
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
