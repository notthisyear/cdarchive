namespace CdArchiveBackend.Data
{
    public readonly record struct UserData
    {
        public long Id { get; init; }
        
        public string Username { get; init; }

        public string Email { get; init; }

    }
}
