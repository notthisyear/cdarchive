namespace CdArchiveBackend.Data.Database
{
    public enum CreditRole
    {
        Primary,
        Featured,
        Composer,
        Producer,
        Author,
        Illustrator,
        Editor,
        Director,
        Writer,
        Actor,
        Other
    }

    internal sealed class ReleaseCreator
    {
        public long ReleaseId { get; set; }

        public long CreatorId { get; set; }

        public CreditRole Role { get; set; }
        /// <summary>
        /// Name of the character played by the creator in this released. Meant for use in films.
        /// </summary>
        public string? CharacterName { get; set; }

        /// <summary>
        /// Where to order this ReleaseCreator if all creators for this release are written in a list. Meant to preserve billing order.
        /// </summary>
        public int? SortOrder { get; set; }

        /// <summary>
        /// Text written after this creator's name when the credits are joined into one line, e.g. " &amp; " or " feat. ".
        /// Comes from MusicBrainz's artist credit. Empty for the last creator.
        /// </summary>
        public string JoinPhrase { get; set; } = "";

        public Release Release { get; set; } = null!;

        public Creator Creator { get; set; } = null!;
    }
}
