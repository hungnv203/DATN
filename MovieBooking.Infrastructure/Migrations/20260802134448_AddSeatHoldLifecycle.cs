using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MovieBooking.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeatHoldLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX IF EXISTS "IX_SeatHolds_ShowtimeId";

                ALTER TABLE "SeatHolds"
                    ADD COLUMN IF NOT EXISTS "BookingId" uuid,
                    ADD COLUMN IF NOT EXISTS "CompletedAt" timestamp without time zone,
                    ADD COLUMN IF NOT EXISTS "HoldGroupId" uuid,
                    ADD COLUMN IF NOT EXISTS "ReleasedAt" timestamp without time zone,
                    ADD COLUMN IF NOT EXISTS "Status" character varying(32);

                ALTER TABLE "Bookings"
                    ADD COLUMN IF NOT EXISTS "SeatHoldGroupId" uuid;

                UPDATE "SeatHolds"
                SET "HoldGroupId" = CASE
                        WHEN "HoldGroupId" IS NULL
                             OR "HoldGroupId" = '00000000-0000-0000-0000-000000000000'
                        THEN "Id"
                        ELSE "HoldGroupId"
                    END,
                    "Status" = CASE
                        WHEN "Status" IS NULL OR "Status" = '' THEN 'Active'
                        ELSE "Status"
                    END;

                WITH ranked_holds AS
                (
                    SELECT "Id",
                           ROW_NUMBER() OVER
                           (
                               PARTITION BY "ShowtimeId", "SeatId"
                               ORDER BY "ExpiredAt" DESC, "CreatedAt" DESC, "Id"
                           ) AS row_number
                    FROM "SeatHolds"
                    WHERE "Status" = 'Active'
                )
                UPDATE "SeatHolds" AS hold
                SET "Status" = 'Expired'
                FROM ranked_holds
                WHERE hold."Id" = ranked_holds."Id"
                  AND ranked_holds.row_number > 1;

                ALTER TABLE "SeatHolds"
                    ALTER COLUMN "HoldGroupId" SET NOT NULL,
                    ALTER COLUMN "Status" SET DEFAULT 'Active',
                    ALTER COLUMN "Status" SET NOT NULL;

                CREATE INDEX IF NOT EXISTS "IX_SeatHolds_HoldGroupId_UserId"
                    ON "SeatHolds" ("HoldGroupId", "UserId");

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_SeatHolds_ShowtimeId_SeatId"
                    ON "SeatHolds" ("ShowtimeId", "SeatId")
                    WHERE "Status" = 'Active';

                CREATE INDEX IF NOT EXISTS "IX_SeatHolds_Status_ExpiredAt"
                    ON "SeatHolds" ("Status", "ExpiredAt");

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_Bookings_SeatHoldGroupId"
                    ON "Bookings" ("SeatHoldGroupId")
                    WHERE "SeatHoldGroupId" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SeatHolds_HoldGroupId_UserId",
                table: "SeatHolds");

            migrationBuilder.DropIndex(
                name: "IX_SeatHolds_ShowtimeId_SeatId",
                table: "SeatHolds");

            migrationBuilder.DropIndex(
                name: "IX_SeatHolds_Status_ExpiredAt",
                table: "SeatHolds");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_SeatHoldGroupId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "BookingId",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "HoldGroupId",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "ReleasedAt",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "SeatHolds");

            migrationBuilder.DropColumn(
                name: "SeatHoldGroupId",
                table: "Bookings");

            migrationBuilder.CreateIndex(
                name: "IX_SeatHolds_ShowtimeId",
                table: "SeatHolds",
                column: "ShowtimeId");
        }
    }
}
