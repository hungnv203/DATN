using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieBooking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewsLoyaltyDiscountCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BalanceAfter",
                table: "PointTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "BookingId",
                table: "PointTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "PointTransactions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Bookings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PointDiscountAmount",
                table: "Bookings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Subtotal",
                table: "Bookings",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "UsedPoints",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MovieReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MovieId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovieReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovieReviews_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MovieReviews_Movies_MovieId",
                        column: x => x.MovieId,
                        principalTable: "Movies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovieReviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_BookingId",
                table: "PointTransactions",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieReviews_BookingId",
                table: "MovieReviews",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_MovieReviews_MovieId_UserId",
                table: "MovieReviews",
                columns: new[] { "MovieId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovieReviews_UserId",
                table: "MovieReviews",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PointTransactions_Bookings_BookingId",
                table: "PointTransactions",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PointTransactions_Bookings_BookingId",
                table: "PointTransactions");

            migrationBuilder.DropTable(
                name: "MovieReviews");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_BookingId",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "BalanceAfter",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "PointDiscountAmount",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Subtotal",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "UsedPoints",
                table: "Bookings");
        }
    }
}
