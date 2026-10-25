using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using CdArchiveBackend.Interfaces;

namespace CdArchiveBackend.Common.HttpWrappers
{
    internal sealed class HttpResponseMessageWrapper(HttpResponseMessage message) : IHttpResponseMessage
    {
        private readonly HttpResponseMessage _message = message;

        public IHttpContent Content =>
            new HttpContentWrapper(_message.Content);

        public bool IsSuccessStatusCode =>
            _message.IsSuccessStatusCode;

        public HttpStatusCode StatusCode =>
            _message.StatusCode;

        public HttpResponseHeaders Headers =>
            _message.Headers;

        public void Dispose()
            => _message.Dispose();

        public void EnsureSuccessStatusCode()
            => _message.EnsureSuccessStatusCode();
    }
}