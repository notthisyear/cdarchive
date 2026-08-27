using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using CdArchiveBackend.Interfaces;

namespace CdArchiveBackend.Common.HttpWrappers
{
    internal sealed class HttpContentWrapper(HttpContent content) : IHttpContent
    {
        private readonly HttpContent _content = content;

        public HttpContentHeaders Headers
            => _content.Headers;

        public Task<byte[]> ReadAsByteArrayAsync()
            => _content.ReadAsByteArrayAsync();
    }
}