// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

namespace Sharpcraft.Common.World;

/// <summary>
/// One block entity as it appears in the chunk packet, kept for the render stage.
/// </summary>
/// <param name="LocalX">Block X inside the chunk, zero to fifteen.</param>
/// <param name="Y">Absolute block Y.</param>
/// <param name="LocalZ">Block Z inside the chunk, zero to fifteen.</param>
/// <param name="TypeId">Registry id of the block entity type.</param>
public readonly record struct BlockEntityRecord(int LocalX, int Y, int LocalZ, int TypeId);
