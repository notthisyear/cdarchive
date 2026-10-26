namespace CdArchiveBackend.Data.Database
{
    // One of a release's images (front cover, back, booklet...). A shared MediaFile may be used by
    // several users' releases.
    internal sealed class ReleaseImage
    {
        public long ReleaseId { get; set; }

        public long MediaFileId { get; set; }

        /// <summary>
        /// Where to show this image among the release's images. The first one is the cover.
        /// </summary>
        public int SortOrder { get; set; }

        public Release Release { get; set; } = null!;

        public MediaFile MediaFile { get; set; } = null!;
    }
}
