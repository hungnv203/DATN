using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieBooking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPasswordResetAndEmailUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoles",
                table: "UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MovieGenres",
                table: "MovieGenres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoyaltyPoints",
                table: "LoyaltyPoints");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BookingPromotions",
                table: "BookingPromotions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PasswordResetExpires",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PasswordResetToken",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "UserRoles",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "UserRoles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "UserRoles",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "MovieGenres",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "MovieGenres",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "MovieGenres",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "LoyaltyPoints",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "LoyaltyPoints",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "LoyaltyPoints",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "BookingPromotions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "BookingPromotions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "BookingPromotions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoles",
                table: "UserRoles",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_MovieGenres",
                table: "MovieGenres",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoyaltyPoints",
                table: "LoyaltyPoints",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BookingPromotions",
                table: "BookingPromotions",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovieGenres_MovieId_GenreId",
                table: "MovieGenres",
                columns: new[] { "MovieId", "GenreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPoints_UserId",
                table: "LoyaltyPoints",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BookingPromotions_BookingId_PromotionId",
                table: "BookingPromotions",
                columns: new[] { "BookingId", "PromotionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoles",
                table: "UserRoles");

            migrationBuilder.DropIndex(
                name: "IX_UserRoles_UserId_RoleId",
                table: "UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_MovieGenres",
                table: "MovieGenres");

            migrationBuilder.DropIndex(
                name: "IX_MovieGenres_MovieId_GenreId",
                table: "MovieGenres");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LoyaltyPoints",
                table: "LoyaltyPoints");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyPoints_UserId",
                table: "LoyaltyPoints");

            migrationBuilder.DropPrimaryKey(
                name: "PK_BookingPromotions",
                table: "BookingPromotions");

            migrationBuilder.DropIndex(
                name: "IX_BookingPromotions_BookingId_PromotionId",
                table: "BookingPromotions");

            migrationBuilder.DropColumn(
                name: "PasswordResetExpires",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordResetToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "UserRoles");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "MovieGenres");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MovieGenres");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MovieGenres");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "LoyaltyPoints");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "LoyaltyPoints");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "LoyaltyPoints");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "BookingPromotions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "BookingPromotions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "BookingPromotions");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoles",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_MovieGenres",
                table: "MovieGenres",
                columns: new[] { "MovieId", "GenreId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_LoyaltyPoints",
                table: "LoyaltyPoints",
                column: "UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BookingPromotions",
                table: "BookingPromotions",
                columns: new[] { "BookingId", "PromotionId" });
        }
    }
}
