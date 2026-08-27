using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Interfaces
{
    internal interface IFileSystem
    {
        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default);
    }
}