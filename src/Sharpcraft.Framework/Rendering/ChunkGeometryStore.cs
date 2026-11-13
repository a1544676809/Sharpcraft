// SPDX-License-Identifier: LGPL-3.0-or-later
// Copyright (C) 2026 Sharpcraft contributors

using System.Numerics;
using System.Runtime.InteropServices;
using Brew.Wgpu;
using Brew.Wgpu.Native;

namespace Sharpcraft.Framework.Rendering;

/// <summary>
/// Per-layer chunk geometry in a small number of shared pages: one vertex buffer and one index buffer per page, drawn with
/// index and vertex offsets instead of one buffer pair per chunk (ADR-0029).
/// </summary>
/// <remarks>
/// <para>
/// Each chunk keeps an interval in a page. Drawing binds the page once and then issues one
/// <c>DrawIndexed(indexCount, 1, firstIndex, baseVertex, 0)</c> per chunk, so the number of buffer bindings follows the
/// number of pages rather than the number of chunks. The index data is uploaded verbatim: the mesher writes indices
/// relative to the chunk's own vertices, and <c>baseVertex</c> shifts them to the page slot at draw time. Layout,
/// derivation, growth and unload rules are specified in docs/analysis/architecture/render-backend-seam.md 9.28.
/// </para>
/// <para>
/// Pages keep ADR-0020's purpose intact: memory follows the resident meshes instead of a single buffer ceiling, and a chunk
/// that leaves the view only returns its intervals (coalescing with neighbours) rather than triggering a rebuild.
/// </para>
/// </remarks>
public sealed class ChunkGeometryStore : IDisposable
{
    private sealed class Page
    {
        public required BufferHandle Vertex;
        public required BufferHandle Index;
        public required long VertexCapacity;
        public required long IndexCapacity;
        public required GeometryPageFreeList VertexFree;
        public required GeometryPageFreeList IndexFree;
        public required bool Oversized;
        public List<long> Chunks { get; } = [];

        /// <summary>Bytes held by this page's two buffers.</summary>
        public long MemoryBytes => VertexCapacity + IndexCapacity;
    }

    private sealed class Entry
    {
        public required Page Page;
        public required long VertexOffset;
        public required long VertexBytes;
        public required long IndexOffset;
        public required uint IndexCount;
        public required Vector3 Minimum;
        public required Vector3 Maximum;

        // Per section index ranges when the mesher recorded them: starts[i]..starts[i + 1] is section i and the
        // last entry is the total index count. Null keeps the whole column behaviour.
        public int[]? SectionStarts;

        public int SectionMinY;
    }

    private readonly IRenderBackend _backend;
    private readonly int _bytesPerVertex;
    private readonly long _pageBytes;
    private readonly long _indexPageBytes;
    private readonly Dictionary<long, Entry> _entries = [];
    /// <summary>Size of one indexed draw argument record: index count, instance count, first index, base vertex, first instance.</summary>
    private const int IndirectArgumentBytes = 20;

    private readonly List<(Page Page, int First, int Count)> _segments = [];
    private BufferHandle _argsBuffer;
    private int _argsCapacity;
    private uint[] _args = [];
    private int _argsCount;
    private readonly List<Page> _pages = [];


    /// <summary>Creates an empty store.</summary>
    /// <param name="backend">Backend that owns the buffers.</param>
    /// <param name="device">Device the pages are created on.</param>
    /// <param name="bytesPerVertex">Vertex stride, also the alignment of a chunk's vertex range.</param>
    /// <param name="pageBytes">Capacity of one page's vertex and index buffer. Defaults to 4 MiB.</param>
    public ChunkGeometryStore(IRenderBackend backend, Device device, int bytesPerVertex = VertexLayout.Stride, long pageBytes = 4L * 1024 * 1024)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerVertex);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageBytes);
        _backend = backend;
        _bytesPerVertex = bytesPerVertex;
        _pageBytes = pageBytes;
        // A chunk's index range is about a tenth of its vertex range (56 byte vertices against 4 byte indices), so an
        // index buffer as large as the vertex buffer would be mostly slack. Both stay aligned with the vertex page.
        _indexPageBytes = Math.Max(pageBytes / 8, 256 * 1024);
    }

    /// <summary>Chunks currently resident.</summary>
    public int ChunkCount => _entries.Count;

    /// <summary>Bytes uploaded since creation (diagnostic).</summary>
    public long UploadedBytes { get; private set; }

    /// <summary>Bytes held by the pages, including the free space inside them (diagnostic).</summary>
    public long MemoryBytes { get; private set; }

    /// <summary>Bytes actually covered by resident geometry, excluding page slack (diagnostic).</summary>
    public long LiveBytes { get; private set; }

    /// <summary>Pages currently allocated (diagnostic).</summary>
    public int PageCount => _pages.Count;

    /// <summary>Chunks skipped by the last draw's frustum test (diagnostic).</summary>
    public int CulledLastFrame { get; private set; }

    /// <summary>Cumulative DrawIndexed calls issued by this store (diagnostic).</summary>
    public long DrawCallsTotal { get; private set; }

    /// <summary>Cumulative vertex and index buffer bindings, the cost this layout removes (diagnostic).</summary>
    public long BindCallsTotal { get; private set; }

    /// <summary>Buffer bindings issued by the last draw call (diagnostic).</summary>
    public int BindCallsLastDraw { get; private set; }

    /// <summary>Chunks the last draw actually issued a draw for (diagnostic).</summary>
    public int DrawnLastDraw { get; private set; }

    /// <summary>Triangles the last draw submitted, which is the geometry the GPU had to process (diagnostic).</summary>
    public long TrianglesLastDraw { get; private set; }

    /// <summary>Pages never presized: pages are created on demand (diagnostic).</summary>
    public bool Presized => false;

    /// <summary>No whole-world rebuild ever happens; unload only returns intervals (diagnostic).</summary>
    public int FullRebuilds => 0;

    /// <summary>Uploads one chunk's geometry, replacing whatever that chunk had.</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    /// <param name="mesh">Vertices and indices for that chunk.</param>
    public void Apply(long chunkKey, GeometryData mesh)
    {
        if (mesh.Vertices.Length == 0)
        {
            Remove(chunkKey);
            return;
        }

        var vertexBytes = (long)mesh.Vertices.Length * sizeof(float);
        var indexCount = mesh.Indices.Length;
        var indexBytes = (long)indexCount * sizeof(uint);

        if (_entries.TryGetValue(chunkKey, out var existing)
            && existing.VertexBytes == vertexBytes
            && existing.IndexCount == (uint)indexCount)
        {
            // Same shape: rewrite both ranges where they are. A block edit that keeps the mesh size is the common case.
            _backend.WriteBuffer(existing.Page.Vertex, System.Runtime.InteropServices.MemoryMarshal.AsBytes(mesh.Vertices.AsSpan()), (ulong)existing.VertexOffset);
            _backend.WriteBuffer(existing.Page.Index, System.Runtime.InteropServices.MemoryMarshal.AsBytes(mesh.Indices.AsSpan()), (ulong)existing.IndexOffset * sizeof(uint));
            UploadedBytes += vertexBytes + indexBytes;
            return;
        }

        Remove(chunkKey);

        var page = ReservePage(vertexBytes, indexBytes, out var vertexOffset, out var indexOffset);
        _backend.WriteBuffer(page.Vertex, System.Runtime.InteropServices.MemoryMarshal.AsBytes(mesh.Vertices.AsSpan()), (ulong)vertexOffset);
        _backend.WriteBuffer(page.Index, System.Runtime.InteropServices.MemoryMarshal.AsBytes(mesh.Indices.AsSpan()), (ulong)indexOffset * sizeof(uint));

        var (minimum, maximum) = BoundsOf(mesh);
        _entries[chunkKey] = new Entry
        {
            Page = page,
            VertexOffset = vertexOffset,
            VertexBytes = vertexBytes,
            IndexOffset = indexOffset,
            IndexCount = (uint)indexCount,
            Minimum = minimum,
            Maximum = maximum,
            SectionStarts = mesh.SectionStarts,
            SectionMinY = mesh.SectionMinY
        };
        page.Chunks.Add(chunkKey);
        LiveBytes += vertexBytes + indexBytes;
        UploadedBytes += vertexBytes + indexBytes;
    }

    /// <summary>Releases one chunk's ranges (it left the view).</summary>
    /// <param name="chunkKey">Packed chunk key.</param>
    public void Remove(long chunkKey)
    {
        if (!_entries.Remove(chunkKey, out var gone))
        {
            return;
        }

        gone.Page.VertexFree.Release(gone.VertexOffset, gone.VertexBytes);
        gone.Page.IndexFree.Release(gone.IndexOffset * sizeof(uint), (long)gone.IndexCount * sizeof(uint));
        gone.Page.Chunks.Remove(chunkKey);
        LiveBytes -= gone.VertexBytes + ((long)gone.IndexCount * sizeof(uint));

        // A page that no longer holds anything is destroyed, except the most recent one: keeping it avoids destroying and
        // recreating a page every time the last chunk of a frame leaves and a new one arrives.
        if (gone.Page.Chunks.Count == 0 && (_pages.Count > 1 || gone.Page.Oversized))
        {
            DestroyPage(gone.Page);
        }
    }

    /// <summary>Draws the resident chunks, skipping any whose bounds fall outside the frustum.</summary>
    /// <param name="pass">Pass to record into.</param>
    public void Draw(RenderPassHandle pass, ReadOnlySpan<Vector4> planes, Func<long, ulong>? visibleSections = null)
    {
        CulledLastFrame = 0;
        BindCallsLastDraw = 0;
        DrawnLastDraw = 0;
        TrianglesLastDraw = 0;
        _argsCount = 0;
        _segments.Clear();

        // Collect before issuing: every chunk of a page draws out of that page's buffers, so the whole page's argument
        // records go into one upload and become one multi draw. Record order is draw order, which keeps the translucent
        // layer's back to front sequence whatever the batching does.
        foreach (var page in _pages)
        {
            var first = _argsCount;
            foreach (var key in page.Chunks)
            {
                if (!_entries.TryGetValue(key, out var entry))
                {
                    continue;
                }

                if (planes.Length >= 6 && !FrustumPlanes.IsVisible(planes, entry.Minimum, entry.Maximum))
                {
                    CulledLastFrame++;
                    continue;
                }

                if (entry.SectionStarts is { Length: > 1 } starts)
                {
                    DrawSections(pass, entry, starts, planes, key, visibleSections);
                    continue;
                }

                AppendDraw(entry.IndexCount, (uint)entry.IndexOffset, (int)(entry.VertexOffset / _bytesPerVertex));
                DrawnLastDraw++;
                TrianglesLastDraw += entry.IndexCount / 3;
            }

            if (_argsCount > first)
            {
                _segments.Add((page, first, _argsCount - first));
            }
        }

        if (_segments.Count == 0)
        {
            return;
        }

        _backend.WriteBuffer(_argsBuffer, MemoryMarshal.AsBytes(_args.AsSpan(0, _argsCount * 5)), 0);
        foreach (var segment in _segments)
        {
            BindPage(pass, segment.Page);
            _backend.MultiDrawIndexedIndirect(pass, _argsBuffer, (ulong)segment.First * IndirectArgumentBytes, (uint)segment.Count);
            DrawCallsTotal += segment.Count;
        }
    }

    /// <summary>Appends one indexed draw argument record, growing the upload buffer only when it no longer fits.</summary>
    private void AppendDraw(uint indexCount, uint firstIndex, int baseVertex)
    {
        if (_args.Length < (_argsCount + 1) * 5)
        {
            Array.Resize(ref _args, Math.Max((_argsCount + 1) * 5, _args.Length * 2));
        }

        if (_argsCapacity < _argsCount + 1)
        {
            var capacity = Math.Max(_argsCount + 1, _argsCapacity * 2);
            if (_argsBuffer.Value is not null)
            {
                _backend.Destroy(_argsBuffer);
            }

            _argsBuffer = _backend.CreateBuffer(
                new BufferDesc((ulong)capacity * IndirectArgumentBytes, BufferUsages.Indirect | BufferUsages.CopyDst));
            _argsCapacity = capacity;
        }

        var at = _argsCount * 5;
        _args[at] = indexCount;
        _args[at + 1] = 1;
        _args[at + 2] = firstIndex;
        _args[at + 3] = (uint)baseVertex;
        _args[at + 4] = 0;
        _argsCount++;
    }

    /// <summary>Draws all resident chunks (no culling).</summary>
    /// <param name="pass">Pass to record into.</param>
    public void Draw(RenderPassHandle pass) => Draw(pass, default);

    /// <summary>Binds a page's vertex and index buffer for drawing.</summary>
    /// <param name="pass">Pass to record into.</param>
    /// <param name="page">Page to bind.</param>
    /// <summary>Submits the sections of one column that are inside the frustum.</summary>
    /// <param name="pass">Pass to record into.</param>
    /// <param name="entry">The column being drawn.</param>
    /// <param name="starts">Per section index starts, with the total index count last.</param>
    /// <param name="planes">Frustum planes, or an empty span to draw everything.</param>
    /// <remarks>
    /// A column spans the whole world height, so testing its box lets twenty four sections through whenever any part
    /// of the column is in view. Testing each section on its own and then merging the runs that survive keeps the
    /// draw count close to what it was while submitting far less geometry.
    /// </remarks>
    private void DrawSections(RenderPassHandle pass, Entry entry, int[] starts, ReadOnlySpan<Vector4> planes, long key, Func<long, ulong>? visibleSections)
    {
        var baseVertex = (int)(entry.VertexOffset / _bytesPerVertex);
        var sectionCount = starts.Length - 1;

        if (planes.Length < 6)
        {
            DrawSectionRun(pass, entry, starts, 0, sectionCount, baseVertex);
            return;
        }

        // The frustum cuts the column's tall band down to a single interval: every plane whose normal has a vertical
        // component bounds the bottom or the top, and planes with no vertical component either keep the whole band or
        // rule the column out. So the sections that survive are always contiguous and the column still costs one draw,
        // which is what makes this cheaper than testing twenty four boxes per column.
        var low = (float)entry.SectionMinY;
        var high = entry.SectionMinY + (sectionCount * 16f);
        for (var index = 0; index < 6; index++)
        {
            var plane = planes[index];
            var horizontal = (plane.X >= 0f ? plane.X * entry.Maximum.X : plane.X * entry.Minimum.X)
                + (plane.Z >= 0f ? plane.Z * entry.Maximum.Z : plane.Z * entry.Minimum.Z)
                + plane.W;

            if (plane.Y > 1e-6f)
            {
                // The top corner is the furthest along this normal, so a section below this height cannot reach it.
                low = MathF.Max(low, -horizontal / plane.Y);
            }
            else if (plane.Y < -1e-6f)
            {
                // The bottom corner decides, so a section above this height cannot reach it either.
                high = MathF.Min(high, -horizontal / plane.Y);
            }
            else if (horizontal < 0f)
            {
                return;
            }
        }

        if (low >= high)
        {
            return;
        }

        // One section of slack on each side: an off by one here would drop geometry, and a section that is drawn but
        // empty costs nothing.
        var first = (int)MathF.Floor((low - entry.SectionMinY) / 16f) - 1;
        var last = (int)MathF.Floor((high - entry.SectionMinY) / 16f) + 1;
        first = Math.Max(0, first);
        last = Math.Min(sectionCount - 1, last);

        if (last < first)
        {
            return;
        }

        if (visibleSections is null)
        {
            DrawSectionRun(pass, entry, starts, first, last + 1, baseVertex);
            return;
        }

        // One lookup per chunk rather than one per section: the mask already says which sections the walk kept.
        var mask = visibleSections(key);
        if (mask == 0UL)
        {
            CulledLastFrame++;
            return;
        }

        // Occlusion does not respect the band's contiguity: a cave under a solid floor can be reachable while the rock
        // between it and the camera is not, so the band is split into runs of sections the traversal kept (T-833).
        var runStart = -1;
        for (var section = first; section <= last; section++)
        {
            if ((mask & (1UL << section)) != 0)
            {
                if (runStart < 0)
                {
                    runStart = section;
                }

                continue;
            }

            if (runStart >= 0)
            {
                DrawSectionRun(pass, entry, starts, runStart, section, baseVertex);
                runStart = -1;
            }
        }

        if (runStart >= 0)
        {
            DrawSectionRun(pass, entry, starts, runStart, last + 1, baseVertex);
        }
    }

    /// <summary>Submits one contiguous run of visible sections as a single draw.</summary>
    private void DrawSectionRun(RenderPassHandle pass, Entry entry, int[] starts, int firstSection, int endSection, int baseVertex)
    {
        var from = starts[firstSection];
        var to = starts[endSection];
        if (to <= from)
        {
            return;
        }

        AppendDraw((uint)(to - from), (uint)(entry.IndexOffset + from), baseVertex);
        DrawnLastDraw++;
        TrianglesLastDraw += (to - from) / 3;
    }

    private void BindPage(RenderPassHandle pass, Page page)
    {
        _backend.SetVertexBuffer(pass, 0, page.Vertex);
        _backend.SetIndexBuffer(pass, page.Index, IndexFormat.Uint32);
        BindCallsTotal += 2;
        BindCallsLastDraw += 2;
    }

    /// <summary>Finds room for a chunk's two ranges, creating a page when none has room for both.</summary>
    /// <param name="vertexBytes">Vertex range size.</param>
    /// <param name="indexBytes">Index range size.</param>
    /// <param name="vertexOffset">Byte offset the vertex range starts at.</param>
    /// <param name="indexOffset">Index offset the index range starts at.</param>
    /// <returns>The page the ranges were taken from.</returns>
    private Page ReservePage(long vertexBytes, long indexBytes, out long vertexOffset, out long indexOffset)
    {
        foreach (var page in _pages)
        {
            if (page.Oversized)
            {
                continue;
            }

            var vertex = page.VertexFree.Allocate(vertexBytes, _bytesPerVertex);
            if (vertex is null)
            {
                continue;
            }

            var index = page.IndexFree.Allocate(indexBytes, sizeof(uint));
            if (index is null)
            {
                page.VertexFree.Release(vertex.Value, vertexBytes);
                continue;
            }

            vertexOffset = vertex.Value;
            indexOffset = index.Value / sizeof(uint);
            return page;
        }

        var oversized = vertexBytes > _pageBytes || indexBytes > _indexPageBytes;
        var vertexCapacity = oversized ? Math.Max(AlignUp(vertexBytes, 4096), 4096) : _pageBytes;
        var indexCapacity = oversized ? Math.Max(AlignUp(indexBytes, 4096), 4096) : _indexPageBytes;

        var created = new Page
        {
            Vertex = _backend.CreateBuffer(new BufferDesc((ulong)vertexCapacity, BufferUsages.Vertex | BufferUsages.CopyDst)),
            Index = _backend.CreateBuffer(new BufferDesc((ulong)indexCapacity, BufferUsages.Index | BufferUsages.CopyDst)),
            VertexCapacity = vertexCapacity,
            IndexCapacity = indexCapacity,
            VertexFree = new GeometryPageFreeList(vertexCapacity),
            IndexFree = new GeometryPageFreeList(indexCapacity),
            Oversized = oversized
        };

        _pages.Add(created);
        MemoryBytes += created.MemoryBytes;

        var vertexSlot = created.VertexFree.Allocate(vertexBytes, _bytesPerVertex);
        var indexPath = created.IndexFree.Allocate(indexBytes, sizeof(uint));
        if (vertexSlot is null || indexPath is null)
        {
            throw new InvalidOperationException("a fresh page could not take the geometry it was created for");
        }

        vertexOffset = vertexSlot.Value;
        indexOffset = indexPath.Value / sizeof(uint);
        return created;
    }

    /// <summary>Destroys a page and forgets it.</summary>
    /// <param name="page">Page to destroy.</param>
    private void DestroyPage(Page page)
    {
        _pages.Remove(page);
        MemoryBytes -= page.MemoryBytes;
        _backend.Destroy(page.Vertex);
        _backend.Destroy(page.Index);
    }

    /// <summary>Computes the position bounds of a chunk mesh.</summary>
    /// <param name="mesh">Geometry to scan.</param>
    /// <returns>Minimum and maximum corner.</returns>
    private (Vector3 Minimum, Vector3 Maximum) BoundsOf(GeometryData mesh)
    {
        var minimum = new Vector3(float.MaxValue);
        var maximum = new Vector3(float.MinValue);
        var floatsPerVertex = _bytesPerVertex / sizeof(float);
        for (var i = 0; i + 2 < mesh.Vertices.Length; i += floatsPerVertex)
        {
            var position = new Vector3(mesh.Vertices[i], mesh.Vertices[i + 1], mesh.Vertices[i + 2]);
            minimum = Vector3.Min(minimum, position);
            maximum = Vector3.Max(maximum, position);
        }

        return (minimum, maximum);
    }

    /// <summary>Rounds a size up to a multiple.</summary>
    /// <param name="value">Size to round.</param>
    /// <param name="multiple">Multiple to round to.</param>
    /// <returns>The rounded size.</returns>
    private static long AlignUp(long value, long multiple) =>
        value % multiple == 0 ? value : value + (multiple - (value % multiple));

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var page in _pages)
        {
            _backend.Destroy(page.Vertex);
            _backend.Destroy(page.Index);
        }

        _pages.Clear();
        _entries.Clear();
        MemoryBytes = 0;
        LiveBytes = 0;
    }
}
