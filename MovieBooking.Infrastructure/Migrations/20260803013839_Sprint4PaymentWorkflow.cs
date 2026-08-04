using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieBooking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4PaymentWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_BookingId",
                table: "PointTransactions");

            migrationBuilder.AddColumn<string>(
                name: "EffectType",
                table: "PointTransactions",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "Bookings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "CustomerOnline");

            migrationBuilder.CreateTable(
                name: "PaymentOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientIdempotencyKey = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderEventKey = table.Column<string>(type: "character(64)", fixedLength: true, maxLength: 64, nullable: true),
                    OperationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "text", nullable: false),
                    Result = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentOperations", x => x.Id);
                    table.CheckConstraint("CK_PaymentOperations_IdempotencyDomain", "(\"ClientIdempotencyKey\" IS NOT NULL AND \"ProviderEventKey\" IS NULL) OR (\"ClientIdempotencyKey\" IS NULL AND \"ProviderEventKey\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_PaymentOperations_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentOperations_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_BookingId_EffectType",
                table: "PointTransactions",
                columns: new[] { "BookingId", "EffectType" },
                unique: true,
                filter: "\"BookingId\" IS NOT NULL AND \"EffectType\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Method_TransactionCode",
                table: "Payments",
                columns: new[] { "Method", "TransactionCode" },
                unique: true,
                filter: "\"TransactionCode\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOperations_BookingId",
                table: "PaymentOperations",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOperations_ClientIdempotencyKey",
                table: "PaymentOperations",
                column: "ClientIdempotencyKey",
                unique: true,
                filter: "\"ClientIdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOperations_PaymentId",
                table: "PaymentOperations",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOperations_ProviderEventKey",
                table: "PaymentOperations",
                column: "ProviderEventKey",
                unique: true,
                filter: "\"ProviderEventKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentOperations");

            migrationBuilder.DropIndex(
                name: "IX_PointTransactions_BookingId_EffectType",
                table: "PointTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_Method_TransactionCode",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "EffectType",
                table: "PointTransactions");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_PointTransactions_BookingId",
                table: "PointTransactions",
                column: "BookingId");
        }
    }
}
