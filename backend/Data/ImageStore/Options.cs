using System.ComponentModel.DataAnnotations;

namespace CdArchiveBackend.Data.ImageStore
{
    public sealed class Options
    {
        public const string SectionName = "ImageStore";

        [Required, MinLength(1)]
        public string Shared { get; set; } = "";
        
        [Required, MinLength(1)]
        public string User { get; set; } = "";
    }
}
