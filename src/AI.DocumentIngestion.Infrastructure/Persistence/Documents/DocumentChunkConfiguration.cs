using AI.DocumentIngestion.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AI.DocumentIngestion.Infrastructure.Persistence.Documents;

internal sealed class DocumentChunkConfiguration : IEntityTypeConfiguration<DocumentChunk>
{
    public void Configure(EntityTypeBuilder<DocumentChunk> builder)
    {
        builder.ToTable("document_chunks", table =>
        {
            table.HasCheckConstraint("ck_document_chunks_sequence", "sequence >= 0");
            table.HasCheckConstraint("ck_document_chunks_page_number", "page_number IS NULL OR page_number > 0");
            table.HasCheckConstraint("ck_document_chunks_token_count", "token_count > 0");
        });
        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.Id).HasColumnName("id");
        builder.Property(chunk => chunk.DocumentId).HasColumnName("document_id");
        builder.Property(chunk => chunk.Sequence).HasColumnName("sequence");
        builder.Property(chunk => chunk.Text).HasColumnName("text").IsRequired();
        builder.Property(chunk => chunk.PageNumber).HasColumnName("page_number");
        builder.Property(chunk => chunk.TokenCount).HasColumnName("token_count");

        builder.HasIndex(chunk => new { chunk.DocumentId, chunk.Sequence }).IsUnique();
        builder.HasOne<Document>()
            .WithMany()
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
