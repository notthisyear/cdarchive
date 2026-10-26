using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using CdArchiveBackend.Services.Utilities;
using System;
using System.Collections.Generic;
using Xunit;

namespace CdArchiveTests
{
    public class ReleaseFingerprintTests
    {
        private static readonly IReadOnlyList<NewReleaseCredit> Credits =
            [new(1, CreditRole.Primary, " & ", null), new(2, CreditRole.Primary, "", null)];

        private static readonly IReadOnlyList<ReleaseTrack> Tracks =
            [new(1, 1, "Detroit Rock City", 318), new(1, 2, "King of the Night Time World", 199)];

        private static NewReleaseRequest Destroyer => new(
            Title: "Destroyer",
            Type: ReleaseType.Record,
            Status: ReleaseStatus.Owned,
            Format: ReleaseFormat.Cd,
            ReleaseYear: 1997,
            FirstReleaseYear: 1976,
            DurationSeconds: 2060,
            Barcode: "731453238428",
            MusicBrainzReleaseId: null,
            MusicBrainzReleaseGroupId: null,
            SpotifyAlbumId: null,
            Notes: null,
            Score: null,
            AcquiredOn: null,
            Credits: Credits,
            Tracks: Tracks,
            Images: []);

        private static string Of(NewReleaseRequest request) => ReleaseFingerprint.Compute(request);

        [Fact]
        public void IsHexSha256()
            => Assert.Matches("^[0-9a-f]{64}$", Of(Destroyer));

        [Fact]
        public void SameRelease_HasTheSameFingerprint()
            => Assert.Equal(Of(Destroyer), Of(Destroyer));

        #region What doesn't count
        [Fact]
        public void CaseAndWhitespace_DontCount()
        {
            var sloppy = Destroyer with
            {
                Title = "  DESTROYER ",
                Barcode = " 731453238428",
                Tracks = [new(1, 1, "detroit  rock city", 318), new(1, 2, "King of the Night Time World ", 199)]
            };

            Assert.Equal(Of(Destroyer), Of(sloppy));
        }

        [Fact]
        public void TrackOrderSent_DoesntCount()
            => Assert.Equal(Of(Destroyer), Of(Destroyer with { Tracks = [Tracks[1], Tracks[0]] }));

        [Fact]
        public void EmptyAndMissingBarcode_AreTheSame()
            => Assert.Equal(Of(Destroyer with { Barcode = null }), Of(Destroyer with { Barcode = "  " }));

        [Fact]
        public void TheUsersOwnFields_DontCount()
        {
            var mine = Destroyer with
            {
                Status = ReleaseStatus.Wanted,
                Notes = "Signed by Gene",
                Score = 9,
                AcquiredOn = new DateOnly(2001, 5, 1)
            };

            Assert.Equal(Of(Destroyer), Of(mine));
        }

        [Fact]
        public void Images_DontCount()
            => Assert.Equal(Of(Destroyer), Of(Destroyer with { Images = [new(MediaSource.Upload, null, 0)] }));

        [Fact]
        public void ExternalIds_DontCount()
        {
            var linked = Destroyer with
            {
                MusicBrainzReleaseId = "8a5d9f5a-3c0e-4b5e-9a39-1f2d3c4b5a69",
                MusicBrainzReleaseGroupId = "541eb339-d0c6-3740-b665-8ad6c09da331",
                SpotifyAlbumId = "0YQ5ZsSIbzeRwFMBiQh0eg"
            };

            Assert.Equal(Of(Destroyer), Of(linked));
        }
        #endregion

        #region What does
        public static TheoryData<string, NewReleaseRequest> Differences => new()
        {
            { "title", Destroyer with { Title = "Destroyer (Resurrected)" } },
            { "title punctuation", Destroyer with { Title = "Destroyer!" } },
            { "type", Destroyer with { Type = ReleaseType.Film } },
            { "format", Destroyer with { Format = ReleaseFormat.Vinyl } },
            { "release year", Destroyer with { ReleaseYear = 2012 } },
            { "first release year", Destroyer with { FirstReleaseYear = 1975 } },
            { "duration", Destroyer with { DurationSeconds = 2061 } },
            { "barcode", Destroyer with { Barcode = "075596061423" } },
            { "artist", Destroyer with { Credits = [new(1, CreditRole.Primary, " & ", null), new(3, CreditRole.Primary, "", null)] } },
            { "billing order", Destroyer with { Credits = [new(2, CreditRole.Primary, " & ", null), new(1, CreditRole.Primary, "", null)] } },
            { "role", Destroyer with { Credits = [new(1, CreditRole.Primary, " & ", null), new(2, CreditRole.Producer, "", null)] } },
            { "join phrase", Destroyer with { Credits = [new(1, CreditRole.Primary, " feat. ", null), new(2, CreditRole.Primary, "", null)] } },
            { "track title", Destroyer with { Tracks = [Tracks[0], new(1, 2, "Beth", 199)] } },
            { "track duration", Destroyer with { Tracks = [Tracks[0], new(1, 2, "King of the Night Time World", 200)] } },
            { "track position", Destroyer with { Tracks = [Tracks[0], new(2, 1, "King of the Night Time World", 199)] } },
            { "missing track", Destroyer with { Tracks = [Tracks[0]] } }
        };

        [Theory]
        [MemberData(nameof(Differences))]
        public void Difference_GivesAnotherFingerprint(string difference, NewReleaseRequest other)
        {
            Assert.NotNull(difference);
            Assert.NotEqual(Of(Destroyer), Of(other));
        }
        #endregion
    }
}
