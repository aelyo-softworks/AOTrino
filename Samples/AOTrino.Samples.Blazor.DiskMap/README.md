# Blazor · DiskMap

A disk usage tool. It scans a drive, shows where the space went, and draws every directory on it as a treemap.

It is also the only sample made of three projects, because it is the only one where the front end is C# too:

| Project | What it is |
| --- | --- |
| `AOTrino.Samples.Blazor.DiskMap` | The host. Native AOT, owns the window, the scanners and the treemap. |
| `AOTrino.Samples.Blazor.DiskMap.Wasm` | The front end. Blazor WebAssembly, the page you see. |
| `AOTrino.Samples.Blazor.DiskMap.Shared` | A shared source project, the C# both sides compile. |

## C# on both sides, without pretending they are the same side

The page runs as WebAssembly inside the WebView. The host runs as native code in the process that owns the window.
They are two runtimes, and they cannot share an assembly: one targets `net10.0` and the other `net10.0-windows`, and a
project reference between them would drag a Windows target framework into a build that compiles to wasm.

A **shared source project** has neither problem, it contributes source rather than a reference. So the DTOs, the size
formatting and the source-generated JSON serializer **are written once and are identical on both sides of the bridge** by
construction rather than by agreement. A front end in another language has to restate every one of them, and keep
them in step by hand, for as long as the app lives.

Be clear about the cost too. The page carries a second .NET runtime (currently Mono) on top of the native one in the host, so the
executable goes from about 11 MB to about 19 MB and startup may be visibly slower than the plain samples. It buys C# in
the page, and that is the only and great thing it buys.

## Two ways to read a drive

The sample offers both, per drive, because they differ in what they can see as well as in how long they take:

* **Normal scan** walks the directory tree. It works on any drive, and a whole system drive takes minutes.
* **Quick scan** reads the NTFS **master file table (MFT)** directly, the volume's own index of every file it holds. The
  same drive takes seconds, around 2.5 million files in about 7.

The quick path is a straight demonstration of what it's like being a real Windows process. It opens `\\.\C:` for raw reading, issues
`FSCTL_GET_NTFS_VOLUME_DATA`, parses record 0 to find where the table's own fragments are, then reads and parses MFT
records, applying the update sequence fixups, to rebuild the tree from the parent reference each record carries.

It also cannot always run, and the app says so rather than hiding it. Reading a volume needs administrator rights, so
the button carries the Windows shield when it would raise a prompt, and clicking it restarts the app elevated through
the shell, which is the one thing a process cannot grant itself. A drive with no master file table, a ReFS Dev Drive
for instance, has the button disabled with the reason written under it.

## The treemap, and why it is not HTML

Every directory on the drive, recursive to the pixel, with each folder's own files as one tile inside it. A real
system drive comes out at tens of thousands of tiles, and the map fills in while the scan is still running.

It is drawn with Direct2D in the host and shown on a `<canvas>` through a shared buffer, and the reason is worth being
precise about, because it is not the one people assume. The drawing is not the hard part, a 2D canvas would manage the
rectangles fine. The problem is that **the tree lives on the native side and is moving**: shipping tens of thousands of
tiles across the bridge several times a second is megabytes per second of pure serialization, of a structure that
scan threads are still modifying. Drawing where the data already is skips all of it, and only finished RGBA pixels cross.

What does cross the bridge is small and dull, which is the point: the folder to show, where the pointer is, and which
theme is on. Hit testing runs in the host too, so a click on the canvas comes back as a path in well under a
millisecond over 35,000 tiles.

## Files worth reading

| File | What is in it |
| --- | --- |
| `Treemap.cs` | The squarified layout, the culling, the brush cache, and why the layout runs off the UI thread. |
| `MftReader.cs` | The master file table, from the volume header to the rebuilt tree. |
| `NtfsVolume.cs` | Opening a volume, checking whether that is even possible, and restarting elevated. |
| `DiskScanner.cs` | The directory walk, the parallel fan out, and the running totals that let the map grow live. |
| `DiskMapApi.cs` | Everything the page can call, which is everything a browser forbids. |

Run it with `dotnet run` from this folder. The front end project is built and embedded as part of building this one,
which is why the solution is told not to build it separately.
