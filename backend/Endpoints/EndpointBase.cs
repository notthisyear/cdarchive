using Microsoft.AspNetCore.Routing;

namespace CdArchiveBackend.Endpoints
{
    internal interface IEndpoint
    {
        public void AddEndpoint(RouteGroupBuilder groupBuilder);
    }
}
