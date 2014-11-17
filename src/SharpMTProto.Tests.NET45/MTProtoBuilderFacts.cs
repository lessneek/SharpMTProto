// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MTProtoBuilderFacts.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using FluentAssertions;
using NUnit.Framework;
using SharpMTProto.Transport;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class MTProtoBuilderFacts
    {
        [Test]
        public void Should_create_connection()
        {
            IMTProtoConnection connection = MTProtoBuilder.Default.BuildConnection(new TcpTransportConfig("127.0.0.1", 9999));
            connection.Should().NotBeNull();
        }
    }
}