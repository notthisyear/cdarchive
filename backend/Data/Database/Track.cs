namespace CdArchiveBackend.Data.Database
{
    internal sealed class Track
    {
        public long Id { get; set; }
        
        public long ReleaseId { get; set; }
        
        public int DiscNumber { get; set; } = 1;
        
        public int TrackNumber { get; set; }
        
        public required string Title { get; set; }
        
        public int? DurationSeconds { get; set; }

        public Release Release { get; set; } = default!;
    }
}
