using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CdArchiveBackend.Interfaces;

namespace CdArchiveBackend.Common
{
    internal class FileSystem : IFileSystem
    {
        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default)
            => File.WriteAllBytesAsync(path, bytes, cancellationToken);
    }
}