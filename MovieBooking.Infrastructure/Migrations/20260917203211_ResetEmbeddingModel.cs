using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieBooking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ResetEmbeddingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear stale embeddings produced by text-embedding-004 (768-dim).
            // EmbeddingSyncService will re-embed everything with gemini-embedding-001 (3072-dim)
            // on the next application startup.
            migrationBuilder.Sql("TRUNCATE TABLE \"MovieEmbeddings\";");
            migrationBuilder.Sql("TRUNCATE TABLE \"KnowledgeDocuments\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data cannot be restored automatically; a manual re-sync is required.
        }
    }
}
