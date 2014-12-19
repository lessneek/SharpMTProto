// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IEncryptionServices.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using System.IO;
    using Authentication;

    public class DHOutParams
    {
        public DHOutParams(byte[] gb, byte[] s)
        {
            GB = gb;
            S = s;
        }

        public byte[] GB { get; set; }
        public byte[] S { get; set; }
    }

    public interface IEncryptionServices
    {
        byte[] RSAEncrypt(byte[] data, PublicKey publicKey);
        DHOutParams DH(byte[] b, byte[] g, byte[] ga, byte[] p);
        void Aes256IgeDecrypt(Stream src, Stream dst, byte[] key, byte[] iv);
        void Aes256IgeEncrypt(Stream src, Stream dst, byte[] key, byte[] iv);
        byte[] Aes256IgeDecrypt(byte[] data, byte[] key, byte[] iv);
        byte[] Aes256IgeEncrypt(byte[] data, byte[] key, byte[] iv);
    }
}
