using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieBooking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStatefulSeatHolds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeatHolds_ShowtimeId",
                table: "SeatHolds");

            migrationBuilder.AddColumn<Guid>(
                name: "BookingId",
                table: "SeatHolds",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "SeatHolds",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleasedAt",
                table: "SeatHolds",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionId",
                table: "SeatHolds",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "SeatHolds",
                type: "text",
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.Sql(
                """
                UPDATE "SeatHolds"
                SET "SessionId" = "Id",
                    "Status" = CASE
                        WHEN "ExpiredAt" <= (NOW() AT TIME ZONE 'UTC') THEN 'Expired'
                        ELSE 'Active'
                    END;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_SeatHolds_BookingId",
                table: "SeatHolds",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_SeatHolds_SessionId_Status",
                table: "SeatHolds",
                columns: new[] { "SessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SeatHolds_ShowtimeId_SeatId_Status_ExpiredAt",
                table: "SeatHolds",
                columns: new[] { "ShowtimeId", "SeatId", "Status", "ExpiredAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_SeatHolds_Bookings_BookingId",
                table: "SeatHolds",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SeatHolds_Bookings_BookingId",
                table: "SeatHolds");

            migrationBuilder.DropIndex(
                name: "IX_SeatHolds_BookingId",
                table: "SeatHolds");

            migrationBuilder.DropIndex(
                name: "IX_SeatHolds_SessionId_Status",
                table: "SeatHolds");

            migrationBuilder.DropIndex(
                name: "IX_SeatHolds_ShowtimeId_SeatId_Status_ExpiredAt",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "SessionId",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SeatHolds");

            migrationBuilder.CreateIndex(
                name: "IX_SeatHolds_ShowtimeId",
                table: "SeatHolds",
                column: "ShowtimeId");
        }
    }
}
