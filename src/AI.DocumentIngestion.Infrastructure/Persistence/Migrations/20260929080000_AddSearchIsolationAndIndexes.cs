using AI.DocumentIngestion.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AI.DocumentIngestion.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DocumentIngestionDbContext))]
[Migration("20260929080000_AddSearchIsolationAndIndexes")]
public sealed class AddSearchIsolationAndIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE documents ADD COLUMN IF NOT EXISTS tenant_id character varying(128) NOT NULL DEFAULT '__legacy_unassigned__';
            ALTER TABLE documents ADD COLUMN IF NOT EXISTS owner_id character varying(128) NOT NULL DEFAULT '__legacy_unassigned__';
            ALTER TABLE documents ADD COLUMN IF NOT EXISTS metadata jsonb NOT NULL DEFAULT '{}'::jsonb;
            """);

        migrationBuilder.Sql("""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_documents_tenant_id_owner_id_status_created_at
            ON documents (tenant_id, owner_id, status, created_at);
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_document_chunks_embedding_hnsw_cosine
            ON document_chunks USING hnsw (embedding vector_cosine_ops)
            WITH (m = 16, ef_construction = 64);
            """, suppressTransaction: true);
        migrationBuilder.Sql("""
            CREATE INDEX CONCURRENTLY IF NOT EXISTS ix_document_chunks_text_fts_simple
            ON document_chunks USING gin (to_tsvector('simple', text));
            """, suppressTransaction: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS ix_document_chunks_text_fts_simple;", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS ix_document_chunks_embedding_hnsw_cosine;", suppressTransaction: true);
        migrationBuilder.Sql("DROP INDEX CONCURRENTLY IF EXISTS ix_documents_tenant_id_owner_id_status_created_at;", suppressTransaction: true);
        migrationBuilder.Sql("""
            ALTER TABLE documents DROP COLUMN IF EXISTS metadata;
            ALTER TABLE documents DROP COLUMN IF EXISTS owner_id;
            ALTER TABLE documents DROP COLUMN IF EXISTS tenant_id;
            """);
    }
}
