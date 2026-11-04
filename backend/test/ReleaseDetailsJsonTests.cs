using CdArchiveBackend.Data.Database;
using CdArchiveBackend.Data.DTO;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace CdArchiveTests
{
    // ReleaseDetails as the frontend gets it (recordDetails.js reads these names)
    public sealed class ReleaseDetailsJsonTests
    {
        // As the backend sends JSON (see ConfigureHttpJsonOptions in Program.cs)
        private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private static JsonElement Serialize()
        {
            var details = new ReleaseDetails(
                new ReleaseSummary(1, "American Fool", "John Cougar", null, 1982, ReleaseType.Record, ReleaseStatus.Owned),
                ReleaseFormat.Cd, 1982, 1982, 2240, null, "US", null, null, null, true, DateTimeOffset.UnixEpoch,
                [new ReleaseCredit(2, "John Mellencamp", CreditRole.Primary, "", null, "John Cougar", "US", "a2c9c3ab-7a3f-4b0b-9a4b-0a7a5b2c4d11")],
                [],
                [],
                new Dictionary<ExternalSource, string>
                {
                    [ExternalSource.MusicBrainz] = "b2a5e1c4-1111-4a7e-9a2b-6f0e2a3c4d5e",
                    [ExternalSource.Spotify] = "4Wl2yU5D5cRz7Gt0qX4k8b"
                });

            return JsonSerializer.SerializeToElement(details, Options);
        }

        [Fact]
        public void ExternalIdsAreKeyedBySourceName()
        {
            var ids = Serialize().GetProperty("externalIds");

            Assert.Equal("b2a5e1c4-1111-4a7e-9a2b-6f0e2a3c4d5e", ids.GetProperty("MusicBrainz").GetString());
            Assert.Equal("4Wl2yU5D5cRz7Gt0qX4k8b", ids.GetProperty("Spotify").GetString());
        }

        [Fact]
        public void CreditsHaveTheirRoleByNameAndTheCreatorsCountry()
        {
            var credit = Serialize().GetProperty("credits")[0];

            Assert.Equal("Primary", credit.GetProperty("role").GetString());
            Assert.Equal("John Cougar", credit.GetProperty("creditedName").GetString());
            Assert.Equal("US", credit.GetProperty("country").GetString());
            Assert.Equal("a2c9c3ab-7a3f-4b0b-9a4b-0a7a5b2c4d11", credit.GetProperty("musicBrainzId").GetString());
        }

        [Fact]
        public void ReleaseHasItsCountryAndYears()
        {
            var json = Serialize();

            Assert.Equal("US", json.GetProperty("country").GetString());
            Assert.Equal(1982, json.GetProperty("releaseYear").GetInt32());
            Assert.Equal(1982, json.GetProperty("firstReleaseYear").GetInt32());
            Assert.True(json.GetProperty("consumed").GetBoolean());
        }
    }
}
