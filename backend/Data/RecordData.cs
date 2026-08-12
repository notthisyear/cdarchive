using System.Collections.Generic;
using System.Text;

namespace CdArchiveBackend.Data
{
    public readonly record struct RecordData
    {
        public RecordSummary Summary { get; init; }

        public long LengthSeconds { get; init; }

        public RecordSummary? PreviousRelease { get; init; }

        public RecordSummary? NextRelease { get; init; }

        public List<Track> Tracks { get; init; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            foreach (var track in Tracks)
            {
                sb.AppendLine(track.ToString("\t"));
                sb.AppendLine();
            }
            return $"Summary:\n{Summary.ToString("\t")}\nLengthSeconds: {LengthSeconds}\nPreviousRelease:\n{(PreviousRelease.HasValue ? PreviousRelease.Value.ToString("\t") : string.Empty)}\nNextRelease:\n{(NextRelease.HasValue ? NextRelease.Value.ToString("\t") : string.Empty)}\nTracks:\n{sb}";
        }
    }
}
