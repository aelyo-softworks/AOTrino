namespace AOTrino.Samples.Blazor.DiskMap;

using AOTrino.Samples.Blazor.DiskMap.Shared;

// the treemap, drawn with Direct2D in the native process and shown on a canvas in the Blazor page.
//
// this is the sample's whole argument, and it only holds because the map is the entire tree rather than one level.
// a single level is a few hundred rectangles, and any front end can draw that. this draws every directory on the drive,
// recursively, down to the point where a tile is smaller than a pixel, which on a real drive is well over a hundred
// thousand tiles, redrawn while a scan is still adding to the tree it reads.
//
// what makes that impossible from the page is not the drawing, a 2D canvas would manage the rectangles.
// it is that the tree lives on this side and is moving: shipping a hundred thousand tiles across the bridge as JSON
// on every frame is megabytes per frame of pure serialization. drawing where the data already is skips all of it,
// and only the finished pixels cross, through a shared buffer, which is memory both sides can see rather than a copy.
//
// the layout is squarified, which keeps tiles close to square. slicing the rectangle in one direction instead
// produces slivers, which at this depth would be a solid smear rather than a map.
public sealed class Treemap(DiskScanner scanner) : IDisposable
{
    // a tile with a side below this is not worth descending into, its children would be thinner than the border around them.
    private const float _minRecurseSide = 14;

    // and one below this is not worth drawing at all.
    private const float _minTileSide = 1.5f;

    // a tile only gets a border once it is big enough for the border not to be most of it.
    private const float _minBorderSide = 5;

    // labels are the expensive part, every one is a DirectWrite layout, so only the tiles that can hold one get one,
    // and only the largest few hundred of those. at full depth the rest are unreadable anyway.
    private const float _minLabelWidth = 58;
    private const float _minLabelHeight = 24;
    private const int _maxLabels = 300;

    private const float _captionHeight = 30;

    // relaying out the whole tree costs more than drawing it, so it happens on a clock rather than on every frame,
    // while the frames in between just redraw the tiles that are already laid out, which is what makes hover cheap.
    private const int _relayoutIntervalMs = 400;

    // a runaway layout must never be able to take the window with it. the tree can be a million directories,
    // and while the culling below bounds the tiles by area, these bound it by construction as well.
    private const int _maxTiles = 120_000;
    private const int _maxDepth = 32;

    // the laid out tiles, replaced wholesale by the layout task and read by the renderer without a lock.
    // an array rather than a list because it is swapped rather than mutated, so a frame either sees the whole
    // previous layout or the whole next one, never a half built one.
    private Tile[] _tiles = [];
    private int _layoutBusy;

    private IComObject<IDWriteTextFormat>? _nameFormat;
    private IComObject<IDWriteTextFormat>? _sizeFormat;
    private IComObject<IDWriteTextFormat>? _captionFormat;

    // brushes are device resources, and creating one per tile is what turns a hundred thousand rectangles
    // from a few milliseconds into a stall. there is one per colour, made once and reused for every frame.
    private readonly Dictionary<int, IComObject<ID2D1SolidColorBrush>> _brushes = [];
    private IComObject<ID2D1RenderTarget>? _target;
    private nint _brushOwner;
    private bool _brushTheme;

    private float _pointerX = -1;
    private float _pointerY = -1;
    private int _width;
    private int _height;
    private string _laidOutPath = string.Empty;
    private int _laidOutWidth;
    private int _laidOutHeight;
    private long _laidOutAt;
    private bool _laidOutRunning;

    // the folder being shown, empty meaning the root of the scan. the page sets this as it navigates,
    // and the treemap resolves it against the live tree, so a scan in progress fills in on screen.
    public string Path { get; set; } = string.Empty;
    public bool IsDark { get; set; } = true;

    // where the pointer is, in normalized coordinates, so the page never has to know the render resolution.
    // the canvas is laid out in CSS pixels and rendered at device pixels, and on a scaled display those differ.
    public void SetPointer(double x, double y)
    {
        _pointerX = x < 0 ? -1 : (float)(x * _width);
        _pointerY = y < 0 ? -1 : (float)(y * _height);
    }

    // the folder a click on the canvas descends into, one level at a time.
    public string? HitTest(double x, double y)
    {
        var px = (float)(x * _width);
        var py = (float)(y * _height);
        var tiles = Volatile.Read(ref _tiles);

        // depth 0 tiles are the immediate children of the folder on show and they cover the whole map,
        // so the first one that contains the point is the folder to open, or the files bucket, which is nowhere to go.
        for (var i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i];
            if (tile.Depth != 0)
                continue;

            if (px >= tile.Rect.left && px <= tile.Rect.right && py >= tile.Rect.top && py <= tile.Rect.bottom)
                return tile.IsBucket ? null : tile.Path;
        }

        return null;
    }

    public void Draw(IComObject<ID2D1RenderTarget> rt, int width, int height, float time)
    {
        _width = width;
        _height = height;

        try
        {
            EnsureFormats();
            EnsureBrushes(rt);

            rt.Clear(Colour(_key_background));

            var node = Resolve();
            if (node == null)
            {
                DrawEmpty(rt, width, height);
                return;
            }

            Relayout(node, width, height);
            DrawTiles(rt);
            DrawCaption(rt, width, height, node);
        }
        catch (Exception ex)
        {
            // the tree is being built by the scan thread while this runs, so a torn read is a dropped frame,
            // not a crash. the next one is 33 ms away.
            AOTrinoApplication.Current?.TraceWarning($"treemap frame: {ex.Message}");
        }
    }

    private ScanNode? Resolve()
    {
        var root = scanner.Root;
        if (root == null)
            return null;

        if (string.IsNullOrEmpty(Path))
            return root;

        return root.Find(Path) ?? root;
    }

    // the layout is rebuilt when what it is showing changes, or on a clock while a scan is still growing the tree.
    // everything else, hover included, redraws the tiles that are already there.
    //
    // it runs off the UI thread, which matters more than it looks. laying out a whole drive is tens of thousands
    // of rectangles over a tree of hundreds of thousands of directories, and the master file table hands that over
    // all at once the moment it finishes. doing that on the UI thread froze the window at exactly that instant,
    // with the page unable to answer a click on Cancel or on its own close button.
    // so the layout is started here and swapped in when it is done, and the frames in between draw the previous one.
    private void Relayout(ScanNode node, int width, int height)
    {
        var running = scanner.GetProgress().IsRunning;
        var now = Environment.TickCount64;
        var stale = running && now - _laidOutAt > _relayoutIntervalMs;
        var changed = width != _laidOutWidth || height != _laidOutHeight || Path != _laidOutPath || running != _laidOutRunning;
        if (!stale && !changed && Volatile.Read(ref _tiles).Length > 0)
            return;

        // one layout at a time, a slow one must not have a queue of others waiting behind it.
        if (Interlocked.CompareExchange(ref _layoutBusy, 1, 0) != 0)
            return;

        _laidOutWidth = width;
        _laidOutHeight = height;
        _laidOutPath = Path;
        _laidOutRunning = running;
        _laidOutAt = now;

        _ = Task.Run(() =>
        {
            try
            {
                var tiles = new List<Tile>(Math.Max(1024, Volatile.Read(ref _tiles).Length));
                var area = new D2D_RECT_F { left = 0, top = 0, right = width, bottom = height - _captionHeight };
                Layout(node, area, 0, -1, tiles);
                Volatile.Write(ref _tiles, [.. tiles]);
            }
            catch (Exception ex)
            {
                // the scan threads are still adding to the tree, so a torn read here costs one layout, not the app.
                AOTrinoApplication.Current?.TraceWarning($"treemap layout: {ex.Message}");
            }
            finally
            {
                Volatile.Write(ref _layoutBusy, 0);
            }
        });
    }

    // one directory laid out into 'area', then every child large enough laid out inside its own tile, all the way down.
    // 'palette' is inherited from the top level, so a tile ten levels deep still carries the colour of the folder it is in.
    private void Layout(ScanNode node, D2D_RECT_F area, int depth, int palette, List<Tile> tiles)
    {
        var width = area.right - area.left;
        var height = area.bottom - area.top;
        if (width < _minTileSide || height < _minTileSide || depth > _maxDepth || tiles.Count >= _maxTiles)
            return;

        // the entry list is per call and the recursion is depth first, so it cannot be a single reused buffer.
        // it is kept small by the culling above, most directories never get here.
        var entries = new List<Entry>();
        long total = 0;
        foreach (var child in node.Children.ToArray())
        {
            var size = child.TotalSize;
            if (size > 0)
            {
                entries.Add(new Entry(child.Name, child.FullPath, size, child));
                total += size;
            }
        }

        // the files sitting directly in this folder are one tile, so the map covers the folder's whole size
        // rather than quietly omitting whatever is not in a subfolder.
        // the top level bucket names the folder it belongs to, since nothing above it does, a deeper one sits
        // inside its labelled parent already, so "36 files" there is not ambiguous.
        if (node.OwnSize > 0)
        {
            var files = node.FileCount == 1 ? "1 file" : $"{node.FileCount:N0} files";
            var name = depth == 0 && !string.IsNullOrEmpty(node.Name) ? $"{files} in {node.Name}" : files;
            entries.Add(new Entry(name, string.Empty, node.OwnSize, null));
            total += node.OwnSize;
        }

        if (entries.Count == 0 || total <= 0)
            return;

        entries.Sort(static (a, b) => b.Size.CompareTo(a.Size));
        Squarify(entries, total, area, depth, palette, tiles);
    }

    // the squarified treemap: fill the rectangle row by row, each row laid along the shorter side,
    // taking entries into the row for as long as that makes the worst aspect ratio in it better rather than worse.
    private void Squarify(List<Entry> entries, long total, D2D_RECT_F area, int depth, int palette, List<Tile> tiles)
    {
        var x = area.left;
        var y = area.top;
        var width = area.right - area.left;
        var height = area.bottom - area.top;
        var remaining = total;

        var index = 0;
        while (index < entries.Count && remaining > 0 && width >= _minTileSide && height >= _minTileSide)
        {
            var alongWidth = width <= height;
            var side = alongWidth ? width : height;
            var areaLeft = (double)width * height;

            long rowSize = 0;
            var count = 0;
            var best = double.MaxValue;
            for (var k = index; k < entries.Count; k++)
            {
                var trySize = rowSize + entries[k].Size;
                if (trySize <= 0)
                    break;

                var thickness = trySize / (double)remaining * areaLeft / side;
                if (thickness <= 0)
                    break;

                var worst = 0.0;
                for (var m = index; m <= k; m++)
                {
                    var length = entries[m].Size / (double)trySize * side;
                    if (length <= 0)
                    {
                        worst = double.MaxValue;
                        break;
                    }

                    var ratio = Math.Max(length / thickness, thickness / length);
                    if (ratio > worst)
                    {
                        worst = ratio;
                    }
                }

                if (worst > best && count > 0)
                    break;

                best = worst;
                rowSize = trySize;
                count++;
            }

            if (count == 0)
                break;

            var rowThickness = (float)(rowSize / (double)remaining * areaLeft / side);
            var position = alongWidth ? x : y;
            for (var m = index; m < index + count; m++)
            {
                var entry = entries[m];
                var length = (float)(entry.Size / (double)rowSize * side);
                var rect = alongWidth
                    ? new D2D_RECT_F { left = position, top = y, right = position + length, bottom = y + rowThickness }
                    : new D2D_RECT_F { left = x, top = position, right = x + rowThickness, bottom = position + length };
                position += length;

                // the colour is chosen once, at the top level, and inherited all the way down,
                // so a deep tile still says which of the big folders it belongs to.
                var tilePalette = depth == 0 ? PaletteOf(entry.Path.Length > 0 ? entry.Path : entry.Name) : palette;

                tiles.Add(new Tile
                {
                    Name = entry.Name,
                    Path = entry.Path,
                    Size = entry.Size,
                    Rect = rect,
                    Palette = tilePalette,
                    Depth = depth,
                    IsBucket = entry.Node == null,
                });

                if (entry.Node == null)
                    continue;

                var tileWidth = rect.right - rect.left;
                var tileHeight = rect.bottom - rect.top;
                if (tileWidth < _minRecurseSide || tileHeight < _minRecurseSide)
                    continue;

                // a tile big enough to be labelled keeps a header strip for the label, and its children go below it.
                // a smaller one is inset by a hairline instead, which is just enough for the parent to read as a frame.
                var header = tileWidth > 70 && tileHeight > 44 ? 17f : 2f;
                var inset = header > 2 ? 3f : 1.5f;
                var inner = new D2D_RECT_F
                {
                    left = rect.left + inset,
                    top = rect.top + header,
                    right = rect.right - inset,
                    bottom = rect.bottom - inset,
                };

                if (inner.right - inner.left >= _minTileSide && inner.bottom - inner.top >= _minTileSide)
                {
                    Layout(entry.Node, inner, depth + 1, tilePalette, tiles);
                }
            }

            if (alongWidth)
            {
                y += rowThickness;
                height -= rowThickness;
            }
            else
            {
                x += rowThickness;
                width -= rowThickness;
            }

            remaining -= rowSize;
            index += count;
        }
    }

    private void DrawTiles(IComObject<ID2D1RenderTarget> rt)
    {
        var tiles = Volatile.Read(ref _tiles);
        var border = Brush(_key_border);
        var hovered = -1;
        var labels = 0;

        for (var i = 0; i < tiles.Length; i++)
        {
            var tile = tiles[i];
            var width = tile.Rect.right - tile.Rect.left;
            var height = tile.Rect.bottom - tile.Rect.top;
            if (width < _minTileSide || height < _minTileSide)
                continue;

            if (_pointerX >= tile.Rect.left && _pointerX <= tile.Rect.right && _pointerY >= tile.Rect.top && _pointerY <= tile.Rect.bottom)
            {
                hovered = i;
            }

            rt.FillRectangle(tile.Rect, Brush(tile.IsBucket ? _key_bucket : Key(tile.Palette, tile.Depth)));

            // a border on a tile a few pixels across is most of the tile, so the small ones go without one
            // and read as texture rather than as noise.
            if (width >= _minBorderSide && height >= _minBorderSide)
            {
                rt.DrawRectangle(tile.Rect, border, 1f);
            }

            if (labels >= _maxLabels || width < _minLabelWidth || height < _minLabelHeight)
                continue;

            labels++;
            var text = new D2D_RECT_F { left = tile.Rect.left + 6, top = tile.Rect.top + 2, right = tile.Rect.right - 4, bottom = tile.Rect.bottom - 2 };
            rt.DrawText(tile.Name, _nameFormat!, text, Brush(_key_text));

            if (height >= 44 && width >= 84)
            {
                text.top += 15;
                rt.DrawText(SizeFormat.Bytes(tile.Size), _sizeFormat!, text, Brush(_key_textDim));
            }
        }

        // the tile under the pointer is drawn again on top, so the highlight is never painted over by a child
        // that was laid out after it.
        if (hovered >= 0)
        {
            var tile = tiles[hovered];
            rt.FillRectangle(tile.Rect, Brush(_key_highlight));
            rt.DrawRectangle(tile.Rect, Brush(_key_hoverBorder), 2f);
        }
    }

    // the strip along the bottom, which says what the pointer is over, or what the whole map adds up to when it is over nothing.
    private void DrawCaption(IComObject<ID2D1RenderTarget> rt, int width, int height, ScanNode node)
    {
        var tiles = Volatile.Read(ref _tiles);
        var hovered = default(Tile);
        var found = false;
        for (var i = tiles.Length - 1; i >= 0 && !found; i--)
        {
            var tile = tiles[i];
            if (_pointerX >= tile.Rect.left && _pointerX <= tile.Rect.right && _pointerY >= tile.Rect.top && _pointerY <= tile.Rect.bottom)
            {
                hovered = tile;
                found = true;
            }
        }

        var caption = found
            ? $"{(string.IsNullOrEmpty(hovered.Path) ? hovered.Name : hovered.Path)}    {SizeFormat.Bytes(hovered.Size)}"
            : $"{(string.IsNullOrEmpty(node.FullPath) ? node.Name : node.FullPath)}    {SizeFormat.Bytes(node.TotalSize > 0 ? node.TotalSize : node.OwnSize)}    {tiles.Length:N0} tiles";

        var rect = new D2D_RECT_F { left = 8, top = height - _captionHeight + 4, right = width - 8, bottom = height };
        rt.DrawText(caption, _captionFormat!, rect, Brush(found ? _key_text : _key_textDim));
    }

    // there is nothing to draw either before the first scan or during a master file table read,
    // which builds its tree only once every record has been read. those are different situations to be in,
    // and inviting someone to start a scan while one is already running reads as the app having missed the click.
    private void DrawEmpty(IComObject<ID2D1RenderTarget> rt, int width, int height)
    {
        var text = scanner.GetProgress().IsRunning ? "reading the volume, the map appears when the tree is built" : "scan a drive to see it here";
        var rect = new D2D_RECT_F { left = 0, top = height / 2f - 12, right = width, bottom = height / 2f + 12 };
        _captionFormat!.Object.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_CENTER);
        rt.DrawText(text, _captionFormat, rect, Brush(_key_textDim));
        _captionFormat.Object.SetTextAlignment(DWRITE_TEXT_ALIGNMENT.DWRITE_TEXT_ALIGNMENT_LEADING);
    }

    private void EnsureFormats()
    {
        if (_nameFormat != null)
            return;

        using var factory = DWriteFunctions.DWriteCreateFactory();
        _nameFormat = factory.CreateTextFormat("Segoe UI", 12f, weight: DWRITE_FONT_WEIGHT.DWRITE_FONT_WEIGHT_SEMI_BOLD);
        _sizeFormat = factory.CreateTextFormat("Segoe UI", 11f);
        _captionFormat = factory.CreateTextFormat("Segoe UI", 12f);

        // a label must never spill out of its tile, and a tile is very often narrower than the name of its folder.
        foreach (var format in new[] { _nameFormat, _sizeFormat, _captionFormat })
        {
            format.Object.SetWordWrapping(DWRITE_WORD_WRAPPING.DWRITE_WORD_WRAPPING_NO_WRAP);
        }
    }

    // one brush per colour, made once. the render target is created once by the surface and reused for every frame,
    // so these outlive the frame, but a theme change or a new target has to throw them away.
    private void EnsureBrushes(IComObject<ID2D1RenderTarget> rt)
    {
        var owner = rt.ToComInstanceNoAddRef();
        if (owner == _brushOwner && _brushTheme == IsDark)
            return;

        foreach (var brush in _brushes.Values)
        {
            brush.Dispose();
        }

        _brushes.Clear();
        _brushOwner = owner;
        _brushTheme = IsDark;
        _target = rt;
    }

    private IComObject<ID2D1SolidColorBrush> Brush(int key)
    {
        if (_brushes.TryGetValue(key, out var brush))
            return brush;

        brush = _target!.CreateSolidColorBrush(Colour(key));
        _brushes[key] = brush;
        return brush;
    }

    private const int _key_background = -1;
    private const int _key_text = -2;
    private const int _key_textDim = -3;
    private const int _key_border = -4;
    private const int _key_hoverBorder = -5;
    private const int _key_bucket = -6;
    private const int _key_highlight = -7;

    // a tile's colour is its inherited palette entry, lightened a step per level, so nesting reads as depth.
    private static int Key(int palette, int depth) => palette * 8 + Math.Min(depth, 7);

    private static int PaletteOf(string name)
    {
        var hash = 0;
        foreach (var c in name)
        {
            hash = hash * 31 + char.ToLowerInvariant(c);
        }

        return (hash & 0x7fffffff) % _paletteDark.Length;
    }

    private D3DCOLORVALUE Colour(int key)
    {
        if (key >= 0)
        {
            var palette = IsDark ? _paletteDark : _paletteLight;
            var colour = palette[key / 8 % palette.Length];
            var depth = key % 8;

            // dark theme lightens with depth, light theme darkens, either way a child stands out from its parent.
            return IsDark ? Mix(colour, 1f, depth * 0.075f) : Mix(colour, 0f, depth * 0.055f);
        }

        return key switch
        {
            _key_background => IsDark ? Rgb(0x18, 0x19, 0x1c, 1) : Rgb(0xf5, 0xf6, 0xf8, 1),
            _key_text => IsDark ? Rgb(0xff, 0xff, 0xff, 0.95f) : Rgb(0x14, 0x17, 0x1a, 0.95f),
            _key_textDim => IsDark ? Rgb(0xe6, 0xe9, 0xee, 0.75f) : Rgb(0x3c, 0x40, 0x43, 0.8f),
            _key_border => IsDark ? Rgb(0x00, 0x00, 0x00, 0.30f) : Rgb(0xff, 0xff, 0xff, 0.55f),
            _key_hoverBorder => IsDark ? Rgb(0xff, 0xff, 0xff, 0.95f) : Rgb(0x14, 0x17, 0x1a, 0.9f),
            _key_bucket => IsDark ? Rgb(0x55, 0x5a, 0x62, 1) : Rgb(0xc8, 0xcc, 0xd2, 1),
            _key_highlight => IsDark ? Rgb(0xff, 0xff, 0xff, 0.22f) : Rgb(0xff, 0xff, 0xff, 0.45f),
            _ => Rgb(0xff, 0x00, 0xff, 1),
        };
    }

    private static readonly D3DCOLORVALUE[] _paletteDark =
    [
        Rgb(0x2f, 0x5d, 0x9e, 1), Rgb(0x2c, 0x7a, 0x6b, 1), Rgb(0x8a, 0x5a, 0x2b, 1), Rgb(0x6b, 0x3f, 0x8a, 1),
        Rgb(0x9e, 0x3f, 0x4f, 1), Rgb(0x3f, 0x6b, 0x2f, 1), Rgb(0x2b, 0x6e, 0x8a, 1), Rgb(0x8a, 0x7a, 0x2b, 1),
    ];

    private static readonly D3DCOLORVALUE[] _paletteLight =
    [
        Rgb(0x9d, 0xc2, 0xf0, 1), Rgb(0x9a, 0xd8, 0xcb, 1), Rgb(0xf0, 0xc9, 0x9a, 1), Rgb(0xcd, 0xb0, 0xe8, 1),
        Rgb(0xf2, 0xaa, 0xb4, 1), Rgb(0xb5, 0xdc, 0xa6, 1), Rgb(0x9c, 0xcf, 0xe8, 1), Rgb(0xe8, 0xdc, 0x9c, 1),
    ];

    private static D3DCOLORVALUE Rgb(int r, int g, int b, float a) => new() { r = r / 255f, g = g / 255f, b = b / 255f, a = a };

    private static D3DCOLORVALUE Mix(D3DCOLORVALUE colour, float towards, float amount) => new()
    {
        r = colour.r + (towards - colour.r) * amount,
        g = colour.g + (towards - colour.g) * amount,
        b = colour.b + (towards - colour.b) * amount,
        a = colour.a,
    };

    public void Dispose()
    {
        foreach (var brush in _brushes.Values)
        {
            brush.Dispose();
        }

        _brushes.Clear();
        _nameFormat?.Dispose();
        _sizeFormat?.Dispose();
        _captionFormat?.Dispose();
    }

    private readonly struct Entry(string name, string path, long size, ScanNode? node)
    {
        public string Name { get; } = name;
        public string Path { get; } = path;
        public long Size { get; } = size;
        public ScanNode? Node { get; } = node;
    }

    private struct Tile
    {
        public string Name;
        public string Path;
        public long Size;
        public D2D_RECT_F Rect;
        public int Palette;
        public int Depth;
        public bool IsBucket;
    }
}
