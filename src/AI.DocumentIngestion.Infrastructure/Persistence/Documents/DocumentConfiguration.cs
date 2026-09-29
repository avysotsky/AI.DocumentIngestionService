using AI.DocumentIngestion.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI.DocumentIngestion.Infrastructure.Persistence.Documents;

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents", table =>
            table.HasCheckConstraint("ck_documents_size_positive", "size > 0"));
        builder.HasKey(document => document.Id);

        builder.Property(document => document.Id).HasColumnName("id");
        builder.Property(document => document.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(document => document.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100)
            .IsRequired();
        builder.Property(document => document.Size).HasColumnName("size");
        builder.Property(document => document.Sha256Hash)
            .HasColumnName("sha256_hash")
            .HasMaxLength(64)
            .IsFixedLength()
            .IsRequired();
        builder.Property(document => document.StorageKey)
            .HasColumnName("storage_key")
            .HasMaxLength(512)
            .IsRequired();
        builder.Property(document => document.TenantId)
            .HasColumnName("tenant_id")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(document => document.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(document => document.MetadataJson)
            .HasColumnName("metadata")
            .HasColumnType("jsonb")
            .IsRequired();
        builder.Property(document => document.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(document => document.CreatedAt).HasColumnName("created_at");
        builder.Property(document => document.ProcessingStartedAt).HasColumnName("processing_started_at");
        builder.Property(document => document.ProcessedAt).HasColumnName("processed_at");
        builder.Property(document => document.ProcessingError).HasColumnName("processing_error");
        builder.Property(document => document.ProcessingAttempt).HasColumnName("processing_attempt");

        builder.HasIndex(document => document.StorageKey).IsUnique();
        builder.HasIndex(document => document.Sha256Hash);
        builder.HasIndex(document => new { document.Status, document.CreatedAt });
        builder.HasIndex(document => new { document.TenantId, document.OwnerId, document.Status, document.CreatedAt });
    }
}
