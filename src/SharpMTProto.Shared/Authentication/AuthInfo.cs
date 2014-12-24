// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthInfo.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Authentication
{
    /// <summary>
    ///     Auth info contains of auth key and initial salt.
    /// </summary>
    public struct AuthInfo
    {
        public static readonly AuthInfo Empty = new AuthInfo(null, 0);

        public AuthInfo(byte[] authKey, ulong salt) : this()
        {
            AuthKey = authKey;
            Salt = salt;
        }

        public byte[] AuthKey { get; set; }
        public ulong Salt { get; set; }

        public bool HasAuthKey
        {
            get { return AuthKey != null; }
        }
    }
}
