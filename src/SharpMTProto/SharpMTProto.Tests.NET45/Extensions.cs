// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Extensions.cs">
//   Copyright (c) 2013-2014 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using SharpTL;

namespace SharpMTProto.Tests
{
    public static class Extensions
    {
        public static T CloneTLObject<T>(this T obj)
        {
            byte[] bytes = TLRig.Default.Serialize(obj);
            var clone = TLRig.Default.Deserialize<T>(bytes);
            return clone;
        }
    }
}
