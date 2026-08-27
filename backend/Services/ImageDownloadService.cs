using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CdArchiveBackend.Interfaces;
using static System.Net.Mime.MediaTypeNames;

namespace CdArchiveBackend.Services
{
    internal sealed partial class ImageDownloadService : IAsyncDisposable
    {
        private readonly record struct DownloadResult(bool Success, string ImageName);
        private readonly record struct DownloadRequest(string ImageUrl, TaskCompletionSource<string> Tcs);

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

        public TaskCompletionSource<string>? AddDownloadRequest(string imageUrl)
        {
            var tcs = new TaskCompletionSource<string>();
            if (_downloadChannel.Writer.TryWrite(new(imageUrl, tcs)))
                return tcs;
            return null;
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

                item.Tcs.SetResult(result.Success ? result.ImageName : string.Empty);
            }
        }

        private async Task<DownloadResult> TryHandleImageData(string imageUrl)
        {
            var match = ImageDataPrefixRegex().Match(imageUrl);
            if (!match.Success)
                return new(false, string.Empty);

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
                return new(false, string.Empty);

            var fileName = $"{Guid.NewGuid()}.{imageType}";
            var path = Path.Combine(_pathToImageStore, fileName);
            var data = Convert.FromBase64String(ImageDataPrefixRegex().Replace(imageUrl, ""));

            try
            {
                await _fileSystem.WriteAllBytesAsync(path, data).ConfigureAwait(false);
                return new(true, fileName);
            }
            catch (Exception)
            {
                return new(false, string.Empty);
            }
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
                        return new(false, string.Empty);
                }
                catch (Exception)
                {
                    return new(false, string.Empty);
                }
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (string.IsNullOrEmpty(mediaType))
                return new(false, string.Empty);

            var fileExtension = mediaType switch
            {
                Image.Jpeg => "jpg",
                Image.Png => "png",
                Image.Tiff => "tiff",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(fileExtension))
                return new(false, string.Empty);

            var content = await response.Content.ReadAsByteArrayAsync();
            if (content == default || content.Length == 0)
                return new(false, string.Empty);

            return await WriteImageToDisk(content, _pathToImageStore, fileExtension).ConfigureAwait(false);
        }

        private async Task<DownloadResult> WriteImageToDisk(byte[] imageData, string pathToImageStore, string fileExtension)
        {
            var fileName = $"{Guid.NewGuid()}.{fileExtension}";
            var path = Path.Combine(pathToImageStore, fileName);

            try
            {
                await _fileSystem.WriteAllBytesAsync(path, imageData).ConfigureAwait(false);
                return new(true, fileName);
            }
            catch (Exception)
            {
                return new(false, string.Empty);
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