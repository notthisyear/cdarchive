namespace CdArchiveBackend.Data.DTO
{
    public readonly record struct Artist(long? Id, string Name, string? ImageUrl);
}