//////////////////////////////////////////////////////////
// Copyright (c) Alexander Logger. All rights reserved. //
//////////////////////////////////////////////////////////

#region R#

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global
// ReSharper disable MemberCanBePrivate.Global

#endregion

namespace SharpMTProto.Messaging
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using BigMath;
    using BigMath.Utils;
    using SharpMTProto.Annotations;
    using SharpMTProto.Dataflows;
    using SharpMTProto.Schema;
    using SharpMTProto.Services;
    using SharpMTProto.Utils;
    using SharpTL;

    public interface IMessageCodec
    {
        #region Common methods.

        /// <summary>
        ///     Encodes a message asynchronously.
        /// </summary>
        /// <param name="messageEnvelope">Message envelope.</param>
        /// <param name="streamer">Streamer.</param>
        /// <param name="messageCodecMode">Messenger mode.</param>
        Task EncodeMessageAsync([NotNull] IMessageEnvelope messageEnvelope, [NotNull] TLStreamer streamer, MessageCodecMode messageCodecMode);

        /// <summary>
        ///     Decodes a message asyncronously.
        /// </summary>
        /// <param name="streamer">Streamer.</param>
        /// <param name="messageCodecMode">Messenger mode which encoded a message in the stream.</param>
        /// <returns>Message envelope.</returns>
        Task<IMessageEnvelope> DecodeMessageAsync([NotNull] TLStreamer streamer, MessageCodecMode messageCodecMode);

        #endregion

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
        /// <param name="messageCodecMode">MessageCodecMode of the message.</param>
        Task EncodeEncryptedMessageAsync(IMessageEnvelope messageEnvelope, TLStreamer streamer, byte[] authKey, MessageCodecMode messageCodecMode);

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
        /// <param name="messageCodecMode">MessageCodecMode of the message.</param>
        /// <returns>Message envelope.</returns>
        IMessageEnvelope DecodeEncryptedMessage(TLStreamer streamer, byte[] authKey, MessageCodecMode messageCodecMode);

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
        /// <param name="messageCodecMode">MessageCodecMode of the message.</param>
        /// <returns>Message envelope.</returns>
        Task<IMessageEnvelope> DecodeEncryptedMessageAsync(TLStreamer streamer, [NotNull] byte[] authKey, MessageCodecMode messageCodecMode);

        #endregion

        #region Helper methods.

        void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly);

        #endregion
    }

    public class MessageCodec : IMessageCodec
    {
        private static readonly ILog Log = LogManager.GetCurrentClassLogger();

        #region Constructors.

        /// <summary>
        ///     Initializes a new instance of the <see cref="MessageCodec" /> class.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">The <paramref name="tlRig" /> is <c>null</c>.</exception>
        /// <exception cref="System.ArgumentNullException">The <paramref name="hashServiceProvider" /> is <c>null</c>.</exception>
        /// <exception cref="System.ArgumentNullException">The <paramref name="encryptionServices" /> is <c>null</c>.</exception>
        /// <exception cref="System.ArgumentNullException">The <paramref name="randomGenerator" /> is <c>null</c>.</exception>
        public MessageCodec([NotNull] TLRig tlRig,
            [NotNull] IHashServiceProvider hashServiceProvider,
            [NotNull] IEncryptionServices encryptionServices,
            [NotNull] IRandomGenerator randomGenerator,
            [NotNull] IAuthKeysProvider authKeysProvider,
            IBytesOcean bytesOcean = null)
        {
            if (tlRig == null)
                throw new ArgumentNullException("tlRig");
            if (hashServiceProvider == null)
                throw new ArgumentNullException("hashServiceProvider");
            if (encryptionServices == null)
                throw new ArgumentNullException("encryptionServices");
            if (randomGenerator == null)
                throw new ArgumentNullException("randomGenerator");
            if (authKeysProvider == null)
                throw new ArgumentNullException("authKeysProvider");

            _tlRig = tlRig;
            _sha1 = hashServiceProvider.Create(HashServiceTag.SHA1);
            _encryptionServices = encryptionServices;
            _randomGenerator = randomGenerator;
            _authKeysProvider = authKeysProvider;

            // TODO: bytes ocean.
            _bytesOcean = bytesOcean ?? MTProtoDefaults.CreateDefaultMessageCodecBytesOcean();
        }

        #endregion

        #region Common methods.

        public Task EncodeMessageAsync(IMessageEnvelope messageEnvelope, TLStreamer streamer, MessageCodecMode messageCodecMode)
        {
            if (messageEnvelope == null)
                throw new ArgumentNullException("messageEnvelope");
            if (streamer == null)
                throw new ArgumentNullException("streamer");

            if (messageEnvelope.IsEncrypted)
            {
                AuthKeyWithId authKeyWithId;
                if (!_authKeysProvider.TryGet(messageEnvelope.SessionTag.AuthKeyId, out authKeyWithId))
                {
                    throw new InvalidMessageException(
                        string.Format("Unable to encrypt a message with auth key ID '{0}'. Auth key with such ID not found.",
                            messageEnvelope.SessionTag.AuthKeyId));
                }

                return EncodeEncryptedMessageAsync(messageEnvelope, streamer, authKeyWithId.AuthKey, messageCodecMode);
            }
            return EncodePlainMessageAsync(messageEnvelope.Message, streamer);
        }

        public async Task<IMessageEnvelope> DecodeMessageAsync(TLStreamer streamer, MessageCodecMode messageCodecMode)
        {
            IMessageEnvelope messageEnvelope;

            ulong incomingMsgAuthKeyId = ReadAuthKeyId(streamer);
            if (incomingMsgAuthKeyId == 0)
            {
                // Assume the message bytes has a plain (unencrypted) message.
                LogDebug(string.Format("Auth key ID = 0x{0:X16}. Assume this is a plain (unencrypted) message.", incomingMsgAuthKeyId));

                IMessage message = await DecodePlainMessageAsync(streamer);
                messageEnvelope = new MessageEnvelope(message);
            }
            else
            {
                // Assume the stream has an encrypted message.
                LogDebug(string.Format("Auth key ID = 0x{0:X16}. Assume this is encrypted message.", incomingMsgAuthKeyId));

                // Getting auth key by id.
                AuthKeyWithId incomingMsgAuthKeyWithId;
                if (!_authKeysProvider.TryGet(incomingMsgAuthKeyId, out incomingMsgAuthKeyWithId))
                {
                    throw new InvalidMessageException(
                        string.Format("Unable to decrypt incoming message with auth key ID '{0}'. Auth key with such ID not found.",
                            incomingMsgAuthKeyId));
                }

                // Decoding an encrypted message.
                messageEnvelope =
                    await
                        DecodeEncryptedMessageAsync(streamer, incomingMsgAuthKeyWithId.AuthKey, messageCodecMode);

                // TODO: check salt.
                // _authInfo.Salt == messageEnvelope.Salt;

                LogDebug(string.Format("Received encrypted message. Message ID = 0x{0:X16}.", messageEnvelope.Message.MsgId));
            }

            return messageEnvelope;
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
        private readonly IHashService _sha1;
        private readonly IRandomGenerator _randomGenerator;
        private readonly IAuthKeysProvider _authKeysProvider;
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

        #region Encrypted TL streamers.

        public async Task EncodeEncryptedMessageAsync(IMessageEnvelope messageEnvelope,
            [NotNull] TLStreamer streamer,
            [NotNull] byte[] authKey,
            MessageCodecMode messageCodecMode)
        {
            if (streamer == null)
                throw new ArgumentNullException("streamer");
            if (authKey == null)
                throw new ArgumentNullException("authKey");

            IMessage message = messageEnvelope.Message;
            ulong authKeyId = _authKeysProvider.ComputeAuthKeyId(authKey);

            // Writing inner data.
            using (IBytesBucket innerDataWithPaddingBucket = await _bytesOcean.TakeAsync(MaximumMessageLength).ConfigureAwait(false))
            using (var innerStreamer = new TLStreamer(innerDataWithPaddingBucket.Bytes))
            {
                innerStreamer.WriteUInt64(messageEnvelope.Salt);
                innerStreamer.WriteUInt64(messageEnvelope.SessionTag.SessionId);
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
                ComputeAesKeyAndIV(authKey, msgKey, out aesKey, out aesIV, messageCodecMode);

                long positionBeforeEncrpyptedData = streamer.Position;
                // Writing encrypted data.
                _encryptionServices.Aes256IgeEncrypt(innerStreamer, streamer, aesKey, aesIV);

                // Checking encrypted data length.
                long encryptedDataLength = streamer.Position - positionBeforeEncrpyptedData;

                Debug.Assert(encryptedDataLength == innerDataWithPaddingLength, "Wrong encrypted data length.");
            }
        }

        public IMessageEnvelope DecodeEncryptedMessage(TLStreamer streamer, byte[] authKey, MessageCodecMode messageCodecMode)
        {
            return DecodeEncryptedMessageAsync(streamer, authKey, messageCodecMode).Result;
        }

        public async Task<IMessageEnvelope> DecodeEncryptedMessageAsync(TLStreamer streamer, byte[] authKey, MessageCodecMode messageCodecMode)
        {
            if (streamer == null)
                throw new ArgumentNullException("streamer");
            if (authKey == null)
                throw new ArgumentNullException("authKey");

            ulong salt;
            ulong sessionId;
            ulong providedAuthKeyId = _authKeysProvider.ComputeAuthKeyId(authKey);

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
                ComputeAesKeyAndIV(authKey, msgKey, out aesKey, out aesIV, messageCodecMode);
                _encryptionServices.Aes256IgeDecrypt(streamer, innerStreamer, aesKey, aesIV);

                // Set used bytes count.
                innerDataWithPaddingBucket.Used = (int) innerStreamer.Position;

                // Rewind inner streamer to beginning.
                innerStreamer.Position = 0;

                // Reading decrypted data.
                salt = innerStreamer.ReadUInt64();
                sessionId = innerStreamer.ReadUInt64();
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

            return new MessageEnvelope(new MTProtoSessionTag(authKeyId, sessionId), salt, new Message(msgId, seqno, body));
        }

        #endregion

        #region Helper methods.

        public void PrepareSerializersForAllTLObjectsInAssembly(Assembly assembly)
        {
            _tlRig.PrepareSerializersForAllTLObjectsInAssembly(assembly);
        }

        private Int128 ComputeMsgKey(ArraySegment<byte> bytes)
        {
            byte[] innerDataSHA1 = _sha1.Hash(bytes);
            return innerDataSHA1.ToInt128(innerDataSHA1.Length - 16, true);
        }

        private void ComputeAesKeyAndIV(byte[] authKey, Int128 msgKey, out byte[] aesKey, out byte[] aesIV, MessageCodecMode messageCodecMode)
        {
            // x = 0 for messages from client to server and x = 8 for those from server to client.
            int x;
            switch (messageCodecMode)
            {
                case MessageCodecMode.Client:
                    x = 0;
                    break;
                case MessageCodecMode.Server:
                    x = 8;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("messageCodecMode");
            }

            byte[] msgKeyBytes = msgKey.ToBytes();

            byte[] buffer = _aesKeyAndIVComputationBuffer ?? (_aesKeyAndIVComputationBuffer = new byte[32 + MsgKeyLength]);

            // sha1_a = SHA1 (msg_key + substr (auth_key, x, 32));
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 0, MsgKeyLength);
            Buffer.BlockCopy(authKey, x, buffer, MsgKeyLength, 32);
            byte[] sha1A = _sha1.Hash(buffer);

            // sha1_b = SHA1 (substr (auth_key, 32+x, 16) + msg_key + substr (auth_key, 48+x, 16));
            Buffer.BlockCopy(authKey, 32 + x, buffer, 0, 16);
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 16, MsgKeyLength);
            Buffer.BlockCopy(authKey, 48 + x, buffer, 16 + MsgKeyLength, 16);
            byte[] sha1B = _sha1.Hash(buffer);

            // sha1_с = SHA1 (substr (auth_key, 64+x, 32) + msg_key);
            Buffer.BlockCopy(authKey, 64 + x, buffer, 0, 32);
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 32, MsgKeyLength);
            byte[] sha1C = _sha1.Hash(buffer);

            // sha1_d = SHA1 (msg_key + substr (auth_key, 96+x, 32));
            Buffer.BlockCopy(msgKeyBytes, 0, buffer, 0, MsgKeyLength);
            Buffer.BlockCopy(authKey, 96 + x, buffer, MsgKeyLength, 32);
            byte[] sha1D = _sha1.Hash(buffer);

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

        /// <summary>
        ///     Reads auth key id from a stream and restore initial position of the stream.
        /// </summary>
        /// <param name="streamer">Streamer to read.</param>
        /// <returns>Auth key id.</returns>
        /// <exception cref="MTProtoErrorException">In case stream contains only 4 bytes with an error code.</exception>
        /// <exception cref="InvalidMessageException">Thrown in case of message has invalid length.</exception>
        private static ulong ReadAuthKeyId(TLStreamer streamer)
        {
            long position = streamer.Position;
            if (streamer.Length == 4)
            {
                int error = streamer.ReadInt32();
                throw new MTProtoErrorException(error);
            }
            if (streamer.Length < 20)
            {
                throw new InvalidMessageException(
                    string.Format("Invalid message length: {0} bytes. Expected to be at least 20 bytes for message or 4 bytes for error code.",
                        streamer.Length));
            }
            ulong incomingMsgAuthKeyId = streamer.ReadUInt64();
            streamer.Position = position;
            return incomingMsgAuthKeyId;
        }

        [Conditional("DEBUG")]
        private static void LogDebug(string message)
        {
            Log.Debug(string.Format("[MTProtoMessenger] : {0}", message));
        }

        #endregion
    }

    public static class MessageCodecExtensions
    {
        #region Encrypted arrays.

        /// <summary>
        ///     Encode as encrypted message.
        /// </summary>
        /// <param name="codec">Codec itself.</param>
        /// <param name="messageEnvelope">A message envelope.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messageCodecMode">MessageCodecMode of the message.</param>
        /// <returns>Serialized encrypted message.</returns>
        public static byte[] EncodeEncryptedMessage(this IMessageCodec codec,
            IMessageEnvelope messageEnvelope,
            byte[] authKey,
            MessageCodecMode messageCodecMode)
        {
            return codec.EncodeEncryptedMessageAsync(messageEnvelope, authKey, messageCodecMode).Result;
        }

        /// <summary>
        ///     Encode as encrypted message asynchronously.
        /// </summary>
        /// <param name="codec">Codec itself.</param>
        /// <param name="messageEnvelope">A message envelope.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messageCodecMode">MessageCodecMode of the message.</param>
        /// <returns>Serialized encrypted message.</returns>
        public static async Task<byte[]> EncodeEncryptedMessageAsync(this IMessageCodec codec,
            IMessageEnvelope messageEnvelope,
            byte[] authKey,
            MessageCodecMode messageCodecMode)
        {
            using (var ms = new MemoryStream())
            using (var streamer = new TLStreamer(ms))
            {
                await codec.EncodeEncryptedMessageAsync(messageEnvelope, streamer, authKey, messageCodecMode);
                return ms.ToArray();
            }
        }

        /// <summary>
        ///     Decode encrypted message.
        /// </summary>
        /// <param name="codec">Codec itself.</param>
        /// <param name="messageBytes">Whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messageCodecMode">MessageCodecMode of the message.</param>
        /// <returns>Message envelope.</returns>
        public static IMessageEnvelope DecodeEncryptedMessage(this IMessageCodec codec,
            byte[] messageBytes,
            byte[] authKey,
            MessageCodecMode messageCodecMode)
        {
            return codec.DecodeEncryptedMessage(new ArraySegment<byte>(messageBytes), authKey, messageCodecMode);
        }

        /// <summary>
        ///     Decode encrypted message.
        /// </summary>
        /// <param name="codec">Codec itself.</param>
        /// <param name="messageBytes">Whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messageCodecMode">MessageCodecMode of the message.</param>
        /// <returns>Message envelope.</returns>
        public static IMessageEnvelope DecodeEncryptedMessage(this IMessageCodec codec,
            ArraySegment<byte> messageBytes,
            byte[] authKey,
            MessageCodecMode messageCodecMode)
        {
            return codec.DecodeEncryptedMessageAsync(messageBytes, authKey, messageCodecMode).Result;
        }

        /// <summary>
        ///     Decode encrypted message asynchronously.
        /// </summary>
        /// <param name="codec">Codec itself.</param>
        /// <param name="messageBytes">Whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messageCodecMode">MessageCodecMode of the message.</param>
        /// <returns>Message envelope.</returns>
        public static Task<IMessageEnvelope> DecodeEncryptedMessageAsync(this IMessageCodec codec,
            byte[] messageBytes,
            byte[] authKey,
            MessageCodecMode messageCodecMode)
        {
            return codec.DecodeEncryptedMessageAsync(new ArraySegment<byte>(messageBytes), authKey, messageCodecMode);
        }

        /// <summary>
        ///     Decode encrypted message asynchronously.
        /// </summary>
        /// <param name="codec">Codec itself.</param>
        /// <param name="messageBytes">Whole message bytes, which contain encrypted data.</param>
        /// <param name="authKey">
        ///     Authorization Key a 2048-bit key shared by the client device and the server, created upon user
        ///     registration directly on the client device be exchanging Diffie-Hellman keys, and never transmitted over a network.
        ///     Each authorization key is user-specific. There is nothing that prevents a user from having several keys (that
        ///     correspond to “permanent sessions” on different devices), and some of these may be locked forever in the event the
        ///     device is lost.
        /// </param>
        /// <param name="messageCodecMode">MessageCodecMode of the message.</param>
        /// <returns>Message envelope.</returns>
        public static async Task<IMessageEnvelope> DecodeEncryptedMessageAsync(this IMessageCodec codec,
            ArraySegment<byte> messageBytes,
            byte[] authKey,
            MessageCodecMode messageCodecMode)
        {
            using (var streamer = new TLStreamer(messageBytes))
            {
                return await codec.DecodeEncryptedMessageAsync(streamer, authKey, messageCodecMode);
            }
        }

        /// <summary>
        ///     Decodes a message asyncronously.
        /// </summary>
        /// <param name="codec">Codec itself.</param>
        /// <param name="data">Serialized message bytes.</param>
        /// <param name="messageCodecMode">Messenger mode which encoded a message in the stream.</param>
        /// <returns>Message envelope.</returns>
        public static async Task<IMessageEnvelope> DecodeMessageAsync(this IMessageCodec codec, ArraySegment<byte> data, MessageCodecMode messageCodecMode)
        {
            using (var streamer = new TLStreamer(data))
            {
                return await codec.DecodeMessageAsync(streamer, messageCodecMode);
            }
        }

        /// <summary>
        ///     Decodes a message.
        /// </summary>
        /// <param name="codec">Codec itself.</param>
        /// <param name="data">Serialized message bytes.</param>
        /// <param name="messageCodecMode">Messenger mode which encoded a message in the stream.</param>
        /// <returns>Message envelope.</returns>
        public static IMessageEnvelope DecodeMessage(this IMessageCodec codec, ArraySegment<byte> data, MessageCodecMode messageCodecMode)
        {
            return codec.DecodeMessageAsync(data, messageCodecMode).Result;
        }

        #endregion
    }
}
