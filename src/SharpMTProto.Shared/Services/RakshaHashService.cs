// --------------------------------------------------------------------------------------------------------------------
// <copyright file="HashServices.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using System;
    using System.IO;
    using Raksha.Crypto;
    using Raksha.Crypto.Digests;

    public class RakshaHashServiceProvider : IHashServiceProvider
    {
        public IHashService Create(HashServiceTag tag)
        {
            return new RakshaHashService(tag);
        }
    }

    public class RakshaHashService : IHashService
    {
        private readonly Func<IDigest> _createAlgorithm;

        public RakshaHashService(HashServiceTag tag)
        {
            switch (tag)
            {
                case HashServiceTag.SHA1:
                    _createAlgorithm = () => new Sha1Digest();
                    break;
                case HashServiceTag.MD5:
                    _createAlgorithm = () => new MD5Digest();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("tag");
            }
        }

        public byte[] Hash(byte[] data)
        {
            return Hash(data, 0, data.Length);
        }

        public byte[] Hash(byte[] data, int offset, int count)
        {
            IDigest digest = _createAlgorithm();

            digest.BlockUpdate(data, offset, count);

            var output = new byte[digest.GetDigestSize()];
            digest.DoFinal(output, 0);
            return output;
        }

        public byte[] Hash(ArraySegment<byte> data)
        {
            return Hash(data.Array, data.Offset, data.Count);
        }

        public byte[] Hash(Stream stream)
        {
            IDigest digest = _createAlgorithm();

            int read;
            var buffer = new byte[256];
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                digest.BlockUpdate(buffer, 0, read);
            }

            var output = new byte[digest.GetDigestSize()];
            digest.DoFinal(output, 0);
            return output;
        }
    }
}
