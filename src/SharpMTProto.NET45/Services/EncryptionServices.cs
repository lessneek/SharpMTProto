// --------------------------------------------------------------------------------------------------------------------
// <copyright file="EncryptionServices.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Services
{
    using System;
    using System.IO;
    using System.Security.Cryptography;

    public partial class EncryptionServices
    {
        public byte[] Aes256IgeDecrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var src = new MemoryStream(data))
            using (var dst = new MemoryStream(data.Length))
            {
                Aes256IgeDecrypt(src, dst, key, iv);
                return dst.ToArray();
            }
        }

        public void Aes256IgeDecrypt(Stream src, Stream dst, byte[] key, byte[] iv)
        {
            var iv1 = new byte[iv.Length/2];
            var iv2 = new byte[iv.Length/2];
            Buffer.BlockCopy(iv, 0, iv1, 0, iv1.Length);
            Buffer.BlockCopy(iv, iv.Length/2, iv2, 0, iv2.Length);

            using (var aes = new AesManaged())
            {
                aes.Mode = CipherMode.ECB;
                aes.KeySize = key.Length*8;
                aes.Padding = PaddingMode.None;
                aes.IV = iv1;
                aes.Key = key;

                int blockSize = aes.BlockSize/8;

                var xPrev = new byte[blockSize];
                Buffer.BlockCopy(iv1, 0, xPrev, 0, blockSize);
                var yPrev = new byte[blockSize];
                Buffer.BlockCopy(iv2, 0, yPrev, 0, blockSize);

                var x = new byte[blockSize];
                ICryptoTransform decryptor = aes.CreateDecryptor();

                while (src.Read(x, 0, blockSize) == blockSize)
                {
                    byte[] y = Xor(decryptor.TransformFinalBlock(Xor(x, yPrev), 0, blockSize), xPrev);

                    Buffer.BlockCopy(x, 0, xPrev, 0, blockSize);
                    Buffer.BlockCopy(y, 0, yPrev, 0, blockSize);

                    dst.Write(y, 0, y.Length);
                }
            }
        }

        public byte[] Aes256IgeEncrypt(byte[] data, byte[] key, byte[] iv)
        {
            using (var src = new MemoryStream(data))
            using (var dst = new MemoryStream(data.Length))
            {
                Aes256IgeEncrypt(src, dst, key, iv);
                return dst.ToArray();
            }
        }

        public void Aes256IgeEncrypt(Stream src, Stream dst, byte[] key, byte[] iv)
        {
            var iv1 = new byte[iv.Length/2];
            var iv2 = new byte[iv.Length/2];
            Buffer.BlockCopy(iv, 0, iv1, 0, iv1.Length);
            Buffer.BlockCopy(iv, iv.Length/2, iv2, 0, iv2.Length);

            using (var aes = new AesManaged())
            {
                aes.Mode = CipherMode.ECB;
                aes.KeySize = key.Length*8;
                aes.Padding = PaddingMode.None;
                aes.IV = iv1;
                aes.Key = key;

                int blockSize = aes.BlockSize/8;

                var xPrev = new byte[blockSize];
                Buffer.BlockCopy(iv2, 0, xPrev, 0, blockSize);
                var yPrev = new byte[blockSize];
                Buffer.BlockCopy(iv1, 0, yPrev, 0, blockSize);

                var x = new byte[blockSize];
                ICryptoTransform encryptor = aes.CreateEncryptor();

                while (src.Read(x, 0, blockSize) == blockSize)
                {
                    byte[] y = Xor(encryptor.TransformFinalBlock(Xor(x, yPrev), 0, blockSize), xPrev);

                    Buffer.BlockCopy(x, 0, xPrev, 0, blockSize);
                    Buffer.BlockCopy(y, 0, yPrev, 0, blockSize);

                    dst.Write(y, 0, y.Length);
                }
            }
        }
    }
}
