using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace CdArchiveBackend.Interfaces
{
    internal interface IHttpContent
    {
        public HttpContentHeaders Headers { get; }

        public Task<byte[]> ReadAsByteArrayAsync();
    }
}