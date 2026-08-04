using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieBooking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShowtimeSeatRealtimeVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ShowtimeSeatVersions",
                columns: table => new
                {
                    ShowtimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShowtimeSeatVersions", x => x.ShowtimeId);
                    table.ForeignKey(
                        name: "FK_ShowtimeSeatVersions_Showtimes_ShowtimeId",
                        column: x => x.ShowtimeId,
                        principalTable: "Showtimes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                """
                INSERT INTO "ShowtimeSeatVersions" ("ShowtimeId", "Version")
                SELECT "Id", 0
                FROM "Showtimes"
                ON CONFLICT ("ShowtimeId") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShowtimeSeatVersions");
        }
    }
}
