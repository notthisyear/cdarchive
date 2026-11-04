using System.ComponentModel.DataAnnotations;

namespace CdArchiveBackend.Data.External.Wikimedia
{
    public sealed class Options
    {
        public const string SectionName = "Wikimedia";

        [Required, MinLength(1)]
        public string UserAgent { get; set; } = "";

        [Range(1, 10)]
        public int MaxNumberOfAttempts { get; set; } = 3;

        [Required, MinLength(1)]
        public string WikidataApiUrl { get; set; } = "";

        [Required, MinLength(1)]
        public string CommonsApiUrl { get; set; } = "";

    }
}
