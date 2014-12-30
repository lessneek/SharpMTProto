// --------------------------------------------------------------------------------------------------------------------
// <copyright file="IHashService.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.IO;

namespace SharpMTProto.Services
{
    public enum HashServiceTag
    {
        SHA1,
        MD5
    }

    public interface IHashServiceProvider
    {
        IHashService Create(HashServiceTag tag);
    }

    public interface IHashService
    {
        byte[] Hash(byte[] data);
        byte[] Hash(byte[] data, int offset, int count);
        byte[] Hash(ArraySegment<byte> data);
        byte[] Hash(Stream stream);
    }
}
