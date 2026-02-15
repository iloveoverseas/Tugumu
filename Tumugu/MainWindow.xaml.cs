using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Tumugu
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        // 現在の保存先フォルダと編集中のファイル名を保持するフィールド
        private string _currentEditFolder;
        private string _currentEditFileName;

        public MainWindow()
        {
            InitializeComponent();

            // WebView2 の初期化と、ドラッグオーバー・ドロップイベントの無効化スクリプトの登録
            InitializeWebView();

            // WPF の WebView2 はブラウザ内部の右クリックを直接 WPF 側で拾えないため、CoreWebView2 のイベントを使って右クリックを検出します。
            MarkdownBrowser.CoreWebView2InitializationCompleted += MarkdownBrowser_CoreWebView2InitializationCompleted;

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
            _currentEditFolder = AppDomain.CurrentDomain.BaseDirectory;
            _currentEditFolder = @"C:\Temp";

            (double physicalWidth, double physicalHeight) = GetScreenSize();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //RewriteMarkdownBrowser();
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

        private void lblTitleBlankArea_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ChangeWindowStage();

            ChangeMarkdownTextBoxWidth(999);
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ChangeWindowStage();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void ChangeWindowStage()
        {
            // WPFで WindowState = WindowState.Maximized; にした際、通常はOSがタスクバー（下のメニュー）を避けて最大化してくれます。
            // しかし、WindowStyle = "None" を指定してカスタムウィンドウを作っている場合、タスクバーを覆い隠してフルスクリーンになってしまうというWPF特有の挙動があります。これをWindows 11のタスクバーを考慮したサイズにするには、主に2つの方法があります。
            this.WindowState = this.WindowState == WindowState.Normal ? WindowState.Maximized : WindowState.Normal;
        }

        private void MarkdownBrowser_CoreWebView2InitializationCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            MarkdownBrowser.Visibility = Visibility.Visible;

            // 右クリックイベントをフック
            MarkdownBrowser.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
        }

        private void CoreWebView2_ContextMenuRequested(object sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            e.Handled = true; // 標準メニューを消す

            var menu = new System.Windows.Controls.ContextMenu();

            var printItem = new System.Windows.Controls.MenuItem();
            printItem.Header = "印刷";
            printItem.Click += (s, ev) =>
            {
                MarkdownBrowser.CoreWebView2.ShowPrintUI();
            };

            menu.Items.Add(printItem);

            // 右クリック位置に表示
            menu.IsOpen = true;
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

        private void MarkdownTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            //var drugFiles = (string[])e.Data.GetData(DataFormats.FileDrop);

            //string drugFileFullPath = drugFiles[0];

            //// ファイルが1つだけで、拡張子が .md なら受け入れ
            //if (drugFiles.Length == 1 && Path.GetExtension(drugFileFullPath) == ".md")
            //{
            //    e.Effects = DragDropEffects.Copy;
            //    Mouse.OverrideCursor = Cursors.Hand;
            //}
            //else
            //{
            //    e.Effects = DragDropEffects.None;
            //    Mouse.OverrideCursor = null;
            //}

            //e.Handled = true;
        }

        private void MarkdownTextBox_PreviewDragLeave(object sender, DragEventArgs e)
        {
            // ウィンドウ全体のカーソルをリセット
            Mouse.OverrideCursor = null; 
            this.Cursor = null;
        }



        private async void RewriteMarkdownBrowser()
        {
            if (MarkdownBrowser == null) return;

            // Markdown → HTML Markdig を使う場合の変換
            var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            string plainText = GetPlainTextFromRichTextBox(RichMarkdownTextBox);
            var htmlBody = Markdown.ToHtml(plainText, pipeline);

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

        private void MarkdownTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            //RewriteMarkdownBrowser();

            //// Markdig を使う場合の変換
            //var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
            //var htmlBody = Markdown.ToHtml(MarkdownTextBox.Text, pipeline);

            ////RewriteMarkdownBrowser();

            //// チラつき防止のため、部分的に更新
            //var script = $"document.getElementById('content').innerHTML = `{htmlBody}`;";
            //MarkdownBrowser.ExecuteScriptAsync(script);
        }







        private void MarkdownBrowser_DragOver(object sender, DragEventArgs e)
        {
            // ドラッグ操作を拒否し、禁止カーソルを表示させる
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }


        private void ChangeMarkdownTextBoxWidth(double ratio)
        {
            // WindowタイトルのlblTitleBlankAreaをダブルクリックで最大化/元に戻すとき、ここに999が渡されるようにしている
            if (ratio == 999) 
            {
                return;
            }

            // MarkdownTextBoxColumnの幅を変更
            TextBoxColumn.Width = new GridLength(ratio, GridUnitType.Star);
        }

        private void Btn0Percent_Click(object sender, RoutedEventArgs e)
        {
            ChangeMarkdownTextBoxWidth(0);
        }

        private void Btn25Percent_Click(object sender, RoutedEventArgs e)
        {
            ChangeMarkdownTextBoxWidth(1);
        }

        private void Btn50Percent_Click(object sender, RoutedEventArgs e)
        {
            ChangeMarkdownTextBoxWidth(3);
        }

        private bool _isChanging = false; // 無限ループ防止フラグ

        // RichTextBox からプレーンテキストを取得する
        private string GetPlainTextFromRichTextBox(RichTextBox rtb)
        {
            // RichTextドキュメントの全範囲(ContentStartからContentEndまで)を指定
            TextRange textRange = new TextRange(rtb.Document.ContentStart, rtb.Document.ContentEnd);

            return textRange.Text.Trim();
        }

        private bool _isInternalChanging = false; // 内部的な変更中かどうかを保持

        private void RichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // ズーム中（内部変更中）なら何もしない
            if (_isInternalChanging) return;

            // プログラムによる変更時は処理をスキップ
            if (_isChanging) return;

            _isChanging = true;

            // sender を RichTextBox のインスタンスとして取得
            var rtb = sender as RichTextBox;
            if (rtb == null)
            {
                _isChanging = false;
                return;
            }

            // BeginChange / EndChange はインスタンスメソッドなので rtb 経由で呼ぶ
            rtb.BeginChange();
            try
            {
                ApplyHeadingHighlighting(rtb);
            }
            finally
            {
                rtb.EndChange();
                _isChanging = false;
            }

            RewriteMarkdownBrowser();
        }

        // RichTextBox内の段落をループして、Markdownの見出しやリストなどの行を判定し、条件に合う行に対して背景色や文字色を変更する
        private void ApplyHeadingHighlighting(RichTextBox rtb)
        {
            if (rtb?.Document == null) return;

            // RichTextBox内の全ブロックをループ
            foreach (var block in rtb.Document.Blocks)
            {
                if (_isSimpleEditMode) continue;

                // シンプル編集モードならスキップ
                if (block is Paragraph paragraph)
                {
                    // 段落のテキストを取得
                    TextRange range = new TextRange(paragraph.ContentStart, paragraph.ContentEnd);
                    string text = range.Text.TrimStart(); // 行頭の空白を除去して判定

                    if (IsMarkdownLine(text))
                    {
                        paragraph.Background = Brushes.GhostWhite;
                        paragraph.Foreground = Brushes.SteelBlue;
                        paragraph.FontWeight = FontWeights.Bold;
                    }
                    else
                    {
                        // 条件に合わない場合は標準の色に戻す
                        paragraph.Background = Brushes.White;
                        paragraph.Foreground = Brushes.Black;
                        paragraph.FontWeight = FontWeights.Normal;
                    }
                }
            }
        }

        // Ctrl + マウスホイールでフォントサイズを変更するイベントハンドラー
        private void RichMarkdownTextBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                _isInternalChanging = true; // ★フラグを立てる

                try
                {
                    double currentSize = RichMarkdownTextBox.FontSize;
                    double newSize;

                    // 1. フォントサイズの増減計算
                    if (e.Delta > 0) newSize = currentSize + 2;
                    else newSize = currentSize - 2;

                    // 制限範囲内なら適用
                    if (newSize >= 10 && newSize <= 60)
                    {
                        // 2. RichTextBox自体のFontSizeを更新
                        RichMarkdownTextBox.FontSize = newSize;

                        // 3. 適正な LineHeight を計算（例：フォントサイズの1.5倍）
                        double multiplier = 1.5;
                        double newLineHeight = newSize * multiplier;

                        // 4. 全ての段落に適用（重要）
                        foreach (var block in RichMarkdownTextBox.Document.Blocks)
                        {
                            if (block is Paragraph p)
                            {
                                p.LineHeight = newLineHeight;
                            }
                        }
                    }
                }
                finally
                {
                    _isInternalChanging = false; // ★必ず最後にfalseに戻す
                }

                e.Handled = true;
            }
        }

        private void RichMarkdownTextBox_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string droppedFile = files[0];

                try
                {
                    // RichTextBoxの操作範囲を確定
                    var document = RichMarkdownTextBox.Document;
                    TextRange range = new TextRange(document.ContentStart, document.ContentEnd);

                    // ファイルを「読み取り専用」かつ「共有許可」で開く（他アプリが使用中でも開けるようにする）
                    using (FileStream fs = new FileStream(droppedFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {                   
                        range.Load(fs, DataFormats.Text);           // DataFormats.Text で読み込み
                    }

                    _currentEditFileName = droppedFile;
                    _currentEditFolder = System.IO.Path.GetDirectoryName(droppedFile);

                    lblEditFile.Content = _currentEditFileName;
                    lblEditFile.Foreground = Brushes.Orange;
                }
                catch (Exception ex)
                {
                    // 6. ユーザーへの通知（ファイルがロックされている、権限がない等のエラー対応）
                    MessageBox.Show($"ファイルの読み込みに失敗しました:\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }


            RewriteMarkdownBrowser();
        }

        private void RichMarkdownTextBox_PreviewDragOver(object sender, DragEventArgs e)
        {
            // ドロップされたものが「ファイル」である場合のみ、受け入れを許可するアイコンにする
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }

            // イベントを完了（標準の動作を抑制）
            e.Handled = true;
        }

        private bool IsMarkdownLine(string text)
        {
            // 行頭の空白をトリミングして判定
            string line = text.TrimStart();

            List<string> markdownKeywords = new List<string>
            {
                "#####", "####", "###", "##", "#", "```", "-", "---", ">"
            };

            List<Regex> MarkdownPatterns = new List<Regex>
            {
                //new Regex(@"^>+\s", RegexOptions.Compiled),             // 引用
                //new Regex(@"^[-*]\s", RegexOptions.Compiled),           // リスト: - または *
                new Regex(@"^#{1,5}\s", RegexOptions.Compiled),         // 見出し: #〜#####
                //new Regex(@"^```", RegexOptions.Compiled),              // コードブロック
                new Regex(@"^!\[.*?\]\(.*?\)", RegexOptions.Compiled)   // 画像構文
            };

            // LINQのAnyを使用して、1つでも一致(IsMatch)するものがあるか確認
            return MarkdownPatterns.Any(reg => reg.IsMatch(line)); ;
        }

        private void BtnSaveAs_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentEditFileName)) return;

            // RichTextBox 全文を TextRange として取得
            var textRange = new TextRange(RichMarkdownTextBox.Document.ContentStart, RichMarkdownTextBox.Document.ContentEnd);

            // プレーンテキストとして保存
            File.WriteAllText(_currentEditFileName, textRange.Text);
            lblEditFile.Foreground = Brushes.White;
        }

        private void BtnFolder_Click(object sender, RoutedEventArgs e)
        {
            ShowSaveDialog();

            //Process.Start("explorer.exe", _currentEditFolder);

        }

        private void chkSimpleEdit_Checked(object sender, RoutedEventArgs e)
        {
            _isSimpleEditMode = true;
            chkSimpleEdit.Foreground = Brushes.White;
        }

        private bool _isSimpleEditMode = true;
        private void chkSimpleEdit_Unchecked(object sender, RoutedEventArgs e)
        {
            _isSimpleEditMode = false;
            chkSimpleEdit.Foreground = Brushes.DarkGray;
        }

        public void ShowSaveDialog()
        {
            var dialog = new SaveFileDialog
            {
                Title = "名前を付けて保存",
                Filter = "マークダウンファイル (*.md)|*.md",
                FileName = "新しいファイル.md",
                InitialDirectory = "C:\\"
            };

            if (string.IsNullOrEmpty(_currentEditFileName) == false)
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(_currentEditFileName);
                dialog.FileName = System.IO.Path.GetFileName(_currentEditFileName);
            }

            if (dialog.ShowDialog() == true)
            {
                // 選択されたファイルパス
                string path = dialog.FileName;

                // 保存処理（例：空ファイルを作成）
                System.IO.File.WriteAllText(path, "");
            }
        }

    }
}
