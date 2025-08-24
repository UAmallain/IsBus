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
    public DbSet<Name> Names { get; set; }
    public DbSet<WordData> WordData { get; set; }
    public DbSet<Community> Communities { get; set; }
    public DbSet<StreetName> StreetNames { get; set; }
    public DbSet<RoadNetwork> RoadNetworks { get; set; }
    public DbSet<ProvinceMapping> ProvinceMappings { get; set; }
    public DbSet<StreetTypeMapping> StreetTypeMappings { get; set; }

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
                
            entity.Property(e => e.LastSeen)
                .HasColumnName("last_seen")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
                
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
                
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<Name>(entity =>
        {
            entity.ToTable("names");
            entity.HasKey(e => e.NameId);
            entity.HasIndex(e => new { e.NameLower, e.NameType }).IsUnique();
            
            entity.Property(e => e.NameId)
                .HasColumnName("name_id")
                .ValueGeneratedOnAdd();
                
            entity.Property(e => e.NameLower)
                .HasColumnName("name_lower")
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(e => e.NameType)
                .HasColumnName("name_type")
                .IsRequired()
                .HasMaxLength(10)
                .HasDefaultValue("both");
                
            entity.Property(e => e.NameCount)
                .HasColumnName("name_count")
                .HasDefaultValue(1);
                
            entity.Property(e => e.LastSeen)
                .HasColumnName("last_seen")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
                
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
                
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
        });

        modelBuilder.Entity<WordData>(entity =>
        {
            entity.ToTable("word_data");
            entity.HasKey(e => e.WordId);
            entity.HasIndex(e => new { e.WordLower, e.WordType }).IsUnique();
            entity.HasIndex(e => e.WordLower);
            entity.HasIndex(e => e.WordType);
            
            entity.Property(e => e.WordId)
                .HasColumnName("word_id")
                .ValueGeneratedOnAdd();
                
            entity.Property(e => e.WordLower)
                .HasColumnName("word_lower")
                .IsRequired()
                .HasMaxLength(255);
                
            entity.Property(e => e.WordType)
                .HasColumnName("word_type")
                .IsRequired()
                .HasMaxLength(20);
                
            entity.Property(e => e.WordCount)
                .HasColumnName("word_count")
                .HasDefaultValue(1);
                
            entity.Property(e => e.LastSeen)
                .HasColumnName("last_seen")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
                
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
                
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at")
                .HasDefaultValueSql("CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP");
        });
    }
}