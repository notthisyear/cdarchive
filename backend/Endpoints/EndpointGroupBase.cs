using Microsoft.AspNetCore.Builder;
using System.Collections.Generic;

namespace CdArchiveBackend.Endpoints
{
    internal class EndpointGroupBase(string endpointGroupName, bool useAuthorization = false)
    {
        private readonly string _endpointGroupName = endpointGroupName;
        
        private readonly bool _useAuthorization = useAuthorization;

        private readonly List<IEndpoint> _endpoints = [];
        
        public void AddEndpoint(IEndpoint endpoint)
        {
            _endpoints.Add(endpoint);
        }

        public void AddEndpoints(WebApplication application)
        {
            var groupBuilder = application.MapGroup(_endpointGroupName);
            foreach (var endpoint in _endpoints)
                endpoint.AddEndpoint(groupBuilder);

            if (_useAuthorization)
                groupBuilder.RequireAuthorization();
        }
    }
}
