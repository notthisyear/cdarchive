namespace CdArchiveBackend.Data.External.MusicBrainz
{
    internal sealed record MusicBrainzTrack(string Title, int DiscNumber, int TrackNumber, int DurationSeconds);
}
