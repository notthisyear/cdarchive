using CdArchiveBackend.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace CdArchiveBackend.Services
{
    internal sealed partial class ImageDownloadService : IAsyncDisposable
    {
        public readonly record struct Result(bool DownloadFailed, string ImageName = "", int ByteCount = 0);
        private readonly record struct DownloadResult(bool Success, string ImageName, int ByteCount);
        private readonly record struct DownloadRequest(string ImageUrl, TaskCompletionSource<Result> Tcs);

        #region Private fields
        private const string ImageTypeCaptureGroupName = "TYPE";

        [GeneratedRegex(@"data:image/(?<" + ImageTypeCaptureGroupName + @">\w+);base64,", RegexOptions.Compiled)]
        private static partial Regex ImageDataPrefixRegex();

        private readonly IFileSystem _fileSystem;
        private readonly Func<IHttpClient> _httpClientFactory;
        private readonly string _pathToImageStore;
        private readonly Channel<DownloadRequest> _downloadChannel = Channel.CreateUnbounded<DownloadRequest>(options: new() { SingleReader = true });
        private readonly Task _monitorDownloadChannelTask;
        private int _disposed = 0;
        #endregion

        public ImageDownloadService(IFileSystem fileSystem, Func<IHttpClient> httpClientFactory, string pathToImageStore)
        {
            _fileSystem = fileSystem;
            _httpClientFactory = httpClientFactory;
            _pathToImageStore = pathToImageStore;
            _monitorDownloadChannelTask = MonitorDownloadChannel();
        }

        public TaskCompletionSource<Result>? AddDownloadRequest(string imageUrl)
        {
            var tcs = new TaskCompletionSource<Result>();
            if (_downloadChannel.Writer.TryWrite(new(imageUrl, tcs)))
                return tcs;

            return null;
        }

        public bool TryGetSha1ForLocalImage(string imageName, out string sha1)
        {
            sha1 = string.Empty;
            try
            {
                using FileStream stream = new(Path.Combine(_pathToImageStore, imageName), FileMode.Open, FileAccess.Read);
                sha1 = Convert.ToHexString(SHA1.HashData(stream));
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        #region Private methods
        private async Task MonitorDownloadChannel()
        {
            await foreach (var item in _downloadChannel.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                var isData = item.ImageUrl.StartsWith("data");
                var result = isData ?
                    await TryHandleImageData(item.ImageUrl).ConfigureAwait(false) :
                    await TryHandleImageUrl(item.ImageUrl).ConfigureAwait(false);

                item.Tcs.SetResult(new(!result.Success , result.Success ? result.ImageName : string.Empty, result.ByteCount));
            }
        }

        private async Task<DownloadResult> TryHandleImageData(string imageUrl)
        {
            var match = ImageDataPrefixRegex().Match(imageUrl);
            if (!match.Success)
                return new(false, string.Empty, 0);

            var imageType = string.Empty;
            foreach (var group in match.Groups.Cast<Group>())
            {
                if (group.Name.Equals(ImageTypeCaptureGroupName, StringComparison.Ordinal))
                {
                    imageType = group.Value;
                    break;
                }
            }

            if (string.IsNullOrEmpty(imageType))
                return new(false, string.Empty, 0);

            return await WriteImageToDisk(
                Convert.FromBase64String(ImageDataPrefixRegex().Replace(imageUrl, "")),
                _pathToImageStore,
                imageType).ConfigureAwait(false);
        }

        private async Task<DownloadResult> TryHandleImageUrl(string imageUrl)
        {
            IHttpResponseMessage response;
            using (var client = _httpClientFactory())
            {
                try
                {
                    response = await client.GetAsync(imageUrl);
                    if (!response.IsSuccessStatusCode)
                        return new(false, string.Empty, 0);
                }
                catch (Exception)
                {
                    return new(false, string.Empty, 0);
                }
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (string.IsNullOrEmpty(mediaType))
                return new(false, string.Empty, 0);

            var fileExtension = mediaType switch
            {
                Image.Jpeg => "jpg",
                Image.Png => "png",
                Image.Tiff => "tiff",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(fileExtension))
                return new(false, string.Empty, 0);

            var content = await response.Content.ReadAsByteArrayAsync();
            if (content == default || content.Length == 0)
                return new(false, string.Empty, 0);

            return await WriteImageToDisk(content, _pathToImageStore, fileExtension).ConfigureAwait(false);
        }

        private async Task<DownloadResult> WriteImageToDisk(byte[] imageData, string pathToImageStore, string fileExtension)
        {
            var fileName = $"{Guid.NewGuid()}.{fileExtension}";
            var path = Path.Combine(pathToImageStore, fileName);

            try
            {
                await _fileSystem.WriteAllBytesAsync(path, imageData).ConfigureAwait(false);
                return new(true, fileName, imageData.Length);
            }
            catch (Exception)
            {
                return new(false, string.Empty, 0);
            }
        }
        #endregion

        #region Disposal
        public async ValueTask DisposeAsync()
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            Interlocked.Exchange(ref _disposed, 1);

            _downloadChannel.Writer.Complete();

            try
            {
                await _monitorDownloadChannelTask.ConfigureAwait(false);
            }
            finally
            {
                _monitorDownloadChannelTask.Dispose();
            }

            GC.SuppressFinalize(this);
        }
        #endregion
    }
}