using CdArchiveBackend.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace CdArchiveBackend
{
    internal sealed class DatabaseContext(DbContextOptions<DatabaseContext> contextOptions) : DbContext(contextOptions)
    {
        public required DbSet<UserData> Users { get; init; }

        public required DbSet<Release> Releases { get; init; }

        public required DbSet<Artist> Artists { get; init; }

        public required DbSet<UserReleases> UserReleases { get; init; }

        public required DbSet<ReleaseArtist> ReleaseArtists { get; init; }

        public required DbSet<ReleaseTrack> ReleaseTracks { get; init; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {

            modelBuilder.Entity<UserData>(entity =>
            {
                entity.HasKey(x => x.Id);

                entity.Property(x => x.CreatedAt)
                .HasDefaultValueSql("now()");
            });

            modelBuilder.Entity<Artist>(entity =>
            {
                entity.HasKey(x => x.Id);
            });

            modelBuilder.Entity<Release>(entity =>
            {
                entity.HasKey(x => x.Id);

                // The entity has a Tracks collection that each refer to the same release id, 1 : N relation
                entity.HasMany(x => x.Tracks)
                .WithOne(x => x.Release)
                .HasForeignKey(x => x.ReleaseId)
                .OnDelete(DeleteBehavior.Cascade);

                // The entity has a ReleaseArtist collection that each refer to the same release id, 1 : N relation
                entity.HasMany(x => x.ReleaseArtists)
                .WithOne(x => x.Release)
                .HasForeignKey(x => x.ReleaseId)
                .OnDelete(DeleteBehavior.Cascade);

                // The entity has a previous release id the refers to a specific other release, 1 : 1 relation
                entity.HasOne(x => x.PreviousRelease)
                .WithMany()
                .HasForeignKey(x => x.PreviousReleaseId)
                .OnDelete(DeleteBehavior.SetNull);

                // The entity has a next release id the refers to a specific other release, 1 : 1 relation
                entity.HasOne(x => x.NextRelease)
                .WithMany()
                .HasForeignKey(x => x.NextReleaseId)
                .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<UserReleases>(entity =>
            {
                entity.HasKey(x => new { x.UserId, x.ReleaseId });

                // The entity has a user field keyed on the user ID
                entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId);

                // The entity has a release field keyed on the release ID
                entity.HasOne(x => x.Release)
                .WithMany()
                .HasForeignKey(x => x.ReleaseId);

                entity.Property(x => x.AddedDate)
                .HasDefaultValueSql("now()");
            });


            modelBuilder.Entity<ReleaseArtist>(entity =>
            {
                entity.HasKey(x => new { x.ReleaseId, x.ArtistId });
                // We add the extra index on artist ID to speed up queries on that property
                entity.HasIndex(x => x.ArtistId);

                // The ReleaseArtist -> Artist navigation isn't setup yet, so we do that here
                entity.HasOne(x => x.Artist)
                    .WithMany(x => x.ReleaseArtists)
                    .HasForeignKey(x => x.ArtistId);

            });

            modelBuilder.Entity<ReleaseTrack>(entity =>
            {
                entity.HasKey(x => new { x.ReleaseId, x.DiscNumber, x.TrackNumber });
                // The relation betweemn the track and the release is already configured
            });
        }
    }
}