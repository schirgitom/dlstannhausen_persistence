using Microsoft.Extensions.Logging;
using SoWeiT.Optimizer.Persistence.History.Data;

namespace SoWeiT.Optimizer.Persistence.History.Persistence;

public sealed class OptimizerSessionRepository : IOptimizerSessionRepository
{
    private readonly OptimizerHistoryDbContext _dbContext;
    private readonly ILogger<OptimizerSessionRepository> _logger;

    public OptimizerSessionRepository(
        OptimizerHistoryDbContext dbContext,
        ILogger<OptimizerSessionRepository> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public OptimizerSessionEntry? FindById(Guid sessionId)
    {
        var session = _dbContext.Sessions.Find(sessionId);
        _logger.LogDebug("Find session by id {SessionId}: Found={Found}", sessionId, session is not null);
        return session;
    }

    public bool Exists(Guid sessionId)
    {
        var exists = _dbContext.Sessions.Any(x => x.SessionId == sessionId);
        _logger.LogDebug("Check session exists {SessionId}: Exists={Exists}", sessionId, exists);
        return exists;
    }

    public int EndOpenSessions(DateTime endedAtUtc)
    {
        var openSessions = _dbContext.Sessions.Where(x => x.EndedAtUtc == null).ToList();
        foreach (var session in openSessions)
        {
            session.EndedAtUtc = endedAtUtc;
        }

        _logger.LogInformation("Closed open sessions: Count={Count}, EndedAtUtc={EndedAtUtc}", openSessions.Count, endedAtUtc);
        return openSessions.Count;
    }

    public void Add(OptimizerSessionEntry session)
    {
        _logger.LogDebug("Add session entity {SessionId}", session.SessionId);
        _dbContext.Sessions.Add(session);
    }
}
