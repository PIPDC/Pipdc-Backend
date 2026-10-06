using Microsoft.EntityFrameworkCore;

namespace PIPDC.Application.Data;

/// <summary>
/// Recognises a unique-constraint violation on a failed save without naming the
/// database provider.
///
/// Application must not reference Npgsql, so the SQLSTATE test itself lives in
/// Infrastructure (PostgresUniqueViolationDetector) and is reached through this
/// seam. Keeps PIPDC.Application compilable against the project-reference rules
/// while leaving the classification logic untouched.
/// </summary>
public interface IUniqueViolationDetector
{
    bool IsUniqueViolation(DbUpdateException exception);
}
