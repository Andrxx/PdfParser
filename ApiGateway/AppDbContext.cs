using Microsoft.EntityFrameworkCore;
using CommonModels.Models;

namespace ApiGateway
{
    public class AppDbContext : DbContext
    {
        // Конструктор принимает настройки подключения из Program.cs
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<Document> Documents { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Document>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.FileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.Status)
                    .IsRequired()
                    .HasMaxLength(50);

                entity.Property(e => e.ExtractedText)
                    .HasColumnType("text");
            });
        }
    }
}
