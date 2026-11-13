using System;
using Reinforced.Typings.Fluent;
using Xunit;

namespace Reinforced.Typings.Tests.SpecificCases
{
    [Flags]
    public enum ULongFlags : ulong
    {
        None = 0,
        One = 1,
        High = 1UL << 63,
        Any = ulong.MaxValue
    }

    [Flags]
    public enum SignedFlags : long
    {
        None = 0,
        One = 1,
        Min = long.MinValue,
        All = -1
    }

    public enum SByteValues : sbyte
    {
        Low = sbyte.MinValue,
        Minus = -1,
        High = sbyte.MaxValue
    }

    // Upstream issue #221: ulong values above long.MaxValue threw "Value was either too large or too
    // small for an Int64"; negative values of signed enums must stay negative.
    public partial class SpecificTestCases
    {
        [Fact]
        public void ULongEnumValues()
        {
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment());
                s.ExportAsEnum<ULongFlags>();
            }, @"
module Reinforced.Typings.Tests.SpecificCases {
	export enum ULongFlags {
		None = 0,
		One = 1,
		High = 9223372036854775808,
		Any = 18446744073709551615
	}
}");
        }

        [Fact]
        public void NegativeSignedEnumValues()
        {
            // Members come out in Enum.GetNames order (by unsigned magnitude), as before.
            AssertConfiguration(s =>
            {
                s.Global(a => a.DontWriteWarningComment());
                s.ExportAsEnum<SignedFlags>();
                s.ExportAsEnum<SByteValues>();
            }, @"
module Reinforced.Typings.Tests.SpecificCases {
	export enum SignedFlags {
		None = 0,
		One = 1,
		Min = -9223372036854775808,
		All = -1
	}
	export enum SByteValues {
		High = 127,
		Low = -128,
		Minus = -1
	}
}");
        }
    }
}
