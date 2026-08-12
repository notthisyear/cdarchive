using CdArchiveBackend.Data;
using System.Threading.Tasks;

namespace CdArchiveBackend.Services
{
    public class UserService
    {
        public Task<UserData?> TryLoginAsync(LoginRequest request)
        {
            // TODO: Validate the request

            return Task.FromResult<UserData?>(new UserData()
            {
                Id = 0,
                Username = request.Username,
                Email = "lindquist.calle@gmail.com"
            });
        }
    }
}
