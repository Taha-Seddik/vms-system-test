using Microsoft.EntityFrameworkCore;
using Vms.Api.Data;

namespace Vms.Api.Services;

public sealed class SessionValidationService(VmsDbContext database)
{
    public async Task<bool> ValidateAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await database.UserSessions
            .Include(item => item.User)
            .SingleOrDefaultAsync(
                item => item.Id == sessionId && item.UserId == userId,
                cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (session is null ||
            session.RevokedAt is not null ||
            session.ExpiresAt <= now ||
            !session.User.IsEnabled)
        {
            return false;
        }

        if (session.LastActivityAt <= now.AddSeconds(-30))
        {
            session.LastActivityAt = now;
            session.User.LastActivityAt = now;
            await database.SaveChangesAsync(cancellationToken);
        }

        return true;
    }
}
