using System.Net.Http;
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
    }
}