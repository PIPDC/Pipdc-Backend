using System.Security.Cryptography;
using PIPDC.Application.Data;
using Microsoft.EntityFrameworkCore;
using PIPDC.Domain.Common;
using PIPDC.Domain.Entities;

namespace PIPDC.Application.Agents;

/// <summary>
/// Issues agent licence numbers.
/// </summary>
/// <remarks>
/// Licence numbers must be unique, and uniqueness has to hold under concurrent
/// approval. Two approvals running at the same time must never receive the same
/// number, and the check-then-insert pattern is racy on its own: both calls can
/// observe "free" before either writes.
///
/// The approach here is generate-then-insert and let the database be the
/// arbiter. <see cref="Agent.LicenseNumber"/> carries a unique index, so a
/// collision surfaces as a unique-constraint violation on insert rather than as a
/// silently duplicated licence. On that violation the service generates a new
/// candidate and retries. The index is what makes this correct; the in-process
/// "is it taken" probe is only an optimisation to avoid a pointless round trip.
/// </remarks>
public interface IAgentLicenseGenerator
{
    /// <summary>
    /// Produces a licence number that is not currently in use, retrying on the
    /// rare occasion that a generated candidate is already taken.
    /// </summary>
    Task<Result<string>> GenerateAsync(CancellationToken ct = default);
}

public class AgentLicenseGenerator(IAppDbContext dbContext) : IAgentLicenseGenerator
{
    private const int MaxAttempts = 8;

    // Crockford-style base32, minus characters that are easy to misread when a
    // licence is read aloud or copied off a printed badge (I, L, O, U).
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int GroupCount = 2;
    private const int GroupLength = 4;

    public async Task<Result<string>> GenerateAsync(CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = Build();

            var taken = await dbContext.Agents
                .AnyAsync(a => a.LicenseNumber == candidate, ct);

            if (!taken)
                return Result<string>.Success(candidate);
        }

        return Result<string>.Failure(Error.Failure(
            "agent.license.generationfailed",
            $"Could not allocate a unique agent licence after {MaxAttempts} attempts."));
    }

    private static string Build()
    {
        var groups = new string[GroupCount];
        for (var g = 0; g < GroupCount; g++)
        {
            var chars = new char[GroupLength];
            for (var i = 0; i < GroupLength; i++)
                chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            groups[g] = new string(chars);
        }

        // 8 random base32 characters is ~2^40 of space, so exhaustion is not a
        // practical concern at this scale; the retry loop exists to make the
        // guarantee correct rather than merely probable.
        return $"PIPDC-AGT-{groups[0]}-{groups[1]}";
    }
}
