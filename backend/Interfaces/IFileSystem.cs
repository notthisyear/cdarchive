using System.Threading;
using System.Threading.Tasks;

namespace CdArchiveBackend.Interfaces
{
    internal interface IFileSystem
    {
        public Task WriteAllBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken = default);

        // Creates the directory and any missing parents; does nothing if it's already there
        public void CreateDirectory(string path);

        // Does nothing if the file isn't there
        public void DeleteFile(string path);
    }
}
