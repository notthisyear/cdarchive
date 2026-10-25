using CdArchiveBackend.Data.Database;
using Microsoft.EntityFrameworkCore;

namespace CdArchiveBackend
{
    internal sealed class DatabaseContext(DbContextOptions<DatabaseContext> contextOptions) : DbContext(contextOptions)
    {
        public DbSet<Creator> Creators => Set<Creator>();

        public DbSet<Release> Releases => Set<Release>();

        public DbSet<ReleaseCreator> ReleaseCreators => Set<ReleaseCreator>();

        public DbSet<Track> Tracks => Set<Track>();

        public DbSet<UserData> Users => Set<UserData>();

        public DbSet<CollectionItem> CollectionItems => Set<CollectionItem>();

        public DbSet<UserCreatorProfile> UserCreatorProfiles => Set<UserCreatorProfile>();

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
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

                entity.HasIndex(x => x.Name);

                // Deleting the image just leaves the creator without one
                entity.HasOne(x => x.DefaultImage).WithMany()
                    .HasForeignKey(x => x.DefaultImageId)
                    .OnDelete(DeleteBehavior.SetNull);
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

                // "Which creator has this MusicBrainz id?", and no two creators with the same one
                entity.HasIndex(x => new { x.Source, x.ExternalId }).IsUnique();
            });

            modelBuilder.Entity<Release>(entity=>
            {
                entity.Property(x => x.Title).HasMaxLength(500).IsRequired();
                entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.Format).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.Barcode).HasMaxLength(32);
                entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

                entity.HasIndex(x => x.Title);

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
        
                // Same barcode = same release instance (reuse it instead of duplicating)
                entity.HasIndex(x => x.Barcode)
                .IsUnique()
                .HasFilter("\"barcode\" IS NOT NULL");

                // Deleting the image just leaves the release without one
                entity.HasOne(x => x.DefaultImage).WithMany()
                    .HasForeignKey(x => x.DefaultImageId)
                    .OnDelete(DeleteBehavior.SetNull);
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

                // "Which releases have this id?", e.g. all releases in a MusicBrainz release group
                // Both indexes are on the same columns, so they're named explicitly (the naming
                // convention would otherwise give one of them a "1" suffix)
                entity.HasIndex(x => new { x.Source, x.ExternalId })
                    .HasDatabaseName("ix_release_external_ids_source_external_id");

                // A release's own id belongs to that release only. A release group is shared by
                // its releases (the CD, the LP...), so it's left out.
                entity.HasIndex(x => new { x.Source, x.ExternalId }, "unique_release_id")
                    .HasDatabaseName("ix_release_external_ids_source_external_id_unique")
                    .IsUnique()
                    .HasFilter($"\"source\" <> '{nameof(ExternalSource.MusicBrainzReleaseGroup)}'");
            });

            modelBuilder.Entity<ReleaseCreator>(entity =>
            {
                // One person may be both director and actor, hence Role is in the key
                entity.HasKey(x => new { x.ReleaseId, x.CreatorId, x.Role });
                entity.Property(x => x.Role).HasConversion<string>().HasMaxLength(32);
                entity.Property(x => x.CharacterName).HasMaxLength(200);

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


            modelBuilder.Entity<CollectionItem>(entity =>
            {
                entity.Property(x => x.Notes).HasMaxLength(4000);
                entity.Property(x => x.AddedAt).HasDefaultValueSql("now()");

                entity.HasIndex(x => new { x.UserId, x.ReleaseId }).IsUnique();

                entity.HasOne<UserData>().WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);        // delete user => delete their data

                // Don't allow deleting a release somebody still owns
                entity.HasOne(x => x.Release).WithMany()
                    .HasForeignKey(x => x.ReleaseId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.CustomImage).WithMany()
                    .HasForeignKey(x => x.CustomImageId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<UserCreatorProfile>(entity =>
            {
                entity.Property(x => x.DisplayNameOverride).HasMaxLength(300);
                entity.Property(x => x.Notes).HasMaxLength(4000);

                entity.HasIndex(x => new { x.UserId, x.CreatorId }).IsUnique();

                entity.HasOne<UserData>().WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.Creator).WithMany()
                    .HasForeignKey(x => x.CreatorId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(x => x.CustomImage).WithMany()
                    .HasForeignKey(x => x.CustomImageId)
                    .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}