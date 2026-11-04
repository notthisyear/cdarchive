using System.ComponentModel.DataAnnotations;

namespace CdArchiveBackend.Data.External.CoverArtArchive
{
    public sealed class Options
    {
        public const string SectionName = "CoverArtArchive";

        [Required, MinLength(1)]
        public string UserAgent { get; set; } = "";

        [Range(1, 10)]
        public int MaxNumberOfAttempts { get; set; } = 3;

        [Required, MinLength(1)]
        public string ApiUrl { get; set; } = "";
    }
}
