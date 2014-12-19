// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageCodecFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Tests.Messaging
{
    using System;
    using System.Threading.Tasks;
    using Autofac;
    using BigMath.Utils;
    using FluentAssertions;
    using NUnit.Framework;
    using Schema;
    using SetUp;
    using SharpMTProto.Messaging;
    using SharpMTProto.Services;

    [TestFixture]
    [Category("Messaging")]
    public class MessageCodecFacts : SharpMTProtoTestBase
    {
        private static readonly byte[] TestPlainMessageBytes =
            ("0000000000000000" + "0807060504030201" + "10000000" + "9EB6EFEB" + "09" + "000102030405060708" + "0000").HexToBytes();

        private static readonly byte[] TestEncryptedClientMessageBytes =
            ("14AECD2F927A0A1AF383C85065EC4F3CA0A44838990AC9CD70C2FBE4E49FF346DA91A0F431EC9694056C3DE623B753CC12E720293B3D2955280FDD3C4AC445F8379557D9E078B232")
                .HexToBytes();

        private static readonly byte[] TestEncryptedServerMessageBytes =
            ("14AECD2F927A0A1AF383C85065EC4F3CA0A44838990AC9CD5D280617914D57A06689F2E1C37328A2023B37804DA5BA0E8A8E0B19EFD843BB901107322E4F7FAEC5A5431CAAEBBFE7")
                .HexToBytes();

        private static readonly IMessage TestMessage = new Message(0x0102030405060708UL, 0, "000102030405060708".HexToBytes());

        [SetUp]
        public void SetUp()
        {
            Override(builder => builder.Register(context => new RandomGenerator(9)).As<IRandomGenerator>());
        }

        [Test]
        public void Should_throw_on_decode_plain_message_with_wrong_body_length()
        {
            var messageCodec = Resolve<IMessageCodec>();
            byte[] messageBytes =
                ("0000000000000000" + "0807060504030201" + "11000000" + "9EB6EFEB" + "09" + "000102030405060708" + "0000").HexToBytes();
            var action = new Action(() => messageCodec.DecodePlainMessage(messageBytes));
            action.ShouldThrow<InvalidMessageException>();
        }

        [Test]
        public void Should_encode_plain_message()
        {
            var messageCodec = Resolve<IMessageCodec>();
            byte[] wrappedMessageBytes = messageCodec.EncodePlainMessage(TestMessage);
            wrappedMessageBytes.Should().Equal(TestPlainMessageBytes);
        }

        [Test]
        public void Should_decode_plain_message()
        {
            var messageCodec = Resolve<IMessageCodec>();
            IMessage message = messageCodec.DecodePlainMessage(TestPlainMessageBytes);
            message.Should().Be(TestMessage);
        }

        [TestCase(Sender.Client)]
        [TestCase(Sender.Server)]
        public void Should_encode_encrypted_message(Sender sender)
        {
            var messageCodec = Resolve<IMessageCodec>();
            var messageEnvelope = new MessageEnvelope(TestMessage, 0x999UL, 0x777UL);
            byte[] encryptedMessageBytes = messageCodec.EncodeEncryptedMessage(messageEnvelope, AuthKey, sender);
            byte[] expectedMessageBytes;
            switch (sender)
            {
                case Sender.Client:
                    expectedMessageBytes = TestEncryptedClientMessageBytes;
                    break;
                case Sender.Server:
                    expectedMessageBytes = TestEncryptedServerMessageBytes;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("sender");
            }
            encryptedMessageBytes.Should().Equal(expectedMessageBytes);
        }

        [TestCase(Sender.Client)]
        [TestCase(Sender.Server)]
        public async Task Should_decode_encrypted_message(Sender sender)
        {
            var messageCodec = Resolve<IMessageCodec>();
            var expectedMessageEnvelope = new MessageEnvelope(TestMessage, 0x999UL, 0x777UL);

            byte[] messageBytes;
            switch (sender)
            {
                case Sender.Client:
                    messageBytes = TestEncryptedClientMessageBytes;
                    break;
                case Sender.Server:
                    messageBytes = TestEncryptedServerMessageBytes;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("sender");
            }

            MessageEnvelope messageEnvelope = await messageCodec.DecodeEncryptedMessageAsync(messageBytes, AuthKey, sender);
            messageEnvelope.Should().Be(expectedMessageEnvelope);
        }
    }
}
