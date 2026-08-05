using SoWeiT.Optimizer.Persistence.History.Data;

namespace SoWeiT.Optimizer.Persistence.History.Persistence;

public interface IOptimizerSessionRepository
{
    OptimizerSessionEntry? FindById(Guid sessionId);

    bool Exists(Guid sessionId);

    int EndOpenSessions(DateTime endedAtUtc);

    void Add(OptimizerSessionEntry session);
}
