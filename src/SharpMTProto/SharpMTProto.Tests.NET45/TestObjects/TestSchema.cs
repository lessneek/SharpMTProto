// --------------------------------------------------------------------------------------------------------------------
// <copyright file="TestObjects.cs">
//   Copyright (c) 2013 Alexander Logger. All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using SharpTL;

namespace SharpMTProto.Tests.TestObjects
{
    [TLObject(0x100500)]
    public class TestRequest
    {
        [TLProperty(1)]
        public int TestId { get; set; }
    }

    [TLType(typeof (TestResponse), typeof (TestResponseEx))]
    public interface ITestResponse
    {
        int TestId { get; set; }
    }

    [TLObject(0x500100)]
    public class TestResponse : ITestResponse, IEquatable<TestResponse>
    {
        [TLProperty(1)]
        public int TestId { get; set; }

        [TLProperty(2)]
        public string TestText { get; set; }

        #region Equals
        public bool Equals(TestResponse other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            return TestId == other.TestId && string.Equals(TestText, other.TestText);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((TestResponse) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (TestId*397) ^ (TestText != null ? TestText.GetHashCode() : 0);
            }
        }

        public static bool operator ==(TestResponse left, TestResponse right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TestResponse left, TestResponse right)
        {
            return !Equals(left, right);
        }
        #endregion
    }

    [TLObject(0x500101)]
    public class TestResponseEx : ITestResponse
    {
        [TLProperty(1)]
        public int TestId { get; set; }

        [TLProperty(2)]
        public string TestText { get; set; }

        [TLProperty(3)]
        public string TestText2 { get; set; }
    }

    [TLType(typeof (TestResponse2))]
    public interface ITestResponse2
    {
        string Name { get; set; }

        string Address { get; set; }
        string City { get; set; }
    }

    [TLObject(0x600100)]
    public class TestResponse2 : ITestResponse2
    {
        [TLProperty(1)]
        public string Name { get; set; }

        [TLProperty(2)]
        public string Address { get; set; }

        [TLProperty(3)]
        public string City { get; set; }
    }
}
