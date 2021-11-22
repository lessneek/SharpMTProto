// --------------------------------------------------------------------------------------------------------------------
// <copyright file="AuthInfo.cs">
//   Copyright (c) 2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto.Authentication
{
    public interface IAuthInfo
    {
        byte[] AuthKey { get; set; }
        ulong Salt { get; set; }
        bool HasAuthKey { get; }
    }

    /// <summary>
    ///     Auth info contains of auth key and initial salt.
    /// </summary>
    public class AuthInfo : IAuthInfo
    {
        public AuthInfo(byte[] authKey = null, ulong salt = 0)
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
