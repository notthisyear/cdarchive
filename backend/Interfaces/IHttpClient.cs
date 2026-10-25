using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Interfaces
{
    internal interface IHttpClient : IDisposable
    {
        public Task<IHttpResponseMessage> GetAsync([StringSyntax("Uri")] string? requestUri, CancellationToken ct = default);
    }
}