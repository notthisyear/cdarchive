using System;
using System.Net;
using System.Net.Http.Headers;

namespace CdArchiveBackend.Interfaces
{
    internal interface IHttpResponseMessage : IDisposable
    {
        public HttpStatusCode StatusCode { get; }

        public bool IsSuccessStatusCode { get; }

        public IHttpContent Content { get; }

        public HttpResponseHeaders Headers { get; }

        public void EnsureSuccessStatusCode();
    }
}