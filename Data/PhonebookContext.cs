using IsBus.Models;
using Microsoft.EntityFrameworkCore;

namespace IsBus.Data;

public class PhonebookContext : DbContext
{
    public PhonebookContext(DbContextOptions<PhonebookContext> options)
        : base(options)
    {
    }

    public DbSet<Word> Words { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Word>(entity =>
        {
            entity.ToTable("words");
            entity.HasKey(e => e.WordId);
            entity.HasIndex(e => e.WordLower).IsUnique();
            
            entity.Property(e => e.WordId)
                .HasColumnName("word_id")
                .ValueGeneratedOnAdd();
                
            entity.Property(e => e.WordLower)
                .HasColumnName("word_lower")
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(e => e.WordCount)
                .HasColumnName("word_count")
                .HasDefaultValue(1);
        });
    }
}