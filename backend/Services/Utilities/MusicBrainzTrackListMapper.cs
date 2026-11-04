using CdArchiveBackend.Data.External.MusicBrainz;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CdArchiveBackend.Services.Utilities
{
    internal static partial class MusicBrainzTrackListMapper
    {
        // A vinyl side printed in front of the track number: "A1", "B-4", "AA3" (one or two letters, then a digit).
        // "7", "1-05", "Bonus 1" and "Intro" have no side.
        [GeneratedRegex(@"^\s*(?<side>[A-Za-z]{1,2})[\s.\-]*\d", RegexOptions.CultureInvariant)]
        private static partial Regex SideRegex();

        public static List<MusicBrainzTrack> ToTracks(this TrackListDto? dto)
        {
            var result = new List<MusicBrainzTrack>();

            // Note: Every distinct (medium, side) becomes its own disc, numbered in order of appearance.            
            var discNumbers = new Dictionary<(int medium, string side), int>();
            var tracksOnDisc = new Dictionary<int, int>();

            var media = dto?.Media ?? [];
            for (var m = 0; m < media.Count; m++)
            {
                var tracks = media[m].Tracks ?? [];

                // Sort only if every track has a position; otherwise trust the order the API returned
                if (tracks.Count > 0 && tracks.All(t => t.Position is not null))
                    tracks = [.. tracks.OrderBy(t => t.Position)];

                foreach (var track in tracks)
                {
                    var side = ParseSide(track.Number);
                    if (!discNumbers.TryGetValue((m, side), out var disc))
                    {
                        disc = discNumbers.Count + 1;
                        discNumbers[(m, side)] = disc;
                    }

                    // Numbering restarts at 1 on every disc/side
                    var trackNumber = tracksOnDisc.GetValueOrDefault(disc) + 1;
                    tracksOnDisc[disc] = trackNumber;

                    var seconds = track.LengthMs is int ms and > 0 ? (ms + 500) / 1000 : 0;   // rounds half up

                    result.Add(new MusicBrainzTrack(
                        Title: track.Title?.Trim() ?? "",
                        DiscNumber: disc,
                        TrackNumber: trackNumber,
                        DurationSeconds: seconds));
                }
            }

            return result;
        }

        private static string ParseSide(string? number)
        {
            var match = SideRegex().Match(number ?? "");
            return match.Success ? match.Groups["side"].Value.ToUpperInvariant() : "";
        }
    }
}
