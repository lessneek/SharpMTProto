// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SystemHashService.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    public class SystemHashServiceProvider : IHashServiceProvider
    {
        public IHashService Create(HashServiceTag tag)
        {
            return new SystemHashService(tag);
        }
    }

    public class SystemHashService : IHashService
    {
        private readonly Func<long, HashAlgorithm> _createHashAlgorithm;

        public SystemHashService(HashServiceTag tag)
        {
            switch (tag)
            {
                case HashServiceTag.SHA1:
                    _createHashAlgorithm = length =>
                    {
                        if (length <= 1024)
                        {
                            // SHA1Managed is faster on small data length.
                            return new SHA1Managed();
                        }
                        return SHA1.Create();
                    };
                    break;
                case HashServiceTag.MD5:
                    _createHashAlgorithm = length => MD5.Create();
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
            HashAlgorithm.Create();

            using (HashAlgorithm algorithm = _createHashAlgorithm(count))
            {
                return algorithm.ComputeHash(data, offset, count);
            }
        }

        public byte[] Hash(ArraySegment<byte> data)
        {
            return Hash(data.Array, data.Offset, data.Count);
        }

        public byte[] Hash(Stream stream)
        {
            using (HashAlgorithm algorithm = _createHashAlgorithm(stream.Length))
            {
                return algorithm.ComputeHash(stream);
            }
        }
    }
}
