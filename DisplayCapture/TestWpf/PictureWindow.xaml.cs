using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DisplayDetector;

namespace TestWpf
{
    /// <summary>
    /// PictureWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class PictureWindow : Window
    {
        Detector _detector;//検出器
        private WriteableBitmap _wb;//キャプチャ画面用（未バインド）
        private bool _isRunDetector;//キャプチャ中かどうか
        private Exception _exception;//非同期処理の例外

        //コンストラクタ
        public PictureWindow(IntPtr hWnd)
        {
            InitializeComponent();

            _detector = new Detector(hWnd, System.Drawing.Imaging.PixelFormat.Format32bppRgb);

            _wb = new WriteableBitmap(_detector.Width, _detector.Height, 96, 96, PixelFormats.Bgr32, null);

            _detector.Start();
            _isRunDetector = true;
            Doing();

        }


        //キャプチャ画面作成用関数
        public static void Create(IntPtr hWnd)
        {

            try
            {
                PictureWindow pctWin = new PictureWindow(hWnd);
                pctWin.Show();
            }
            catch (InvalidHandleException)
            {
                MessageBox.Show("そのウィンドウは存在しません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //定期的にキャプチャ画面を更新
        async void Doing()
        {
            while (_isRunDetector)
            {
                try
                {
                    byte[] buf = _detector.GetPicture();
                    if (buf != null)

                        _wb.WritePixels(new Int32Rect(0, 0, _wb.PixelWidth, _wb.PixelHeight), buf, _wb.BackBufferStride,
                            0);

                    pic.Source = _wb;
                    await Task.Delay(50);
                }
                catch (Exception ex)
                {
                    _exception = ex;
                    _isRunDetector = false;
                }
            }
            Close();
        }

        //閉じるボタン
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            _detector.Stop();
            _isRunDetector = true;
            Close();
        }

        //ウィンドウを閉じるときの動作（例外処理）
        private void Closing_Window(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_exception != null)
            {
                Type ex = _exception.GetType();
                if(ex==typeof(ChangeSizeException))
                    MessageBox.Show("キャプチャしているウィンドウのサイズを変えてはいけません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }

        }
    }
}
