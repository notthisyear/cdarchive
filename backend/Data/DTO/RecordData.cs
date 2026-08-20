using System;
using System.Collections.Generic;

namespace CdArchiveBackend.Data.DTO
{
    public readonly record struct ArtistNameOnly(string Name);

    public readonly record struct Summary(string Name, List<ArtistNameOnly> Artists, int Year, string? ImageUrl, string? SpotifyLink);

    public readonly record struct Track(string Title, int TrackNumber, int DurationSeconds);

    public readonly record struct RecordData(Summary Summary, int DurationSeconds, List<Track> Tracks)
    {
        public bool Validate()
        {
            if (string.IsNullOrEmpty(Summary.Name))
                return false;

            if (DurationSeconds < 0)
                return false;

            if (Summary.Artists.Count == 0)
                return false;

            foreach (var artist in Summary.Artists)
            {
                if (string.IsNullOrEmpty(artist.Name))
                    return false;
            }

            if (Tracks.Count > 0)
            {
                foreach (var track in Tracks)
                {
                    if (string.IsNullOrEmpty(track.Title))
                        return false;

                    if (track.TrackNumber < 1)
                        return false;

                    if (DurationSeconds < 0)
                        return false;
                }
            }

            return true;
        }

        public readonly void DebugPrint()
        {
            Console.WriteLine("Summary:");
            Console.WriteLine($"\tName: {Summary.Name}");
            Console.WriteLine($"\tYear: {Summary.Year}");
            Console.WriteLine($"\tImageUrl: {Summary.ImageUrl}");
            Console.WriteLine($"\tSpotifyLink: {Summary.SpotifyLink}");
            
            Console.WriteLine("\tArtists:");
            foreach (var artist in Summary.Artists)
                Console.WriteLine($"\t\tName: {artist.Name}");
            
            Console.WriteLine($"LengthSeconds: {DurationSeconds}");

            Console.WriteLine("Tracks:");
            foreach (var track in Tracks)
            {
                Console.WriteLine($"\t\tTrackNumber: {track.TrackNumber}");
                Console.WriteLine($"\t\tTitle: {track.Title}");
                Console.WriteLine($"\t\tDurationSeconds: {track.DurationSeconds}\n");
            }

        }
    }
}
