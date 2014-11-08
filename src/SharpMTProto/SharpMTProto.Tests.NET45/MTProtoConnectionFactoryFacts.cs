// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoConnectionFactoryFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using Catel.IoC;
using Catel.Logging;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using SharpMTProto.Transport;

namespace SharpMTProto.Tests
{
    [TestFixture]
    [Category("Core")]
    public class MTProtoConnectionFactoryFacts
    {
        [SetUp]
        public void SetUp()
        {
            LogManager.AddDebugListener(true);
        }

        [Test]
        public void Should_create_connection()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();
            
            var connectionFactory = new MTProtoConnectionFactory(serviceLocator);
            IMTProtoConnection connection = connectionFactory.Create(Mock.Of<TransportConfig>());
            connection.Should().NotBeNull();
        }
    }
}
