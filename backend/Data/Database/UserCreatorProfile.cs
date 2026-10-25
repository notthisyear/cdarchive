namespace CdArchiveBackend.Data.Database
{
    internal sealed class UserCreatorProfile
    {
        public long Id { get; set; }

        public long UserId { get; set; }

        public long CreatorId { get; set; }

        public long? CustomImageId { get; set; }

        public MediaFile? CustomImage { get; set; }

        public string? DisplayNameOverride { get; set; }

        public string? Notes { get; set; }

        public bool IsFavorite { get; set; }

        public Creator Creator { get; set; } = default!;
    }
}
