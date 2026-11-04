namespace CdArchiveBackend.Data.Database
{
    // One of a creator's images, looked up or uploaded by the user
    internal sealed class CreatorImage
    {
        public long CreatorId { get; set; }

        public long MediaFileId { get; set; }

        /// <summary>
        /// Where to show this image among the creator's images. The first one is the one shown.
        /// </summary>
        public int SortOrder { get; set; }

        public Creator Creator { get; set; } = null!;

        public MediaFile MediaFile { get; set; } = null!;
    }
}
