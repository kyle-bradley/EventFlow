// The MIT License (MIT)
// 
// Copyright (c) 2015-2025 Rasmus Mikkelsen
// https://github.com/eventflow/EventFlow
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of
// this software and associated documentation files (the "Software"), to deal in
// the Software without restriction, including without limitation the rights to
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
// FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
// COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
// IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
// CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace EventFlow.SourceGenerators
{
    [Generator]
    public class AggregateExtensionsSourceGenerator : IIncrementalGenerator
    {
        private const string SourceCodeTemplate =
        """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using EventFlow.Aggregates;
        using EventFlow.Aggregates.ExecutionResults;
        using EventFlow.Core;
        using EventFlow.EventStores;
        using EventFlow.Subscribers;

        namespace {0}
        {{
            /// <summary>
            /// A base class for all events of the <see cref="{1}"/>
            /// </summary>
            public abstract class {1}Event :
                IAggregateEvent<{1}, {1}Id>
            {{
            }}

            /// <summary>
            /// An interface for synchronous subscribers of the <see cref="{1}"/>
            /// </summary>
            /// <typeparam name="TEvent">The type of the event</typeparam>
            public interface ISubscribeSynchronousTo<TEvent> :
                ISubscribeSynchronousTo<{1}, {1}Id, TEvent>
                where TEvent : {1}Event
            {{
            }}

            /// <summary>
            /// An interface for asynchronous subscribers of the <see cref="{1}"/>
            /// </summary>
            /// <typeparam name="TEvent">The type of the event</typeparam>
            public interface ISubscribeAsynchronousTo<TEvent> :
                ISubscribeAsynchronousTo<{1}, {1}Id, TEvent>
                where TEvent : {1}Event
            {{
            }}

            /// <summary>
            /// Provides extension methods for the <see cref="{1}"/>
            /// </summary>
            public static class {1}_IAggregateStoreExtensions
            {{
                /// <summary>
                /// Updates the <see cref="{1}"/>
                /// </summary>
                /// <param name="aggregateStore">Aggregate store</param>
                /// <param name="id">ID of the aggregate</param>
                /// <param name="update">Update function</param>
                /// <param name="cancellationToken">Cancellation token</param>
                /// <returns>A task that returns a collection of domain events that happened during the update</returns>
                public static Task<IReadOnlyCollection<IDomainEvent>> UpdateAsync(
                    this IAggregateStore aggregateStore,
                    {1}Id id,
                    Func<{1}, Task> update,
                    CancellationToken cancellationToken) =>
                        aggregateStore.UpdateAsync<{1}, {1}Id>(
                            id,
                            SourceId.New,
                            (aggregate, _) => update(aggregate),
                            cancellationToken);

                /// <summary>
                /// Loads the <see cref="{1}"/>
                /// </summary>
                /// <param name="aggregateStore">Aggregate store</param>
                /// <param name="id">ID of the aggregate</param>
                /// <param name="cancellationToken">Cancellation token</param>
                /// <returns>A task that returns the <see cref="{1}"/></returns>
                public static Task<{1}> LoadAsync(
                    this IAggregateStore aggregateStore,
                    {1}Id id,
                    CancellationToken cancellationToken) =>
                        aggregateStore.LoadAsync<{1}, {1}Id>(
                            id,
                            cancellationToken);

                /// <summary>
                /// Updates the <see cref="{1}"/>
                /// </summary>
                /// <typeparam name="TExecutionResult">The type of the execution result</typeparam>
                /// <param name="aggregateStore">Aggregate store</param>
                /// <param name="id">ID of the aggregate</param>
                /// <param name="update">Update function</param>
                /// <param name="cancellationToken">Cancellation token</param>
                /// <returns>A task that returns an <see cref="IAggregateUpdateResult{{TExecutionResult}}"/></returns>
                public static Task<IAggregateUpdateResult<TExecutionResult>> UpdateAsync<TExecutionResult>(
                    this IAggregateStore aggregateStore,
                    {1}Id id,
                    Func<{1}, Task<TExecutionResult>> update,
                    CancellationToken cancellationToken)
                        where TExecutionResult : IExecutionResult =>
                        aggregateStore.UpdateAsync<{1}, {1}Id, TExecutionResult>(
                            id,
                            SourceId.New,
                            (aggregate, _) => update(aggregate),
                            cancellationToken);

                /// <summary>
                /// Stores the <see cref="{1}"/>
                /// </summary>
                /// <param name="aggregateStore">Aggregate store</param>
                /// <param name="aggregate">Aggregate to store</param>
                /// <param name="cancellationToken">Cancellation token</param>
                /// <returns>A task that returns a collection of domain events that happened during the update</returns>
                public static Task<IReadOnlyCollection<IDomainEvent>> StoreAsync(
                    this IAggregateStore aggregateStore,
                    {1} aggregate,
                    CancellationToken cancellationToken) =>
                        aggregateStore.StoreAsync<{1}, {1}Id>(
                            aggregate,
                            SourceId.New,
                            cancellationToken);
            }}

            /// <summary>
            /// Provides extension methods for the <see cref="{1}"/>.
            /// </summary>
            public static class {1}_IEventStoreExtensions
            {{
                /// <summary>
                /// Stores uncommitted domain events asynchronously.
                /// </summary>
                /// <param name="eventStore">The event store instance.</param>
                /// <param name="id">The unique identifier of the aggregate.</param>
                /// <param name="uncommittedDomainEvents">The collection of uncommitted domain events.</param>
                /// <param name="cancellationToken">A cancellation token.</param>
                /// <returns>A task containing the stored domain events.</returns>
                public static Task<IReadOnlyCollection<IDomainEvent<{1}, {1}Id>>> StoreAsync(
                    this IEventStore eventStore,
                    {1}Id id,
                    IReadOnlyCollection<IUncommittedEvent> uncommittedDomainEvents,
                    CancellationToken cancellationToken) =>
                        eventStore.StoreAsync<{1}, {1}Id>(
                            id,
                            uncommittedDomainEvents,
                            SourceId.New,
                            cancellationToken);
                
                /// <summary>
                /// Loads all events for a given aggregate asynchronously.
                /// </summary>
                /// <param name="eventStore">The event store instance.</param>
                /// <param name="id">The unique identifier of the aggregate.</param>
                /// <param name="cancellationToken">A cancellation token.</param>
                /// <returns>A task containing the loaded domain events.</returns>
                public static Task<IReadOnlyCollection<IDomainEvent<{1}, {1}Id>>> LoadEventsAsync(
                    this IEventStore eventStore,
                    {1}Id id,
                    CancellationToken cancellationToken) =>
                        eventStore.LoadEventsAsync<{1}, {1}Id>(
                            id,
                            cancellationToken);
                
                /// <summary>
                /// Loads a range of events for a given aggregate asynchronously.
                /// </summary>
                /// <param name="eventStore">The event store instance.</param>
                /// <param name="id">The unique identifier of the aggregate.</param>
                /// <param name="fromSequenceNumber">The starting sequence number.</param>
                /// <param name="toSequenceNumber">The ending sequence number.</param>
                /// <param name="cancellationToken">A cancellation token.</param>
                /// <returns>A task containing the loaded domain events.</returns>
                public static Task<IReadOnlyCollection<IDomainEvent<{1}, {1}Id>>> LoadEventsAsync(
                    this IEventStore eventStore,
                    {1}Id id,
                    int fromSequenceNumber,
                    int toSequenceNumber,
                    CancellationToken cancellationToken) =>
                        eventStore.LoadEventsAsync<{1}, {1}Id>(
                            id,
                            fromSequenceNumber,
                            toSequenceNumber,
                            cancellationToken);

                /// <summary>
                /// Loads events starting from a specific sequence number for a given aggregate asynchronously.
                /// </summary>
                /// <param name="eventStore">The event store instance.</param>
                /// <param name="id">The unique identifier of the aggregate.</param>
                /// <param name="fromEventSequenceNumber">The starting event sequence number.</param>
                /// <param name="cancellationToken">A cancellation token.</param>
                /// <returns>A task containing the loaded domain events.</returns>
                public static Task<IReadOnlyCollection<IDomainEvent<{1}, {1}Id>>> LoadEventsAsync(
                    this IEventStore eventStore,
                    {1}Id id,
                    int fromEventSequenceNumber,
                    CancellationToken cancellationToken) =>
                        eventStore.LoadEventsAsync<{1}, {1}Id>(
                            id,
                            fromEventSequenceNumber,
                            cancellationToken);

                /// <summary>
                /// Deletes an aggregate from the event store asynchronously.
                /// </summary>
                /// <param name="eventStore">The event store instance.</param>
                /// <param name="id">The unique identifier of the aggregate.</param>
                /// <param name="cancellationToken">A cancellation token.</param>
                /// <returns>A task representing the asynchronous operation.</returns>
                public static Task DeleteAggregateAsync<{1}, {1}Id>(
                    this IEventStore eventStore,
                    {1}Id id,
                    CancellationToken cancellationToken) =>
                        eventStore.DeleteAggregateAsync<{1}, {1}Id>(id, cancellationToken);
            }}
        }}
        """;

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var provider = context.SyntaxProvider.CreateSyntaxProvider(
                static (syntaxNode, _) =>
                {
                    // Filter out not classes
                    if (syntaxNode is not ClassDeclarationSyntax classDeclarationSyntax)
                    {
                        return false;
                    }

                    // Filter out classes without at least one attribute
                    if (classDeclarationSyntax.AttributeLists.Count < 1)
                    {
                        return false;
                    }

                    // Check if the class has the target attribute
                    var prefixSpan = AggregateExtensionsAttributeGenerator.Namespace.AsSpan();
                    var postfixSpan = "Attribute".AsSpan();
                    var attributeNameSpan = AggregateExtensionsAttributeGenerator.AttributeName.AsSpan();

                    foreach (var attributeList in classDeclarationSyntax.AttributeLists)
                    {
                        foreach (var attribute in attributeList.Attributes)
                        {
                            var name = attribute.Name.ToString();
                            var nameSpan = name.AsSpan();

                            // Trim the namespace
                            if (nameSpan.StartsWith(prefixSpan))
                            {
                                // +1 for the dot after the namespace
                                nameSpan = nameSpan.Slice(prefixSpan.Length + 1);
                            }

                            // Trim the "Attribute"
                            if (nameSpan.EndsWith(postfixSpan))
                            {
                                nameSpan = nameSpan.Slice(0, nameSpan.Length - postfixSpan.Length);
                            }

                            if (MemoryExtensions.Equals(attributeNameSpan, nameSpan, StringComparison.Ordinal))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                },
                static (syntaxContext, _) => (ClassDeclarationSyntax)syntaxContext.Node);

            context.RegisterSourceOutput(provider, static (ctx, classDeclarationSyntax) =>
            {
                var @namespace = GetNamespace(classDeclarationSyntax);

                var className = classDeclarationSyntax.Identifier.ValueText;

                var source = string.Format(SourceCodeTemplate, @namespace, className);

                ctx.AddSource(
                    $"{className}.g.cs",
                    SourceText.From(source, Encoding.UTF8));
            });
        }

        private static string GetNamespace(ClassDeclarationSyntax classDeclarationSyntax)
        {
            var namespaceDeclaration = classDeclarationSyntax
                .Ancestors()
                .OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault();

            if (namespaceDeclaration is not null)
            {
                return namespaceDeclaration.Name.ToString();
            }

            // Handle case for file-scoped namespaces (C# 10+)
            var fileScopedNamespace = classDeclarationSyntax
                .Ancestors()
                .OfType<FileScopedNamespaceDeclarationSyntax>()
                .First();

            return fileScopedNamespace.Name.ToString();
        }
    }
}
