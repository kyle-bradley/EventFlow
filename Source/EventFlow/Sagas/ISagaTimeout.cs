using EventFlow.Aggregates;
using EventFlow.Aggregates.ExecutionResults;
using EventFlow.Commands;
using System.Threading.Tasks;
using System.Threading;

namespace EventFlow.Sagas
{
    public interface ISagaTimeout : ICommand
    {
        Task ProcessAsync(IDispatchToSagas sagaDispatcher, CancellationToken cancellationToken);
    }

    public interface ISagaTimeout<TSaga, TIdentity>: ISagaTimeout, ICommand<TSaga, TIdentity, SuccessExecutionResult> 
        where TSaga : IAggregateRoot<TIdentity>
        where TIdentity : ISagaId
    {    }
}
