using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Interfaces
{
    internal interface IHttpContent
    {
        public HttpContentHeaders Headers { get; }

        public Task<byte[]> ReadAsByteArrayAsync();

        public Task<T?> ReadFromJsonAsync<T>(CancellationToken cancellationToken = default);
    }
}