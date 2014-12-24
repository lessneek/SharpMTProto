// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MessageCodec.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Messaging
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using Annotations;
    using BigMath;
    using BigMath.Utils;
    using Dataflows;
    using Schema;
    using Services;
    using SharpTL;

    public interface IMessageCodec
    {
        #region Plain arrays.

        /// <summary>
        ///     Encode as plain message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <returns>Serialized plain message.</returns>
        byte[] EncodePlainMessage(IMessage message);

        /// <summary>
        ///     Encode as plain message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <returns>Serialized plain message.</returns>
        Task<byte[]> EncodePlainMessageAsync(IMessage message);

        /// <summary>
        ///     Decode plain message.
        /// </summary>
        /// <param name="data">Serialized message bytes.</param>
        /// <returns>Message.</returns>
        IMessage DecodePlainMessage(byte[] data);

        /// <summary>
        ///     Decode plain message.
        /// </summary>
        /// <param name="data">Serialized message bytes.</param>
        /// <returns>Message.</returns>
        IMessage DecodePlainMessage(ArraySegment<byte> data);

        /// <summary>
        ///     Decode plain message.
        /// </summary>
        /// <param name="data">Serialized message bytes.</param>
        /// <returns>Message.</returns>
        Task<IMessage> DecodePlainMessageAsync(ArraySegment<byte> data);

        #endregion

        #region Plain TL streamers.

        /// <summary>
        ///     Encode as plain message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="streamer">TL streamer for writing serialized message.</param>
        void EncodePlainMessage([NotNull] IMessage message, [NotNull] TLStreamer streamer);

        /// <summary>
        ///     Encode as plain message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="streamer">TL streamer for writing serialized message.</param>
        Task EncodePlainMessageAsync([NotNull] IMessage message, [NotNull] TLStreamer streamer);

        /// <summary>
        ///     Decode plain message.
        /// </summary>
        /// <param name="streamer">TL streamer with serialized message bytes.</param>
        /// <returns>Message.</returns>
        Task<IMessage> DecodePlainMessageAsync([NotNull] TLStreamer streamer);

        #endregion

        #region Encrypted arrays.

        /// <summary>
        ///     Encode as encrypted message.
        /// </summary>
        /// <param name="messageEnvelope">A message envelope.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messengerMode">MessengerMode of the message.</param>
        /// <returns>Serialized encrypted message.</returns>
        byte[] EncodeEncryptedMessage(MessageEnvelope messageEnvelope, [NotNull] byte[] authKey, MessengerMode messengerMode);

        /// <summary>
        ///     Encode as encrypted message.
        /// </summary>
        /// <param name="messageEnvelope">A message envelope.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messengerMode">MessengerMode of the message.</param>
        /// <returns>Serialized encrypted message.</returns>
        Task<byte[]> EncodeEncryptedMessageAsync(MessageEnvelope messageEnvelope, [NotNull] byte[] authKey, MessengerMode messengerMode);

        /// <summary>
        ///     Decode encrypted message.
        /// </summary>
        /// <param name="messageBytes">Whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messengerMode">MessengerMode of the message.</param>
        /// <returns>Message envelope.</returns>
        MessageEnvelope DecodeEncryptedMessage(byte[] messageBytes, [NotNull] byte[] authKey, MessengerMode messengerMode);

        /// <summary>
        ///     Decode encrypted message.
        /// </summary>
        /// <param name="messageBytes">Whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messengerMode">MessengerMode of the message.</param>
        /// <returns>Message envelope.</returns>
        MessageEnvelope DecodeEncryptedMessage(ArraySegment<byte> messageBytes, [NotNull] byte[] authKey, MessengerMode messengerMode);

        /// <summary>
        ///     Decode encrypted message.
        /// </summary>
        /// <param name="messageBytes">Whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messengerMode">MessengerMode of the message.</param>
        /// <returns>Message envelope.</returns>
        Task<MessageEnvelope> DecodeEncryptedMessageAsync(byte[] messageBytes, [NotNull] byte[] authKey, MessengerMode messengerMode);

        /// <summary>
        ///     Decode encrypted message.
        /// </summary>
        /// <param name="messageBytes">Whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messengerMode">MessengerMode of the message.</param>
        /// <returns>Message envelope.</returns>
        Task<MessageEnvelope> DecodeEncryptedMessageAsync(ArraySegment<byte> messageBytes, [NotNull] byte[] authKey, MessengerMode messengerMode);

        #endregion

        #region Encrypted TL streamers.

        /// <summary>
        ///     Encode as encrypted message.
        /// </summary>
        /// <param name="messageEnvelope">A message envelope.</param>
        /// <param name="streamer">TL streamer.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messengerMode">MessengerMode of the message.</param>
        Task EncodeEncryptedMessageAsync(MessageEnvelope messageEnvelope, TLStreamer streamer, byte[] authKey, MessengerMode messengerMode);

        /// <summary>
        ///     Decode encrypted message.
        /// </summary>
        /// <param name="streamer">Stream with whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messengerMode">MessengerMode of the message.</param>
        /// <returns>Message envelope.</returns>
        MessageEnvelope DecodeEncryptedMessage(TLStreamer streamer, byte[] authKey, MessengerMode messengerMode);

        /// <summary>
        ///     Decode encrypted message.
        /// </summary>
        /// <param name="streamer">Stream with whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messengerMode">MessengerMode of the message.</param>
        /// <returns>Message envelope.</returns>
        Task<MessageEnvelope> DecodeEncryptedMessageAsync(TLStreamer streamer, [NotNull] byte[] authKey, MessengerMode messengerMode);

        #endregion

        #region Helper methods.

        void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly);

        /// <summary>
        ///     Computes auth key id.
        /// </summary>
        /// <param name="authKey">Auth key.</param>
        /// <returns>Auth key id.</returns>
        ulong ComputeAuthKeyId(byte[] authKey);

        #endregion
    }

    public class MessageCodec : IMessageCodec
    {
        #region Constructors.

        /// <summary>
        ///     Initializes a new instance of the <see cref="MessageCodec" /> class.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">The <paramref name="tlRig" /> is <c>null</c>.</exception>
        /// <exception cref="System.ArgumentNullException">The <paramref name="hashServices" /> is <c>null</c>.</exception>
        /// <exception cref="System.ArgumentNullException">The <paramref name="encryptionServices" /> is <c>null</c>.</exception>
        /// <exception cref="System.ArgumentNullException">The <paramref name="randomGenerator" /> is <c>null</c>.</exception>
        public MessageCodec([NotNull] TLRig tlRig,
            [NotNull] IHashServices hashServices,
            [NotNull] IEncryptionServices encryptionServices,
            [NotNull] IRandomGenerator randomGenerator,
            IBytesOcean bytesOcean = null)
        {
            if (tlRig == null)
                throw new ArgumentNullException("tlRig");
            if (hashServices == null)
                throw new ArgumentNullException("hashServices");
            if (encryptionServices == null)
                throw new ArgumentNullException("encryptionServices");
            if (randomGenerator == null)
                throw new ArgumentNullException("randomGenerator");

            _tlRig = tlRig;
            _hashServices = hashServices;
            _encryptionServices = encryptionServices;
            _randomGenerator = randomGenerator;

            // TODO: bytes ocean.
            _bytesOcean = bytesOcean ?? MTProtoDefaults.CreateDefaultMessageCodecBytesOcean();
        }

        #endregion

        #region Constants.

        /// <summary>
        ///     Message header length in bytes.
        /// </summary>
        public const int PlainHeaderLength = 20;

        /// <summary>
        ///     Outer header length in bytes (8 + 16).
        /// </summary>
        private const int EncryptedOuterHeaderLength = 24;

        /// <summary>
        ///     Inner header length in bytes (8 + 8 + 8 + 4 + 4).
        /// </summary>
        private const int EncryptedInnerHeaderLength = 32;

        /// <summary>
        ///     Count of bytes to encode length of a message.
        /// </summary>
        private const int LengthBytesCount = 4;

        private const int MsgKeyLength = 16;
        private const int Alignment = 16;
        private const int MaximumMessageLength = MTProtoDefaults.MaximumMessageLength;
        private const int MaximumMsgDataLength = MaximumMessageLength - EncryptedInnerHeaderLength;

        #endregion

        #region Fields.

        [ThreadStatic] private static byte[] _aesKeyAndIVComputationBuffer;
        private readonly byte[] _alignmentBuffer = new byte[Alignment];
        private readonly IBytesOcean _bytesOcean;
        private readonly IEncryptionServices _encryptionServices;
        private readonly IHashServices _hashServices;
        private readonly IRandomGenerator _randomGenerator;
        private readonly TLRig _tlRig;

        #endregion

        #region Plain arrays.

        public byte[] EncodePlainMessage(IMessage message)
        {
            return EncodePlainMessageAsync(message).Result;
        }

        public async Task<byte[]> EncodePlainMessageAsync(IMessage message)
        {
            using (var ms = new MemoryStream())
            using (var streamer = new TLStreamer(ms))
            {
                await EncodePlainMessageAsync(message, streamer);
                return ms.ToArray();
            }
        }

        public IMessage DecodePlainMessage(byte[] data)
        {
            return DecodePlainMessage(new ArraySegment<byte>(data, 0, data.Length));
        }

        public IMessage DecodePlainMessage(ArraySegment<byte> data)
        {
            return DecodePlainMessageAsync(data).Result;
        }

        public async Task<IMessage> DecodePlainMessageAsync(ArraySegment<byte> data)
        {
            using (var streamer = new TLStreamer(data))
            {
                return await DecodePlainMessageAsync(streamer);
            }
        }

        #endregion

        #region Plain TL streamers.

        public void EncodePlainMessage(IMessage message, TLStreamer streamer)
        {
            EncodePlainMessageAsync(message, streamer).Wait();
        }

        public Task EncodePlainMessageAsync(IMessage message, TLStreamer streamer)
        {
            return Task.Run(() =>
            {
                // Writing header.
                streamer.WriteInt64(0); // Plain unencrypted message must always have zero auth key id.
                streamer.WriteUInt64(message.MsgId); // MsgId.
                long lengthPos = streamer.Position;
                streamer.Position += LengthBytesCount; // Skip length.

                // Writing data.
                var length = (uint) _tlRig.Serialize(message.Body, streamer, TLSerializationMode.Boxed);

                streamer.Position = lengthPos;
                streamer.WriteUInt32(length);
                streamer.Position += length;
            });
        }

        public Task<IMessage> DecodePlainMessageAsync(TLStreamer streamer)
        {
            return Task.Run(() =>
            {
                long authKey = streamer.ReadInt64();
                if (authKey != 0)
                {
                    throw new InvalidMessageException("Auth key must always be zero for a plain message.");
                }

                ulong msgId = streamer.ReadUInt64();
                int bodyLength = streamer.ReadInt32();
                if (bodyLength > streamer.BytesTillEnd)
                {
                    throw new InvalidMessageException("Wrong message body length.");
                }
                object body = _tlRig.Deserialize(streamer);
                return (IMessage) new Message(msgId, 0, body);
            });
        }

        #endregion

        #region Encrypted arrays.

        public byte[] EncodeEncryptedMessage(MessageEnvelope messageEnvelope, byte[] authKey, MessengerMode messengerMode)
        {
            return EncodeEncryptedMessageAsync(messageEnvelope, authKey, messengerMode).Result;
        }

        public async Task<byte[]> EncodeEncryptedMessageAsync(MessageEnvelope messageEnvelope, byte[] authKey, MessengerMode messengerMode)
        {
            using (var ms = new MemoryStream())
            using (var streamer = new TLStreamer(ms))
            {
                await EncodeEncryptedMessageAsync(messageEnvelope, streamer, authKey, messengerMode);
                return ms.ToArray();
            }
        }

        public MessageEnvelope DecodeEncryptedMessage(byte[] messageBytes, byte[] authKey, MessengerMode messengerMode)
        {
            return DecodeEncryptedMessage(new ArraySegment<byte>(messageBytes), authKey, messengerMode);
        }

        public MessageEnvelope DecodeEncryptedMessage(ArraySegment<byte> messageBytes, byte[] authKey, MessengerMode messengerMode)
        {
            return DecodeEncryptedMessageAsync(messageBytes, authKey, messengerMode).Result;
        }

        public Task<MessageEnvelope> DecodeEncryptedMessageAsync(byte[] messageBytes, byte[] authKey, MessengerMode messengerMode)
        {
            return DecodeEncryptedMessageAsync(new ArraySegment<byte>(messageBytes), authKey, messengerMode);
        }

        public async Task<MessageEnvelope> DecodeEncryptedMessageAsync(ArraySegment<byte> messageBytes, byte[] authKey, MessengerMode messengerMode)
        {
            using (var streamer = new TLStreamer(messageBytes))
            {
                return await DecodeEncryptedMessageAsync(streamer, authKey, messengerMode);
            }
        }

        #endregion

        #region Encrypted TL streamers.

        public async Task EncodeEncryptedMessageAsync(MessageEnvelope messageEnvelope,
            [NotNull] TLStreamer streamer,
            [NotNull] byte[] authKey,
            MessengerMode messengerMode)
        {
            if (streamer == null)
                throw new ArgumentNullException("streamer");
            if (authKey == null)
                throw new ArgumentNullException("authKey");

            IMessage message = messageEnvelope.Message;
            ulong authKeyId = ComputeAuthKeyId(authKey);

            // Writing inner data.
            using (IBytesBucket innerDataWithPaddingBucket = await _bytesOcean.TakeAsync(MaximumMessageLength).ConfigureAwait(false))
            using (var innerStreamer = new TLStreamer(innerDataWithPaddingBucket.Bytes))
            {
                innerStreamer.WriteUInt64(messageEnvelope.Salt);
                innerStreamer.WriteUInt64(messageEnvelope.SessionId);
                innerStreamer.WriteUInt64(message.MsgId);
                innerStreamer.WriteUInt32(message.Seqno);

                long messageDataLengthPosition = innerStreamer.Position;
                innerStreamer.WriteInt32(0); // Write zero length before we have actual length of a serialized body.

                // Serializing a message data (body).
                var messageDataLength = (int) _tlRig.Serialize(message.Body, innerStreamer, TLSerializationMode.Boxed);

                // Calculating and writing of inner data length.
                innerStreamer.Position = messageDataLengthPosition;
                innerStreamer.WriteInt32(messageDataLength);
                innerStreamer.Position += messageDataLength;

                // Calculating and writing alignment padding length.
                int innerDataLength = EncryptedInnerHeaderLength + messageDataLength;
                int mod = innerDataLength%Alignment;
                int paddingLength = mod > 0 ? Alignment - mod : 0;
                int innerDataWithPaddingLength = innerDataLength + paddingLength;

                if (paddingLength > 0)
                {
                    _randomGenerator.FillWithRandom(_alignmentBuffer);
                    innerStreamer.Write(_alignmentBuffer, 0, paddingLength);
                }

                innerStreamer.SetLength(innerStreamer.Position);
                innerStreamer.Position = 0;

                innerDataWithPaddingBucket.Used = innerDataWithPaddingLength;

                Int128 msgKey = ComputeMsgKey(innerDataWithPaddingBucket.UsedBytes);

                // Writing header of an encrypted message.
                streamer.WriteUInt64(authKeyId);
                streamer.WriteInt128(msgKey);

                // Encrypting.
                byte[] aesKey, aesIV;
                ComputeAesKeyAndIV(authKey, msgKey, out aesKey, out aesIV, messengerMode);

                long positionBeforeEncrpyptedData = streamer.Position;
                // Writing encrypted data.
                _encryptionServices.Aes256IgeEncrypt(innerStreamer, streamer, aesKey, aesIV);

                // Checking encrypted data length.
                long encryptedDataLength = streamer.Position - positionBeforeEncrpyptedData;

                Debug.Assert(encryptedDataLength == innerDataWithPaddingLength, "Wrong encrypted data length.");
            }
        }

        public MessageEnvelope DecodeEncryptedMessage(TLStreamer streamer, byte[] authKey, MessengerMode messengerMode)
        {
            return DecodeEncryptedMessageAsync(streamer, authKey, messengerMode).Result;
        }

        public async Task<MessageEnvelope> DecodeEncryptedMessageAsync(TLStreamer streamer, byte[] authKey, MessengerMode messengerMode)
        {
            if (streamer == null)
                throw new ArgumentNullException("streamer");
            if (authKey == null)
                throw new ArgumentNullException("authKey");

            var messageEnvelope = new MessageEnvelope();
            ulong providedAuthKeyId = ComputeAuthKeyId(authKey);

            Int128 msgKey;

            // Reading header.
            ulong authKeyId = streamer.ReadUInt64();
            if (authKeyId != providedAuthKeyId)
            {
                throw new InvalidAuthKey(
                    string.Format("Message encrypted with auth key with id={0}, but auth key provided for decryption with id={1}.",
                        authKeyId,
                        providedAuthKeyId));
            }
            msgKey = streamer.ReadInt128();

            // Reading encrypted data.
            UInt64 msgId;
            UInt32 seqno;
            Object body;

            using (IBytesBucket innerDataWithPaddingBucket = await _bytesOcean.TakeAsync(MaximumMessageLength))
            using (var innerStreamer = new TLStreamer(innerDataWithPaddingBucket.Bytes))
            {
                // Decrypting.
                byte[] aesKey, aesIV;
                ComputeAesKeyAndIV(authKey, msgKey, out aesKey, out aesIV, messengerMode);
                _encryptionServices.Aes256IgeDecrypt(streamer, innerStreamer, aesKey, aesIV);

                // Set used bytes count.
                innerDataWithPaddingBucket.Used = (int) innerStreamer.Position;

                // Rewind inner streamer to beginning.
                innerStreamer.Position = 0;

                // Reading decrypted data.
                messageEnvelope.Salt = innerStreamer.ReadUInt64();
                messageEnvelope.SessionId = innerStreamer.ReadUInt64();
                msgId = innerStreamer.ReadUInt64();
                seqno = innerStreamer.ReadUInt32();
                Int32 messageDataLength = innerStreamer.ReadInt32();

                if (messageDataLength > MaximumMsgDataLength)
                {
                    throw new InvalidMessageException(string.Format("Message data length must not exceed {0}, but actual is {1}.",
                        MaximumMsgDataLength,
                        messageDataLength));
                }

                long positionBeforeMessageData = innerStreamer.Position;

                body = _tlRig.Deserialize(innerStreamer);

                // Checking message body length.
                var actualMessageDataLength = (int) (innerStreamer.Position - positionBeforeMessageData);
                if (actualMessageDataLength != messageDataLength)
                {
                    throw new InvalidMessageException(string.Format("Expected message data length to be {0}, but actual is {1}.",
                        messageDataLength,
                        actualMessageDataLength));
                }

                // When an encrypted message is received, it must be checked that
                // msg_key is in fact equal to the 128 lower-order bits
                // of the SHA1 hash of the previously encrypted portion.
                Int128 expectedMsgKey = ComputeMsgKey(innerDataWithPaddingBucket.UsedBytes);
                if (msgKey != expectedMsgKey)
                {
                    throw new InvalidMessageException(string.Format("Expected message key to be {0}, but actual is {1}.", expectedMsgKey, msgKey));
                }
            }

            messageEnvelope.Message = new Message(msgId, seqno, body);
            return messageEnvelope;
        }

        #endregion

        #region Helper methods.

        public void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly)
        {
            _tlRig.PrepareSerializersForAllTLObjectsInAssembly(assembly);
        }

        public ulong ComputeAuthKeyId(byte[] authKey)
        {
            byte[] authKeySHA1 = _hashServices.ComputeSHA1(authKey);
            return authKeySHA1.ToUInt64(authKeySHA1.Length - 8, true);
        }

        private Int128 ComputeMsgKey(ArraySegment<byte> bytes)
        {
            byte[] innerDataSHA1 = _hashServices.ComputeSHA1(bytes);
            return innerDataSHA1.ToInt128(innerDataSHA1.Length - 16, true);
        }

        private void ComputeAesKeyAndIV(byte[] authKey, Int128 msgKey, out byte[] aesKey, out byte[] aesIV, MessengerMode messengerMode)
        {
            // x = 0 for messages from client to server and x = 8 for those from server to client.
            int x;
            switch (messengerMode)
            {
                case MessengerMode.Client:
                    x = 0;
                    break;
                case MessengerMode.Server:
                    x = 8;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("messengerMode");
            }

            byte[] msgKeyBytes = msgKey.ToBytes();

            byte[] buffer = _aesKeyAndIVComputationBuffer ?? (_aesKeyAndIVComputationBuffer = new byte[32 + MsgKeyLength]);

            // sha1_a = SHA1 (msg_key + substr (auth_key, x, 32));
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 0, MsgKeyLength);
            Buffer.BlockCopy(authKey, x, buffer, MsgKeyLength, 32);
            byte[] sha1A = _hashServices.ComputeSHA1(buffer);

            // sha1_b = SHA1 (substr (auth_key, 32+x, 16) + msg_key + substr (auth_key, 48+x, 16));
            Buffer.BlockCopy(authKey, 32 + x, buffer, 0, 16);
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 16, MsgKeyLength);
            Buffer.BlockCopy(authKey, 48 + x, buffer, 16 + MsgKeyLength, 16);
            byte[] sha1B = _hashServices.ComputeSHA1(buffer);

            // sha1_с = SHA1 (substr (auth_key, 64+x, 32) + msg_key);
            Buffer.BlockCopy(authKey, 64 + x, buffer, 0, 32);
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 32, MsgKeyLength);
            byte[] sha1C = _hashServices.ComputeSHA1(buffer);

            // sha1_d = SHA1 (msg_key + substr (auth_key, 96+x, 32));
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 0, MsgKeyLength);
            Buffer.BlockCopy(authKey, 96 + x, buffer, MsgKeyLength, 32);
            byte[] sha1D = _hashServices.ComputeSHA1(buffer);

            // aes_key = substr (sha1_a, 0, 8) + substr (sha1_b, 8, 12) + substr (sha1_c, 4, 12);
            aesKey = new byte[32];
            Buffer.BlockCopy(sha1A, 0, aesKey, 0, 8);
            Buffer.BlockCopy(sha1B, 8, aesKey, 8, 12);
            Buffer.BlockCopy(sha1C, 4, aesKey, 20, 12);

            // aes_iv = substr (sha1_a, 8, 12) + substr (sha1_b, 0, 8) + substr (sha1_c, 16, 4) + substr (sha1_d, 0, 8);
            aesIV = new byte[32];
            Buffer.BlockCopy(sha1A, 8, aesIV, 0, 12);
            Buffer.BlockCopy(sha1B, 0, aesIV, 12, 8);
            Buffer.BlockCopy(sha1C, 16, aesIV, 20, 4);
            Buffer.BlockCopy(sha1D, 0, aesIV, 24, 8);
        }

        #endregion
    }
}
