using CdArchiveBackend.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace CdArchiveBackend
{
    internal sealed class DatabaseContext(DbContextOptions<DatabaseContext> contextOptions) : DbContext(contextOptions)
    {
        public DbSet<Creator> Creators => Set<Creator>();

        public DbSet<CreatorImage> CreatorImages => Set<CreatorImage>();

        public DbSet<Release> Releases => Set<Release>();

        public DbSet<ReleaseCreator> ReleaseCreators => Set<ReleaseCreator>();

        public DbSet<ReleaseImage> ReleaseImages => Set<ReleaseImage>();

        public DbSet<Track> Tracks => Set<Track>();

        public DbSet<UserData> Users => Set<UserData>();

        public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

        public DbSet<CreatorExternalId> CreatorExternalIds => Set<CreatorExternalId>();

        public DbSet<ReleaseExternalId> ReleaseExternalIds => Set<ReleaseExternalId>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.EnableSensitiveDataLogging();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserData>(entity =>
            {
                entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
                entity.Property(x => x.Username).HasMaxLength(100).IsRequired();
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
                entity.HasIndex(x => x.Email).IsUnique();
            });

            modelBuilder.Entity<MediaFile>(entity =>
            {
                entity.Property(x => x.FileName).HasMaxLength(200).IsRequired();
                entity.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
                entity.Property(x => x.Sha1).HasMaxLength(40).IsRequired();
                entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.OriginalUrl).HasMaxLength(2000);
                entity.Property(x => x.SourcePageUrl).HasMaxLength(2000);
                entity.Property(x => x.Title).HasMaxLength(500);
                entity.Property(x => x.Author).HasMaxLength(500);
                entity.Property(x => x.License).HasMaxLength(100);
                entity.Property(x => x.LicenseUrl).HasMaxLength(2000);
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

                // Guids, so unique across all owners
                entity.HasIndex(x => x.FileName).IsUnique();

                // Deleting a user deletes the rows of their uploads (the files themselves have to
                // be removed from their folder separately)
                entity.HasOne<UserData>().WithMany()
                    .HasForeignKey(x => x.OwnerUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Same content = same file (reuse it instead of storing it again), per owner: once
                // among the shared files and once per user. Two indexes, since a unique index
                // treats every null owner as different.
                entity.HasIndex(x => x.Sha1, "unique_shared_sha1")
                    .HasDatabaseName("ix_media_files_sha1_shared")
                    .IsUnique()
                    .HasFilter("\"owner_user_id\" IS NULL");

                entity.HasIndex(x => new { x.OwnerUserId, x.Sha1 }, "unique_owned_sha1")
                    .HasDatabaseName("ix_media_files_owner_user_id_sha1")
                    .IsUnique()
                    .HasFilter("\"owner_user_id\" IS NOT NULL");
            });

            modelBuilder.Entity<Creator>(entity=>
            {
                entity.Property(x => x.Name).HasMaxLength(300).IsRequired();
                entity.Property(x => x.SortName).HasMaxLength(300);
                entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.Country).HasMaxLength(2);
                entity.Property(x => x.Notes).HasMaxLength(4000);
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

                // "This user's creators", searched and sorted by name
                entity.HasIndex(x => new { x.UserId, x.SortName });

                entity.HasOne<UserData>().WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);        // delete user => delete their data

                // Deleting the file just goes back to showing the first image
                entity.HasOne(x => x.PreferredImage).WithMany()
                    .HasForeignKey(x => x.PreferredImageId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<CreatorImage>(entity =>
            {
                entity.HasKey(x => new { x.CreatorId, x.MediaFileId });

                entity.HasOne(x => x.Creator).WithMany(c => c.Images)
                    .HasForeignKey(x => x.CreatorId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Deleting the file just removes it from the creators that used it
                entity.HasOne(x => x.MediaFile).WithMany()
                    .HasForeignKey(x => x.MediaFileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // "Is this file still used?", for cleaning up files nothing refers to
                entity.HasIndex(x => x.MediaFileId);
            });

            modelBuilder.Entity<CreatorExternalId>(entity =>
            {
                // At most one id per service and creator
                entity.HasKey(x => new { x.CreatorId, x.Source });
                entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();

                entity.HasOne(x => x.Creator).WithMany(c => c.ExternalIds)
                    .HasForeignKey(x => x.CreatorId)
                    .OnDelete(DeleteBehavior.Cascade);

                // "Which creators have this MusicBrainz id?" Not unique: creators belong to one user
                // each, so several users' copies of the same artist share its id.
                entity.HasIndex(x => new { x.Source, x.ExternalId });
            });

            modelBuilder.Entity<Release>(entity=>
            {
                entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
                entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.Format).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.Barcode).HasMaxLength(32);
                entity.Property(x => x.Fingerprint).HasMaxLength(64).IsRequired();
                entity.Property(x => x.Notes).HasMaxLength(4000);
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

                entity.HasIndex(x => x.Title);

                // "Does the user already have this release?" Not unique: two different MusicBrainz
                // releases may well have the same fingerprint.
                entity.HasIndex(x => new { x.UserId, x.Fingerprint });

                // "This user's collection", optionally only what they own
                entity.HasIndex(x => new { x.UserId, x.Status });

                entity.HasOne<UserData>().WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);        // delete user => delete their data

                // Releases belong to one user each, so several releases may share a barcode
                entity.HasIndex(x => x.Barcode)
                .HasFilter("\"barcode\" IS NOT NULL");

                // Deleting the file just goes back to showing the first image
                entity.HasOne(x => x.PreferredImage).WithMany()
                    .HasForeignKey(x => x.PreferredImageId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.ToTable(t => t.HasCheckConstraint(
                    "ck_releases_score",
                    $"\"score\" BETWEEN {Release.MinScore} AND {Release.MaxScore}"));
            });

            modelBuilder.Entity<ReleaseImage>(entity =>
            {
                entity.HasKey(x => new { x.ReleaseId, x.MediaFileId });

                entity.HasOne(x => x.Release).WithMany(r => r.Images)
                    .HasForeignKey(x => x.ReleaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Deleting the file just removes it from the releases that used it
                entity.HasOne(x => x.MediaFile).WithMany()
                    .HasForeignKey(x => x.MediaFileId)
                    .OnDelete(DeleteBehavior.Cascade);

                // "Is this file still used?", for cleaning up files nothing refers to
                entity.HasIndex(x => x.MediaFileId);
            });

            modelBuilder.Entity<ReleaseExternalId>(entity =>
            {
                // At most one id per service and release
                entity.HasKey(x => new { x.ReleaseId, x.Source });
                entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.ExternalId).HasMaxLength(100).IsRequired();

                entity.HasOne(x => x.Release).WithMany(r => r.ExternalIds)
                    .HasForeignKey(x => x.ReleaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                // "Which releases have this id?", e.g. all releases in a MusicBrainz release group.
                // Not unique: releases belong to one user each, so several users' copies of the
                // same release share its id.
                entity.HasIndex(x => new { x.Source, x.ExternalId });
            });

            modelBuilder.Entity<ReleaseCreator>(entity =>
            {
                // One person may be both director and actor, hence Role is in the key
                entity.HasKey(x => new { x.ReleaseId, x.CreatorId, x.Role });
                entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.CharacterName).HasMaxLength(200);
                entity.Property(x => x.JoinPhrase).HasMaxLength(50).HasDefaultValue("");

                entity.HasOne(x => x.Release).WithMany(r => r.Credits)
                    .HasForeignKey(x => x.ReleaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Creator).WithMany(c => c.ReleaseCredits)
                    .HasForeignKey(x => x.CreatorId)
                    .OnDelete(DeleteBehavior.Cascade);

                // "Everything this creator has done"
                entity.HasIndex(x => x.CreatorId);
            });

            modelBuilder.Entity<Track>(entity =>
            {
                entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
                entity.HasIndex(x => new { x.ReleaseId, x.DiscNumber, x.TrackNumber }).IsUnique();
                entity.HasOne(x => x.Release).WithMany(r => r.Tracks)
                    .HasForeignKey(x => x.ReleaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}