using Microsoft.EntityFrameworkCore;
using Npgsql;
using PIPDC.Application.Data;

namespace PIPDC.Infrastructure.Data;

/// <summary>
/// Npgsql implementation of <see cref="IUniqueViolationDetector"/>.
///
/// A unique-index race (or an optimistic-concurrency miss on an indexed column)
/// surfaces as a PostgresException carrying SQLSTATE 23505, wrapped by EF Core
/// in a DbUpdateException. This is byte-for-byte the predicate TransactionService
/// used before the project split - only its owning layer changed.
/// </summary>
public sealed class PostgresUniqueViolationDetector : IUniqueViolationDetector
{
    public bool IsUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
