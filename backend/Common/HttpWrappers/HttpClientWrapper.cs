using CdArchiveBackend.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Common.HttpWrappers
{
    internal sealed class HttpClientWrapper(HttpClient client) : IHttpClient
    {
        private readonly HttpClient _client = client;

        public async Task<IHttpResponseMessage> GetAsync([StringSyntax("Uri")] string? requestUri, CancellationToken ct = default)
            => new HttpResponseMessageWrapper(await _client.GetAsync(requestUri, ct).ConfigureAwait(false));

        public void Dispose()
            => _client.Dispose();
    }
}