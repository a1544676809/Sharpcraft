// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.Nbt;

/// <summary>NBT tag ids as used on the wire.</summary>
public enum NbtTagType : byte
{
    /// <summary>End of compound or list terminator.</summary>
    End = 0,

    /// <summary>Signed 8-bit.</summary>
    Byte = 1,

    /// <summary>Signed 16-bit.</summary>
    Short = 2,

    /// <summary>Signed 32-bit.</summary>
    Int = 3,

    /// <summary>Signed 64-bit.</summary>
    Long = 4,

    /// <summary>32-bit float.</summary>
    Float = 5,

    /// <summary>64-bit float.</summary>
    Double = 6,

    /// <summary>Length-prefixed byte array.</summary>
    ByteArray = 7,

    /// <summary>UTF-8 string.</summary>
    String = 8,

    /// <summary>Homogeneous list.</summary>
    List = 9,

    /// <summary>Named tag map.</summary>
    Compound = 10,

    /// <summary>Length-prefixed int array.</summary>
    IntArray = 11,

    /// <summary>Length-prefixed long array.</summary>
    LongArray = 12
}
