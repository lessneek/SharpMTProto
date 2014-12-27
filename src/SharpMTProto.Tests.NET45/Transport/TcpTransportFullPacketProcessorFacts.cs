//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

namespace SharpMTProto.Tests.Transport
{
    using System;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Dataflow;
    using BigMath.Utils;
    using FluentAssertions;
    using NUnit.Framework;
    using SharpMTProto.Dataflows;
    using SharpMTProto.Transport;

    [TestFixture]
    public class TcpTransportFullPacketProcessorFacts
    {
        [Test]
        public async Task Should_process_packet()
        {
            var bytes =
                new ArraySegment<byte>(
                    "540000000100000014AECD2F927A0A1A85F4D0D2FFDE304134E975DF41519D32FC304838D5C05C50233F44F203ED9608CF4B4C17C591CE35F32B7F00B414465A73701D6FE7D928E76B881979A4954D51CB532FFB"
                        .HexToBytes());
            var expectedPayloadBytes =
                new ArraySegment<byte>(
                    "14AECD2F927A0A1A85F4D0D2FFDE304134E975DF41519D32FC304838D5C05C50233F44F203ED9608CF4B4C17C591CE35F32B7F00B414465A73701D6FE7D928E76B881979A4954D51"
                        .HexToBytes());

            var bufferBlock = new BufferBlock<IBytesBucket>();

            var packetProcessor = new TcpTransportFullPacketProcessor();
            packetProcessor.IncomingMessageBuckets.Subscribe(bucket => bufferBlock.Post(bucket));

            await packetProcessor.ProcessIncomingPacketAsync(bytes);

            using (IBytesBucket bytesBucket = await bufferBlock.ReceiveAsync(TimeSpan.FromSeconds(5)))
            {
                bytesBucket.UsedBytes.Should().Equal(expectedPayloadBytes);
            }
        }
    }
}
