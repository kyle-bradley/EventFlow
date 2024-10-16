// The MIT License (MIT)
// 
// Copyright (c) 2015-2024 Rasmus Mikkelsen
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
using EventFlow.EntityFramework.ReadStores;
using EventFlow.EntityFramework.ReadStores.Configuration;
using EventFlow.Extensions;
using EventFlow.ReadStores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EventFlow.EntityFramework.Extensions
{
    public static class EventFlowOptionsEntityFrameworkReadStoreExtensions
    {

        private static IEventFlowOptions ApplyEntityFrameworkReadModelOptions<TReadModel, TDbContext>(this IEventFlowOptions eventFlowOptions,
            EntityFrameworkReadModelConfiguration<TReadModel> config)
            where TDbContext : DbContext
            where TReadModel : class, IReadModel, new()
        {
            return eventFlowOptions
                .RegisterServices(f =>
                {
                    f.TryAddTransient<IEntityFrameworkReadModelStore<TReadModel>,
                        EntityFrameworkReadModelStore<TReadModel, TDbContext>>();

                    f.TryAddSingleton<IApplyQueryableConfiguration<TReadModel>>(_ => config);

                    f.TryAddTransient<IReadModelStore<TReadModel>>(r =>
                        r.GetRequiredService<IEntityFrameworkReadModelStore<TReadModel>>());
                });
        }

        public static IEventFlowOptions UseEntityFrameworkReadModel<TReadModel, TDbContext>(
            this IEventFlowOptions eventFlowOptions)
            where TDbContext : DbContext
            where TReadModel : class, IReadModel, new()
        {
            return eventFlowOptions.ApplyEntityFrameworkReadModelOptions<TReadModel, TDbContext>(new EntityFrameworkReadModelConfiguration<TReadModel>())
                .UseReadStoreFor<IEntityFrameworkReadModelStore<TReadModel>, TReadModel>();
        }

        public static IEventFlowOptions UseEntityFrameworkReadModel<TReadModel, TDbContext>(
            this IEventFlowOptions eventFlowOptions,
            Func<EntityFrameworkReadModelConfiguration<TReadModel>, IApplyQueryableConfiguration<TReadModel>> configure)
            where TDbContext : DbContext
            where TReadModel : class, IReadModel, new()
        {
            var readModelConfig = new EntityFrameworkReadModelConfiguration<TReadModel>();
            configure(readModelConfig);

            return eventFlowOptions.ApplyEntityFrameworkReadModelOptions<TReadModel, TDbContext>(readModelConfig)
                .UseReadStoreFor<IEntityFrameworkReadModelStore<TReadModel>, TReadModel>();
        }

        public static IEventFlowOptions UseEntityFrameworkReadModel<TReadModel, TDbContext, TReadModelLocator>(
            this IEventFlowOptions eventFlowOptions,
            Func<EntityFrameworkReadModelConfiguration<TReadModel>, IApplyQueryableConfiguration<TReadModel>> configure)
            where TDbContext : DbContext
            where TReadModel : class, IReadModel, new()
            where TReadModelLocator : IReadModelLocator
        {
            var readModelConfig = new EntityFrameworkReadModelConfiguration<TReadModel>();
            configure(readModelConfig);

            return eventFlowOptions.ApplyEntityFrameworkReadModelOptions<TReadModel, TDbContext>(readModelConfig)
                .UseReadStoreFor<IEntityFrameworkReadModelStore<TReadModel>, TReadModel, TReadModelLocator>();
        }

        public static IEventFlowOptions UseEntityFrameworkReadModel<TReadModel, TDbContext, TReadModelLocator>(
            this IEventFlowOptions eventFlowOptions)
            where TDbContext : DbContext
            where TReadModel : class, IReadModel, new()
            where TReadModelLocator : IReadModelLocator
        {
            return eventFlowOptions.ApplyEntityFrameworkReadModelOptions<TReadModel, TDbContext>(new EntityFrameworkReadModelConfiguration<TReadModel>())
                .UseReadStoreFor<IEntityFrameworkReadModelStore<TReadModel>, TReadModel, TReadModelLocator>();
        }

    }
}