using CdArchiveBackend.Interfaces;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Common.HttpWrappers
{
    internal sealed class HttpContentWrapper(HttpContent content) : IHttpContent
    {
        private readonly HttpContent _content = content;

        public HttpContentHeaders Headers
            => _content.Headers;

        public Task<byte[]> ReadAsByteArrayAsync()
            => _content.ReadAsByteArrayAsync();

        public Task<T?> ReadFromJsonAsync<T>(CancellationToken cancellationToken = default)
            => _content.ReadFromJsonAsync<T>(cancellationToken);
    }
}