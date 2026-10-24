using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ScreenToGif.Native.External;
using ScreenToGif.Native.Helpers;

internal static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateWindowEx(uint exStyle, string className, string windowName,
        uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr process, uint flags);

    private static int Main(string[] args)
    {
        var iterations = args.Length == 0 ? 1000 : int.Parse(args[0]);
        if (iterations < 1)
            throw new ArgumentOutOfRangeException(nameof(iterations));

        // A private, hidden native window keeps the probe independent of user input and WPF rendering.
        var window = CreateWindowEx(0, "STATIC", "DrawFrame probe", 0, 0, 0, 640, 480,
            IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (window == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            using var process = Process.GetCurrentProcess();

            // Warm up the real renderer before measuring process-wide GDI usage.
            for (var index = 0; index < 10; index++)
                Other.DrawFrame(window, 1);

            // Validate the counter with a live DC; zero is a valid count once all DCs are released.
            var dc = User32.GetWindowDC(window);
            if (dc == IntPtr.Zero)
                throw new InvalidOperationException("Could not acquire a window DC.");
            try
            {
                if (GetGuiResources(process.Handle, 0) == 0)
                    throw new InvalidOperationException("GDI resource counter did not detect the live DC.");
            }
            finally
            {
                User32.ReleaseDC(window, dc);
            }

            var before = GetGuiResources(process.Handle, 0);
            for (var index = 0; index < iterations; index++)
                Other.DrawFrame(window, 1);
            var after = GetGuiResources(process.Handle, 0);
            var delta = (long)after - before;

            Console.WriteLine($"DrawFrame calls: {iterations}");
            Console.WriteLine($"GDI objects before: {before}");
            Console.WriteLine($"GDI objects after: {after}");
            Console.WriteLine($"GDI delta: {delta}");
            Console.WriteLine(delta <= 0 ? "PASS: no GDI accumulation" : "FAIL: GDI objects accumulated");
            return delta <= 0 ? 0 : 1;
        }
        finally
        {
            DestroyWindow(window);
        }
    }
}
