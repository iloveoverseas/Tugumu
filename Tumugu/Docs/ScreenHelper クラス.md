ウィンドウが別のモニタへ移動した際、Windowsの設定（拡大率やタスクバーの配置）がモニタごとに異なる場合でも、正しく `MaxHeight` と `MaxWidth` を追従させる実装を紹介します。

この処理を実現するには、ウィンドウの移動イベント（`LocationChanged`）を監視し、その都度「現在どのモニタにいるか」を判定して計算をやり直すのが最も確実です。

---

### 1. 改良版 ScreenHelper クラス

まずは、ウィンドウハンドル（HWND）を渡すと、そのウィンドウが今いるモニタの作業領域を計算して返す汎用的なメソッドを作成します。

C#

```
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

        // 1. ウィンドウハンドルから現在のモニタを取得
        IntPtr hMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

        MONITORINFO monitorInfo = new MONITORINFO();
        monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

        if (GetMonitorInfo(hMonitor, ref monitorInfo))
        {
            // 2. 現在のウィンドウのDPIを取得（モニタをまたぐと自動で更新される）
            DpiScale dpi = VisualTreeHelper.GetDpi(window);

            // 3. 物理ピクセルを作業領域の論理単位に変換
            double width = (monitorInfo.rcWork.Right - monitorInfo.rcWork.Left) / dpi.DpiScaleX;
            double height = (monitorInfo.rcWork.Bottom - monitorInfo.rcWork.Top) / dpi.DpiScaleY;

            return new Rect(0, 0, width, height);
        }

        return SystemParameters.WorkArea;
    }
}
```

---

### 2. MainWindow でのイベント実装

次に、ウィンドウが移動したときにこの関数を呼び出す仕組みを組み込みます。

C#

```
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // 移動イベントを購読
        this.LocationChanged += MainWindow_LocationChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // 初回表示時の計算
        UpdateMaxConstraints();
    }

    private void MainWindow_LocationChanged(object sender, EventArgs e)
    {
        // ウィンドウが移動するたびに最大化制限を更新
        UpdateMaxConstraints();
    }

    private void UpdateMaxConstraints()
    {
        // 現在のモニタに合わせた作業領域を取得
        Rect workArea = ScreenHelper.GetCurrentWorkArea(this);

        // 最大化時のサイズを制限（これでタスクバーを隠さない）
        this.MaxWidth = workArea.Width;
        this.MaxHeight = workArea.Height;
    }
}
```

---

### この実装のポイント

1. **`MONITOR_DEFAULTTONEAREST`**:
   ウィンドウが2つのモニタにまたがっている場合でも、面積が大きい方のモニタの情報を取得してくれます。

2. **`LocationChanged` イベント**:
   ユーザーがウィンドウをドラッグして別のモニタへ放り込んだ瞬間に `UpdateMaxConstraints()` が走ります。これにより、最大化ボタンを押す直前にそのモニタの正しい `MaxHeight` がセットされます。

3. **DPIの自動追従**: `VisualTreeHelper.GetDpi(window)` は、Windows 10/11の「モニターごとのDPI（Per-Monitor DPI）」に対応しているため、100%のモニタから150%のモニタへ移動した際も、計算結果がズレません。

4. **`OnSourceInitialized`**:
   コンストラクタ時点ではウィンドウハンドル (`HWND`) がまだ生成されていないため、Win32 APIを叩く処理は `OnSourceInitialized` 以降で行うのが安全です。

### 動作の確認

- **タスクバーが下にあるモニタ**から**タスクバーが左にあるモニタ**へ移動させた際、最大化したときにそれぞれのタスクバーが正しく避けて表示されるか確認してください。

- **解像度の異なるモニタ間**の移動でも、ウィンドウがモニタからはみ出さずに最大化されれば成功です。
