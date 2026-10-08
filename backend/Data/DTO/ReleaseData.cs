using CdArchiveBackend.Common;
using System;
using System.Collections.Generic;

namespace CdArchiveBackend.Data.DTO
{
    public readonly record struct Summary(string Name, List<Artist> Artists, int Year, string? ImageUrl, string? SpotifyLink)
    {
        public bool IsSameSummary(Summary other)
        {
            if (!Name.Equals(other.Name, StringComparison.Ordinal))
                return false;

            if (Year != other.Year)
                return false;

            if (!Utilities.CompareNullableStrings(SpotifyLink, other.SpotifyLink))
                return false;

            if (Artists.Count != other.Artists.Count)
                return false;

            for (var i = 0; i < Artists.Count; i++)
            {
                if (Artists[i].Id != other.Artists[i].Id)
                    return false;
            }

            return true;
        }
    }

    public readonly record struct Track(string Title, int DiscNumber, int TrackNumber, int DurationSeconds);

    public readonly record struct ReleaseData(long? Id, Summary Summary, int DurationSeconds, List<Track> Tracks)
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

                if (artist.Id == null)
                    return false;
            }

            if (Tracks.Count > 0)
            {
                foreach (var track in Tracks)
                {
                    if (string.IsNullOrEmpty(track.Title))
                        return false;

                    if (track.DiscNumber < 1)
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
            Console.WriteLine($"Id: {Id}");
            Console.WriteLine("Summary:");
            Console.WriteLine($"\tName: {Summary.Name}");
            Console.WriteLine($"\tYear: {Summary.Year}");
            Console.WriteLine($"\tImageUrl: {Summary.ImageUrl}");
            Console.WriteLine($"\tSpotifyLink: {Summary.SpotifyLink}");

            Console.WriteLine("\tArtists:");
            foreach (var artist in Summary.Artists)
            {
                Console.WriteLine($"\t\tId: {artist.Id}");
                Console.WriteLine($"\t\tName: {artist.Name}");
                Console.WriteLine($"\t\tImageUrl: {artist.ImageUrl}");
            }

            Console.WriteLine($"LengthSeconds: {DurationSeconds}");

            Console.WriteLine("Tracks:");
            foreach (var track in Tracks)
            {
                Console.WriteLine($"\t\tDiscNumber: {track.DiscNumber}");
                Console.WriteLine($"\t\tTrackNumber: {track.TrackNumber}");
                Console.WriteLine($"\t\tTitle: {track.Title}");
                Console.WriteLine($"\t\tDurationSeconds: {track.DurationSeconds}\n");
            }

        }

        public bool IsSameRelease(ReleaseData other)
        {
            if (DurationSeconds != other.DurationSeconds)
                return false;

            if (!Summary.IsSameSummary(other.Summary))
                return false;

            if (Tracks.Count != other.Tracks.Count)
                return false;

            for (var i = 0; i < Tracks.Count; i++)
            {
                if (Tracks[i] != other.Tracks[i])
                    return false;
            }

            return true;
        }
    }
}
