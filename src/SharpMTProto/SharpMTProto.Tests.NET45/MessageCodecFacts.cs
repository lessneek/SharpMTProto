// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageCodecFacts.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using BigMath.Utils;
using Catel.IoC;
using FluentAssertions;
using SharpMTProto.Schema;
using NUnit.Framework;
using SharpMTProto.Messages;
using SharpMTProto.Services;

namespace SharpMTProto.Tests
{
    [TestFixture]
    public class MessageCodecFacts
    {
        private IMessageCodec GetMessageCodec()
        {
            IServiceLocator serviceLocator = TestRig.CreateTestServiceLocator();
            serviceLocator.RegisterInstance<IRandomGenerator>(new RandomGenerator(9));
            return serviceLocator.ResolveType<IMessageCodec>();
        }

        private static readonly byte[] TestPlainMessageBytes =
            ("0000000000000000" + "0807060504030201" + "10000000" + "9EB6EFEB" + "09" + "000102030405060708" + "0000").HexToBytes();
        
        private static readonly byte[] TestEncryptedClientMessageBytes =
            ("14AECD2F927A0A1AF383C85065EC4F3CA0A44838990AC9CD70C2FBE4E49FF346DA91A0F431EC9694056C3DE623B753CC12E720293B3D2955280FDD3C4AC445F8379557D9E078B232").HexToBytes();

        private static readonly byte[] TestEncryptedServerMessageBytes =
            ("14AECD2F927A0A1AF383C85065EC4F3CA0A44838990AC9CD5D280617914D57A06689F2E1C37328A2023B37804DA5BA0E8A8E0B19EFD843BB901107322E4F7FAEC5A5431CAAEBBFE7").HexToBytes();

        private static readonly IMessage TestMessage = new Message(0x0102030405060708UL, 0, "000102030405060708".HexToBytes());

        [Test]
        public void Should_throw_on_unwrap_plain_message_with_wrong_body_length()
        {
            IMessageCodec messageCodec = GetMessageCodec();
            byte[] messageBytes = ("0000000000000000" + "0807060504030201" + "11000000" + "9EB6EFEB" + "09" + "000102030405060708" + "0000").HexToBytes();
            var action = new Action(() => messageCodec.DecodePlainMessage(messageBytes));
            action.ShouldThrow<InvalidMessageException>();
        }

        [Test]
        public void Should_unwrap_plain_message()
        {
            IMessageCodec messageCodec = GetMessageCodec();
            IMessage message = messageCodec.DecodePlainMessage(TestPlainMessageBytes);
            message.ShouldBeEquivalentTo(TestMessage);
        }

        [Test]
        public void Should_wrap_plain_message()
        {
            IMessageCodec messageCodec = GetMessageCodec();
            byte[] wrappedMessageBytes = messageCodec.EncodePlainMessage(TestMessage);
            wrappedMessageBytes.ShouldBeEquivalentTo(TestPlainMessageBytes);
        }

        [Test]
        public void Should_unwrap_encrypted_message()
        {
            IMessageCodec messageCodec = GetMessageCodec();
            IMessage message = messageCodec.DecodePlainMessage(TestPlainMessageBytes);
            message.ShouldBeEquivalentTo(TestMessage);
        }

        [TestCase(Sender.Client)]
        [TestCase(Sender.Server)]
        public void Should_wrap_encrypted_message(Sender sender)
        {
            IMessageCodec messageCodec = GetMessageCodec();
            byte[] wrappedMessageBytes = messageCodec.EncodeEncryptedMessage(TestMessage, TestRig.AuthKey, 0x999UL, 0x777UL, sender);
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
            wrappedMessageBytes.ShouldBeEquivalentTo(expectedMessageBytes);
        }
    }
}
