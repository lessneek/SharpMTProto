﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Exceptions.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace SharpMTProto
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Schema;
    using Transport;

    public class MTProtoException : Exception
    {
        public MTProtoException()
        {
        }

        public MTProtoException(string message) : base(message)
        {
        }

        public MTProtoException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class InvalidResponseException : MTProtoException
    {
        public InvalidResponseException()
        {
        }

        public InvalidResponseException(string message) : base(message)
        {
        }

        public InvalidResponseException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class InvalidMessageException : MTProtoException
    {
        public InvalidMessageException()
        {
        }

        public InvalidMessageException(string message) : base(message)
        {
        }

        public InvalidMessageException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class CouldNotConnectException : MTProtoException
    {
        public CouldNotConnectException()
        {
        }

        public CouldNotConnectException(string message, TransportConnectResult result) : base(message)
        {
        }

        public CouldNotConnectException(string message, TransportConnectResult result, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public class PublicKeyNotFoundException : MTProtoException
    {
        public PublicKeyNotFoundException(ulong fingerprint)
            : base(string.Format("Public key with fingerprint {0:X16} not found.", fingerprint))
        {
        }

        public PublicKeyNotFoundException(IEnumerable<ulong> fingerprints) : base(GetMessage(fingerprints))
        {
        }

        private static string GetMessage(IEnumerable<ulong> fingerprints)
        {
            var sb = new StringBuilder();
            sb.Append("There are no keys found with corresponding fingerprints: ");
            foreach (ulong fingerprint in fingerprints)
            {
                sb.Append(string.Format("0x{0:X16}", fingerprint));
            }
            sb.Append(".");
            return sb.ToString();
        }
    }

    public class TransportException : MTProtoException
    {
        public TransportException()
        {
        }

        public TransportException(string message) : base(message)
        {
        }

        public TransportException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class InvalidAuthKeyException : MTProtoException
    {
        public InvalidAuthKeyException()
        {
        }

        public InvalidAuthKeyException(string message) : base(message)
        {
        }

        public InvalidAuthKeyException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public class RpcErrorException : MTProtoException
    {
        public RpcErrorException(uint code, string type, string description = null)
        {
            Error = new RpcError {ErrorCode = code, ErrorMessage = type};
            Description = description;
        }

        public RpcErrorException(IRpcError error) : base(string.Format("Error [{0}]: '{1}'.", error.ErrorCode, error.ErrorMessage))
        {
            Error = error;
        }

        public IRpcError Error { get; private set; }

        public string Description { get; private set; }
    }

    public class MTProtoErrorException : MTProtoException
    {
        public MTProtoErrorException(int error)
            : base(string.Format("Received error code: {0}.", error))
        {
        }
    }
}
