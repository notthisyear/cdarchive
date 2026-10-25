namespace CdArchiveBackend.Data
{
    public sealed record StreamingProgress(string Stage,
                                           string? Step = null,
                                           int? Attempt = null,
                                           int? Of = null);
}
