using Microsoft.EntityFrameworkCore;

namespace CdArchiveBackend.Data
{
    internal sealed class UserContext(DbContextOptions<UserContext> contextOptions) : DbContext(contextOptions)
    {
        public required DbSet<UserData> Users { get; init; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserData>(entity =>
            {
                entity.ToTable("users");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Id).HasColumnName("id");
                entity.Property(x => x.Username).HasColumnName("username");
                entity.Property(x => x.Email).HasColumnName("email");
                entity.Property(x => x.PasswordHash).HasColumnName("password_hash");
                entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            });
        }
    }
}