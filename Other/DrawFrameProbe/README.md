# DrawFrame GDI resource regression probe

On Windows, from the repository root:

```powershell
dotnet run --project Other/DrawFrameProbe -c Debug -- 1000
```

The probe creates its own hidden native window and invokes the production
`Other.DrawFrame` method repeatedly. It measures process GDI objects with
`GetGuiResources`, after ten warm-up calls. A separate live DC validates that
the counter can see allocated resources. Exit code 0 means no accumulation;
exit code 1 means the GDI count increased.

No user input, screen recording, or WPF rendering loop is involved. The tool
does not reproduce or assert anything about UI hangs.

Measured on Windows with .NET SDK 10.0.400 and .NET runtime 10.0.11:

| Renderer | Calls measured | GDI before | GDI after | Delta | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Before fix | 1,000 | 10 | 1,010 | +1,000 | FAIL |
| With fix | 1,000 | 0 | 0 | 0 | PASS |
| With fix | 10,000 | 0 | 0 | 0 | PASS |

For a before/after comparison, keep this probe present, use the parent revision
of `ScreenToGif.Native/Helpers/Other.cs`, rebuild, and run it with 1,000 calls.
Then restore the fixed file, rebuild, and run again. Explicitly rebuild when
copying source files, since preserved file timestamps can fool incremental builds:

```powershell
dotnet build Other/DrawFrameProbe -c Debug -t:Rebuild
dotnet run --project Other/DrawFrameProbe -c Debug --no-build -- 1000
```

The fix pairs each acquired window DC with `ReleaseDC` in `finally`, as required
by the [GetWindowDC documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowdc).
