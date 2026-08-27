using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading.Tasks;
using CdArchiveBackend.Interfaces;

namespace CdArchiveBackend.Common.HttpWrappers
{
    internal sealed class HttpClientWrapper(HttpClient client) : IHttpClient
    {
        private readonly HttpClient _client = client;

        public async Task<IHttpResponseMessage> GetAsync([StringSyntax("Uri")] string? requestUri)
            => new HttpResponseMessageWrapper(await _client.GetAsync(requestUri).ConfigureAwait(false));

        public void Dispose()
            => _client.Dispose();
    }
}