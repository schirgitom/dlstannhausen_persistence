using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SoWeiT.Optimizer.Persistence.History.Data;
using SoWeiT.Optimizer.Persistence.History.Persistence;

namespace SoWeiT.Optimizer.Tests;

public sealed class EfCoreOptimizerHistoryStoreTests
{
    [Fact]
    public void CreateSession_ClosesAllPreviouslyOpenSessions()
    {
        var sessionRepo = new FakeSessionRepository
        {
            Sessions =
            {
                [Guid.Parse("11111111-1111-1111-1111-111111111111")] = new OptimizerSessionEntry
                {
                    SessionId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    CreatedAtUtc = new DateTime(2026, 8, 5, 5, 0, 0, DateTimeKind.Utc),
                    EndedAtUtc = null
                },
                [Guid.Parse("22222222-2222-2222-2222-222222222222")] = new OptimizerSessionEntry
                {
                    SessionId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    CreatedAtUtc = new DateTime(2026, 8, 5, 5, 5, 0, DateTimeKind.Utc),
                    EndedAtUtc = null
                },
                [Guid.Parse("33333333-3333-3333-3333-333333333333")] = new OptimizerSessionEntry
                {
                    SessionId = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    CreatedAtUtc = new DateTime(2026, 8, 5, 4, 55, 0, DateTimeKind.Utc),
                    EndedAtUtc = new DateTime(2026, 8, 5, 5, 10, 0, DateTimeKind.Utc)
                }
            }
        };

        var store = new EfCoreOptimizerHistoryStore(
            new FakeUnitOfWorkFactory(sessionRepo),
            NullLogger<EfCoreOptimizerHistoryStore>.Instance);

        var createdAtUtc = new DateTime(2026, 8, 5, 6, 0, 0, DateTimeKind.Utc);
        var newSessionId = Guid.Parse("44444444-4444-4444-4444-444444444444");

        store.CreateSession(
            newSessionId,
            new OptimizerSessionConfig(7, 60, 120, useOrTools: true, useGreedyFallback: false),
            createdAtUtc);

        Assert.Equal(createdAtUtc, sessionRepo.Sessions[Guid.Parse("11111111-1111-1111-1111-111111111111")].EndedAtUtc);
        Assert.Equal(createdAtUtc, sessionRepo.Sessions[Guid.Parse("22222222-2222-2222-2222-222222222222")].EndedAtUtc);
        Assert.Equal(new DateTime(2026, 8, 5, 5, 10, 0, DateTimeKind.Utc), sessionRepo.Sessions[Guid.Parse("33333333-3333-3333-3333-333333333333")].EndedAtUtc);
        Assert.True(sessionRepo.Sessions.ContainsKey(newSessionId));
        Assert.Null(sessionRepo.Sessions[newSessionId].EndedAtUtc);
    }

    private sealed class FakeUnitOfWorkFactory(FakeSessionRepository sessionRepository) : IOptimizerUnitOfWorkFactory
    {
        public IOptimizerUnitOfWork Create()
        {
            return new FakeUnitOfWork(sessionRepository);
        }
    }

    private sealed class FakeUnitOfWork(FakeSessionRepository sessionRepository) : IOptimizerUnitOfWork
    {
        public OptimizerHistoryDbContext DbContext { get; } =
            new(new DbContextOptionsBuilder<OptimizerHistoryDbContext>().Options);

        public IOptimizerSessionRepository Sessions { get; } = sessionRepository;

        public IOptimizerRequestRepository Requests { get; } = new FakeRequestRepository();

        public void SaveChanges()
        {
        }

        public void Dispose()
        {
            DbContext.Dispose();
        }
    }

    private sealed class FakeRequestRepository : IOptimizerRequestRepository
    {
        public void Add(OptimizerRequestEntry request) => throw new NotSupportedException();
    }

    private sealed class FakeSessionRepository : IOptimizerSessionRepository
    {
        public Dictionary<Guid, OptimizerSessionEntry> Sessions { get; } = new();

        public OptimizerSessionEntry? FindById(Guid sessionId)
        {
            return Sessions.TryGetValue(sessionId, out var session) ? session : null;
        }

        public bool Exists(Guid sessionId)
        {
            return Sessions.ContainsKey(sessionId);
        }

        public int EndOpenSessions(DateTime endedAtUtc)
        {
            var count = 0;
            foreach (var session in Sessions.Values.Where(x => x.EndedAtUtc is null))
            {
                session.EndedAtUtc = endedAtUtc;
                count++;
            }

            return count;
        }

        public void Add(OptimizerSessionEntry session)
        {
            Sessions[session.SessionId] = session;
        }
    }
}
