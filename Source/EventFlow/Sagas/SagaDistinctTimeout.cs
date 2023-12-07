using EventFlow.Aggregates;
using EventFlow.Aggregates.ExecutionResults;
using EventFlow.Commands;
using System.Threading;
using System.Threading.Tasks;

namespace EventFlow.Sagas
{
    public abstract class SagaDistinctTimeout<TSaga, TIdentity> : DistinctCommand<TSaga, TIdentity, SuccessExecutionResult>, 
            ISagaTimeout<TSaga, TIdentity>
        where TSaga : IAggregateRoot<TIdentity>
        where TIdentity : ISagaId
    {
        protected SagaDistinctTimeout(TIdentity aggregateId) : base(aggregateId)
        {
        }

        public ISagaId GetSagaId()
        {
            return AggregateId;
        }

        public async Task ProcessAsync(IDispatchToSagas sagaDispatcher, CancellationToken cancellationToken)
        {
            await sagaDispatcher.ProcessAsync(this, cancellationToken).ConfigureAwait(false);
        }
    }
}
