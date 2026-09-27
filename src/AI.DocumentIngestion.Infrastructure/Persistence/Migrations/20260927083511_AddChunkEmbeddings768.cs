using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace AI.DocumentIngestion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkEmbeddings768 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Chunks created before embeddings were implemented cannot be made searchable.
            // Failed/Ready documents can be queued for reprocessing to recreate them atomically.
            migrationBuilder.Sql("DELETE FROM document_chunks;");

            migrationBuilder.AddColumn<Vector>(
                name: "embedding",
                table: "document_chunks",
                type: "vector(768)",
                nullable: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "embedding",
                table: "document_chunks");
        }
    }
}
