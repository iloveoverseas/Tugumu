using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

public static class ScreenHelper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll")]
    public static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    private const uint MONITOR_DEFAULTTONEAREST = 2; // 最も近いモニタを取得

    /// <summary>
    /// 特定のウィンドウが存在するモニタの作業領域をWPF論理単位で取得します
    /// </summary>
    public static Rect GetCurrentWorkArea(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return SystemParameters.WorkArea;

        // ウィンドウハンドルから現在のモニタを取得
        IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

        MONITORINFO monitorInfo = new MONITORINFO();
        monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

        if (GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            // 現在のウィンドウのDPIを取得（モニタをまたぐと自動で更新される）
            DpiScale dpi = VisualTreeHelper.GetDpi(window);

            // 物理ピクセルを作業領域の論理単位に変換
            double width = (monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) / dpi.DpiScaleX;
            double height = (monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) / dpi.DpiScaleY;

            return new Rect(0, 0, width, height);
        }

        return SystemParameters.WorkArea;
    }
}