using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MovieBooking.Infrastructure.Persistence;

namespace MovieBooking.Infrastructure.Services;

internal static class ShowtimeSeatVersionStore
{
    public static async Task<long> IncrementAsync(
        AppDbContext dbContext,
        Guid showtimeId,
        CancellationToken cancellationToken)
    {
        var transaction = dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A database transaction is required to increment seat state version.");
        var connection = dbContext.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText =
            """
            INSERT INTO "ShowtimeSeatVersions" ("ShowtimeId", "Version")
            VALUES (@showtimeId, 1)
            ON CONFLICT ("ShowtimeId")
            DO UPDATE SET "Version" = "ShowtimeSeatVersions"."Version" + 1
            RETURNING "Version";
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "showtimeId";
        parameter.Value = showtimeId;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }
}
