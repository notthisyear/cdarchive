using CdArchiveBackend.Data;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CdArchiveBackend.Services
{
    public class RecordsService
    {
        private static readonly Artist s_pinkFloyd = new() { Id = 0, Name = "Pink Floyd", ImageUrl = string.Empty, SpotifyLink = string.Empty };
        private static readonly Artist s_genesis = new() { Id = 1, Name = "Genesis", ImageUrl = string.Empty, SpotifyLink = string.Empty };

        private static readonly RecordSummary s_animalsSummary = new() { Id = 0, Artist = s_pinkFloyd,  Name = "Animals", Year = 1977, ImageUrl = string.Empty, SpotifyLink = string.Empty };
        private static readonly RecordSummary s_foxtrotSummary = new() { Id = 1, Artist = s_genesis, Name = "Foxtrot", Year = 1972, ImageUrl = string.Empty, SpotifyLink = string.Empty };

        private static readonly RecordData s_animals = new()
        {
            Summary = s_animalsSummary,
            LengthSeconds = 2500,
            NextRelease = null,
            PreviousRelease = null,
            Tracks =
            [
                new() { TrackNumber = 1, Title = "Pigs on the Wing (Part 1)", DurationSeconds = 85 },
                new() { TrackNumber = 2, Title = "Dogs", DurationSeconds = 1024 },
                new() { TrackNumber = 3, Title = "Pigs (Three Different Ones)", DurationSeconds = 684 },
                new() { TrackNumber = 4, Title = "Sheep", DurationSeconds = 620 },
                new() { TrackNumber = 5, Title = "Pigs on the Wing (Part 2)", DurationSeconds = 84 }
            ]
        };
        private static readonly RecordData s_foxtrot = new()
        {
            Summary = s_foxtrotSummary,
            LengthSeconds = 3073,
            NextRelease = null,
            PreviousRelease = null,
            Tracks =
            [
                new() { TrackNumber = 1, Title = "Watcher of the Skies", DurationSeconds = 444  },
                new() { TrackNumber = 2, Title = "Time Table", DurationSeconds = 287  },
                new() { TrackNumber = 3, Title = "Get 'Em Out by Friday", DurationSeconds = 518  },
                new() { TrackNumber = 4, Title = "Can-Utility and the Coastliners", DurationSeconds = 348  },
                new() { TrackNumber = 5, Title = "Horizons", DurationSeconds = 102  },
                new() { TrackNumber = 6, Title = "Supper's Ready", DurationSeconds = 1374  }
            ]
        };

        public Task<List<RecordSummary>> GetRecordSummaries(int userId, int offset, int numberOfRecordsToGet)
        {
            return Task.FromResult<List<RecordSummary>>([s_animalsSummary, s_foxtrotSummary]);
        }

        public Task<RecordData> GetRecordData(int recordId)
        {
            if (recordId == 0)
                return Task.FromResult(s_animals);
            else
                return Task.FromResult(s_foxtrot);
        }
    }
}
