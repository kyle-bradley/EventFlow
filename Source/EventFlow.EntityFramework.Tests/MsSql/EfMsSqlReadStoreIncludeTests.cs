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
using System.Threading;
using System.Threading.Tasks;
using EventFlow.EntityFramework.Extensions;
using EventFlow.EntityFramework.Tests.Model;
using EventFlow.EntityFramework.Tests.MsSql.IncludeTests;
using EventFlow.EntityFramework.Tests.MsSql.IncludeTests.Commands;
using EventFlow.EntityFramework.Tests.MsSql.IncludeTests.Queries;
using EventFlow.Extensions;
using EventFlow.TestHelpers;
using EventFlow.TestHelpers.MsSql;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Shouldly;

namespace EventFlow.EntityFramework.Tests.MsSql
{
    [Category(Categories.Integration)]
    public class EfMsSqlReadStoreIncludeTests : IntegrationTest
    {
        private IMsSqlDatabase _testDatabase;

        protected override IServiceProvider Configure(IEventFlowOptions eventFlowOptions)
        {
            _testDatabase = MsSqlHelpz.CreateDatabase("eventflow");

            eventFlowOptions
                .RegisterServices(sr => sr.AddTransient(c => _testDatabase.ConnectionString))
                .ConfigureEntityFramework(EntityFrameworkConfiguration.New)
                .AddDbContextProvider<TestDbContext, MsSqlDbContextProvider>()
                .ConfigureForReadStoreIncludeTest()
                .AddDefaults(typeof(EfMsSqlReadStoreIncludeTests).Assembly);

            var serviceProvider = base.Configure(eventFlowOptions);

            return serviceProvider;
        }

        [TearDown]
        public void TearDown()
        {
            _testDatabase.DisposeSafe(Logger, "Failed to delete database");
        }

        [Test]
        public async Task ReadModelContainsPersonNameAfterCreation()
        {
            // Arrange
            var id = PersonId.New;
            
            // Act
            await CommandBus
                .PublishAsync(new CreatePersonCommand(id, "Bob"), CancellationToken.None)
                .ConfigureAwait(false);
            
            var readModel = await QueryProcessor
                .ProcessAsync(new PersonGetQuery(id), CancellationToken.None)
                .ConfigureAwait(false);

            // Assert
            readModel.ShouldNotBeNull();
            readModel.Name.ShouldBe("Bob");
            readModel.Addresses.ShouldBeEmpty();
        }

        [Test]
        public async Task ReadModelContainsPersonAddressesAfterAdd()
        {
            // Arrange
            var id = PersonId.New;
            await CommandBus
                .PublishAsync(new CreatePersonCommand(id, "Bob"), CancellationToken.None)
                .ConfigureAwait(false);

            // Act
            var address1 = new Address(AddressId.New, "Smith street 4.", "1234", "New York", "US");
            await CommandBus
                .PublishAsync(new AddAddressCommand(id, 
                        address1), 
                    CancellationToken.None)
                .ConfigureAwait(false);

            var address2 = new Address(AddressId.New, "Musterstraße 42.", "6541", "Berlin", "DE");
            await CommandBus
                .PublishAsync(new AddAddressCommand(id, 
                        address2), 
                    CancellationToken.None)
                .ConfigureAwait(false);

            var readModel = await QueryProcessor
                .ProcessAsync(new PersonGetQuery(id), CancellationToken.None)
                .ConfigureAwait(false);

            // Assert
            readModel.ShouldNotBeNull();
            readModel.NumberOfAddresses.ShouldBe(2);
            readModel.Addresses.Count.ShouldBe(2);

            readModel.Addresses.ShouldContain(a => 
                a.Street == address1.Street && 
                a.PostalCode == address1.PostalCode && 
                a.City == address1.City && 
                a.Country == address1.Country);

            readModel.Addresses.ShouldContain(a => 
                a.Street == address2.Street && 
                a.PostalCode == address2.PostalCode && 
                a.City == address2.City && 
                a.Country == address2.Country);
        }
    }
}
