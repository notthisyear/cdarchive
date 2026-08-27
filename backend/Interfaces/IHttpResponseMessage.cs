namespace CdArchiveBackend.Interfaces
{
    internal interface IHttpResponseMessage
    {
        public bool IsSuccessStatusCode { get; }

        public IHttpContent Content { get; }
    }
}