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

using System;
using EventFlow.Aggregates;
using EventFlow.Core;
using EventFlow.Extensions;
using EventFlow.ReadStores;
using EventFlow.Sql.ReadModels;
using EventFlow.SQLite.Connections;
using EventFlow.SQLite.EventStores;
using EventFlow.SQLite.ReadStores;
using EventFlow.SQLite.RetryStrategies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventFlow.SQLite.Extensions
{
    public static class EventFlowOptionsExtensions
    {
        public static IEventFlowOptions ConfigureSQLite(
            this IEventFlowOptions eventFlowOptions,
            ISQLiteConfiguration sqLiteConfiguration)
        {
            eventFlowOptions.ServiceCollection.TryAddTransient<ISQLiteConnection, SQLiteConnection>();
            eventFlowOptions.ServiceCollection.TryAddTransient<ISQLiteConnectionFactory, SQLiteConnectionFactory>();
            eventFlowOptions.ServiceCollection.TryAddTransient<ISQLiteErrorRetryStrategy, SQLiteErrorRetryStrategy>();
            eventFlowOptions.ServiceCollection.TryAddSingleton(sqLiteConfiguration);
            return eventFlowOptions;
        }

        public static IEventFlowOptions UseSQLiteEventStore(
            this IEventFlowOptions eventFlowOptions)
        {
            return eventFlowOptions.UseEventPersistence<SQLiteEventPersistence>();
        }

        public static IEventFlowOptions UseSQLiteReadModel<TReadModel, TReadModelLocator>(
            this IEventFlowOptions eventFlowOptions)
            where TReadModel : class, IReadModel
            where TReadModelLocator : IReadModelLocator
        {
            return eventFlowOptions
                .RegisterServices(RegisterSQLiteReadStore<TReadModel>)
                .UseReadStoreFor<ISQLiteReadModelStore<TReadModel>, TReadModel, TReadModelLocator>();
        }

        public static IEventFlowOptions UseSQLiteReadModel<TReadModel>(
            this IEventFlowOptions eventFlowOptions)
            where TReadModel : class, IReadModel
        {
            return eventFlowOptions
                .RegisterServices(RegisterSQLiteReadStore<TReadModel>)
                .UseReadStoreFor<ISQLiteReadModelStore<TReadModel>, TReadModel>();
        }

        [Obsolete("Use the simpler method UseSQLiteReadModel<TReadModel> instead.")]
        public static IEventFlowOptions UseSQLiteReadModelFor<TAggregate, TIdentity, TReadModel>(
            this IEventFlowOptions eventFlowOptions)
            where TAggregate : IAggregateRoot<TIdentity>
            where TIdentity : IIdentity
            where TReadModel : class, IReadModel
        {
            return eventFlowOptions
                .RegisterServices(RegisterSQLiteReadStore<TReadModel>)
                .UseReadStoreFor<TAggregate, TIdentity, ISQLiteReadModelStore<TReadModel>, TReadModel>();
        }

        private static void RegisterSQLiteReadStore<TReadModel>(
            IServiceCollection serviceCollection)
            where TReadModel : class, IReadModel
        {
            serviceCollection.TryAddSingleton<IReadModelSqlGenerator, ReadModelSqlGenerator>();
            serviceCollection.TryAddTransient<ISQLiteReadModelStore<TReadModel>, SQLiteReadModelStore<TReadModel>>();
            serviceCollection.TryAddTransient<IReadModelStore<TReadModel>>(sp => sp.GetRequiredService<ISQLiteReadModelStore<TReadModel>>());
        }
    }
}