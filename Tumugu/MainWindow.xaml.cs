using Markdig;
using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using static ScreenHelper;

namespace Tumugu
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        // Folder where images will be saved. Defaults to application folder, updated when a .md file is opened via drag-and-drop.
        private string _currentSaveFolder;

        // Zoom support for the Markdown TextBox via mouse wheel + Ctrl
        private double _markdownFontSizeDefault = 12.0;
        private double _markdownFontSizeMin = 6.0;
        private double _markdownFontSizeMax = 48.0;

        public MainWindow()
        {
            InitializeComponent();

            // WebView2 の初期化と、ドラッグオーバー・ドロップイベントの無効化スクリプトの登録
            InitializeWebView();

            // 現在のモニタに合わせた作業領域を取得
            Rect workArea = ScreenHelper.GetCurrentWorkArea(this);

            // 最大化時のサイズを制限（これでタスクバーを隠さない）
            this.MaxWidth = workArea.Width;
            this.MaxHeight = workArea.Height;

            // キャプションバー以外でもドラッグ可能にする
            this.MouseLeftButtonDown += (sender, e) => this.DragMove();

            this.Topmost = true;
            this.Topmost = false;

            // default save folder
            _currentSaveFolder = AppDomain.CurrentDomain.BaseDirectory;
            _currentSaveFolder = @"C:\Develop\WpfStartSample\WpfStartSample\Doc";

            (double physicalWidth, double physicalHeight) = GetScreenSize();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        // DPIを考慮した「物理ピクセル」を取得する
        // Windows 11のような高解像度ディスプレイ環境では、論理サイズと物理ピクセルが異なります。
        // 現在のウィンドウが表示されているモニタの「正確な倍率」を知るには、WPFのVisualTreeHelperを使います。

        private (double physicalWidth, double physicalHeight) GetScreenSize()
        {
            // Windowクラス内での実行を想定
            var dpi = VisualTreeHelper.GetDpi(this);

            double dpiScaleX = dpi.DpiScaleX; // 例: 1.25 (125%)
            double dpiScaleY = dpi.DpiScaleY;

            // 物理的なピクセル解像度を計算
            double physicalWidth = SystemParameters.PrimaryScreenWidth * dpiScaleX;
            double physicalHeight = SystemParameters.PrimaryScreenHeight * dpiScaleY;

            return (physicalWidth, physicalHeight);
        }


        private void MarkdownTextBox_Drop(object sender, DragEventArgs e)
        {
            // ファイルがドロップされていなければ終了
            if (e.Data.GetDataPresent(DataFormats.FileDrop) == false)
            {
                return;
            }

            var dropFiles = (string[])e.Data.GetData(DataFormats.FileDrop);

            string droppedFileFullPath = dropFiles[0];
            string directoryPath = Path.GetDirectoryName(droppedFileFullPath);
            _currentSaveFolder = directoryPath;

            string text = File.ReadAllText(droppedFileFullPath, Encoding.UTF8);

            MarkdownTextBox.Text = text.ToString();

            Mouse.OverrideCursor = Cursors.IBeam;       // カーソルを戻す

            RewriteMarkdownBrowser();
        }

        private void MarkdownTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            var drugFiles = (string[])e.Data.GetData(DataFormats.FileDrop);

            string drugFileFullPath = drugFiles[0];

            // ファイルが1つだけで、拡張子が .md なら受け入れ
            if (drugFiles.Length == 1 && Path.GetExtension(drugFileFullPath) == ".md")
            {
                e.Effects = DragDropEffects.Copy;
                Mouse.OverrideCursor = Cursors.Hand;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                Mouse.OverrideCursor = null;
            }

            e.Handled = true;
        }

        private void MarkdownTextBox_PreviewDragLeave(object sender, DragEventArgs e)
        {
            // ウィンドウ全体のカーソルをリセット
            Mouse.OverrideCursor = null; 
            this.Cursor = null;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RewriteMarkdownBrowser();
        }

        private async void RewriteMarkdownBrowser()
        {
            // Markdown → HTML Markdig を使う場合の変換
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            var htmlBody = Markdown.ToHtml(MarkdownTextBox.Text, pipeline);

            // 最低限の CSS を付与（白基調・読みやすい
            var htmlTemplate = $@"
                    <!DOCTYPE html>
                    <html>
                    <head> <meta charset=""utf-8""> <base href=""https://local.example/""> </head>
                    <style>
                    /* ===== Markdown Base Style ===== */
                    body {{
                        font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Helvetica, Arial, sans-serif;
                        font-size: 16px;
                        line-height: 1.6;
                        color: #24292e;
                        background: #ffffff;
                        padding: 20px;
                    }}

                    /* Headings */
                    h1, h2, h3, h4, h5, h6 {{
                        font-weight: 600;
                        margin-top: 24px;
                        margin-bottom: 16px;
                        line-height: 1.25;
                    }}
                    h1 {{ font-size: 2em; border-bottom: 1px solid #eaecef; padding-bottom: .3em; }}
                    h2 {{ font-size: 1.5em; border-bottom: 1px solid #eaecef; padding-bottom: .3em; }}
                    h3 {{ font-size: 1.25em; }}
                    h4 {{ font-size: 1em; }}
                    h5 {{ font-size: .875em; }}
                    h6 {{ font-size: .85em; color: #6a737d; }}

                    /* Paragraph */
                    p {{
                        margin: 16px 0;
                    }}

                    /* Links */
                    a {{
                        color: #0366d6;
                        text-decoration: none;
                    }}
                    a:hover {{
                        text-decoration: underline;
                    }}

                    /* Lists */
                    ul, ol {{
                        padding-left: 2em;
                        margin: 16px 0;
                    }}
                    li {{
                        margin: 4px 0;
                    }}

                    /* Blockquote */
                    blockquote {{
                        padding: 0 1em;
                        color: #6a737d;
                        border-left: .25em solid #dfe2e5;
                        margin: 16px 0;
                    }}

                    /* Code (inline) */
                    code {{
                        background-color: rgba(27,31,35,.05);
                        padding: .2em .4em;
                        border-radius: 3px;
                        font-family: SFMono-Regular, Consolas, ""Liberation Mono"", Menlo, monospace;
                        font-size: 85%;
                    }}

                    /* Code block */
                    pre {{
                        background-color: #f6f8fa;
                        padding: 16px;
                        border-radius: 6px;
                        overflow: auto;
                    }}
                    pre code {{
                        background: none;
                        padding: 0;
                        font-size: 85%;
                    }}

                    /* Table */
                    table {{
                        border-collapse: collapse;
                        width: 100%;
                        margin: 16px 0;
                    }}
                    th, td {{
                        border: 1px solid #dfe2e5;
                        padding: 6px 13px;
                    }}
                    th {{
                        background: #f6f8fa;
                        font-weight: 600;
                    }}
                    tr:nth-child(even) {{
                        background: #fafbfc;
                    }}

                    /* Horizontal rule */
                    hr {{
                        border: 0;
                        border-top: 1px solid #eaecef;
                        margin: 24px 0;
                    }}

                    /* Images */
                    img {{
                        max-width: 100%;
                        height: auto;
                    }}

                    /* Task list */
                    .task-list-item {{
                        list-style-type: none;
                    }}
                    .task-list-item input {{
                        margin-right: .5em;
                    }}
                    </style>

                    </head>
                    <body>
                    <div id='content'>{htmlBody}</div>
                    </body>
                    </html>
                    ";
            //                     <div id=""content""></div>


            // WebView2 に HTML を表示
            await MarkdownBrowser.EnsureCoreWebView2Async();

            MarkdownBrowser.CoreWebView2.SetVirtualHostNameToFolderMapping("local.example", @"C:\Develop\WpfStartSample\WpfStartSample\Doc", CoreWebView2HostResourceAccessKind.Allow);
            MarkdownBrowser.CoreWebView2.NavigateToString(htmlTemplate);
        }

        private void MarkdownTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            RewriteMarkdownBrowser();

            //// Markdig を使う場合の変換
            //var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            //var htmlBody = Markdown.ToHtml(MarkdownTextBox.Text, pipeline);

            ////RewriteMarkdownBrowser();

            //// チラつき防止のため、部分的に更新
            //var script = $"document.getElementById('content').innerHTML = `{htmlBody}`;";
            //MarkdownBrowser.ExecuteScriptAsync(script);
        }

        private void MarkdownTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Ctrl キーが押されていなければ終了
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;

            // フォントサイズ調整
            if (MarkdownTextBox.FontSize == 0) MarkdownTextBox.FontSize = _markdownFontSizeDefault;

            // ホイールの回転方向でフォントサイズを増減
            if (e.Delta > 0)
            {
                MarkdownTextBox.FontSize = Math.Min(_markdownFontSizeMax, MarkdownTextBox.FontSize + 1);
            }
            else if (e.Delta < 0)
            {
                MarkdownTextBox.FontSize = Math.Max(_markdownFontSizeMin, MarkdownTextBox.FontSize - 1);
            }

            e.Handled = true;
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ChangeWindowStage();
        }

        private void ChangeWindowStage()
        {
            // WPFで WindowState = WindowState.Maximized; にした際、通常はOSがタスクバー（下のメニュー）を避けて最大化してくれます。
            // しかし、WindowStyle = "None" を指定してカスタムウィンドウを作っている場合、タスクバーを覆い隠してフルスクリーンになってしまうというWPF特有の挙動があります。これをWindows 11のタスクバーを考慮したサイズにするには、主に2つの方法があります。
            this.WindowState = this.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private async void InitializeWebView()
        {
            await MarkdownBrowser.EnsureCoreWebView2Async();

            // ページロード時および遷移時に常に実行されるスクリプトを登録
            await MarkdownBrowser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                window.addEventListener('dragover', function(e) {
                    e.preventDefault();
                    e.dataTransfer.dropEffect = 'none'; // カーソルを禁止にする
                }, false);

                window.addEventListener('drop', function(e) {
                    e.preventDefault(); // ドロップ動作を無効化
                }, false);
            ");
        }

        private void MarkdownBrowser_DragOver(object sender, DragEventArgs e)
        {
            // ドラッグ操作を拒否し、禁止カーソルを表示させる
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void lblTitleBlankArea_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ChangeWindowStage();
        }
    }
}
