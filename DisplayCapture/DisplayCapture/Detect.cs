using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using System.Diagnostics;



namespace DisplayDetector
{
    public class Detector
    {
        #region リングバッファ
        class RingBuf<T>
        {
            private readonly object _asyncLock = new object();
            private readonly int _bufSize;

            private T[] _items;
            private int _bufPos = 0;
            public int Count = 0;

            public RingBuf(int bufSize)
            {
                _bufSize = bufSize;
                _items = new T[_bufSize];
            }

            public void Add(T item)
            {
                lock (_asyncLock)
                {

                    _items[_bufPos] = item;
                    _bufPos++;
                    Count++;
                    if (_bufPos == _bufSize)
                        _bufPos = 0;
                }
            }

            public T Latest()
            {
                lock (_asyncLock)
                {
                    return _items[_bufPos];
                }
            }

            public T[] ToArray()
            {
                T[] items;
                lock (_asyncLock)
                {

                    int num = _bufSize < Count ? _bufSize : Count;
                    items = new T[num];
                    for (int i = 0; i < num; i++)
                    {
                        items[i] = _items[(_bufPos + _bufSize - i) / _bufSize];
                    }
                }
                return items;
            }

            public void Clear()
            {
                lock (_asyncLock)
                {
                    Count = 0;
                    _bufPos = 0;
                    _items = new T[_bufSize];
                }
            }
        }
        #endregion

        #region private変数

        RingBuf<byte[]> _pictureBuf = new RingBuf<byte[]>(2);//キャプチャ画面のリングバッファ

        //キャプチャ画面の情報
        private IntPtr _hwnd;//ウィンドウハンドル
        private BITMAP _defBitmap;//デフォルトのビットマップ情報

        //検出器の情報
        private bool _isRun = false; //検出器が起動しているかどうか

        private RingBuf<byte[]> _divs = new RingBuf<byte[]>(1);//

        private Exception _exception;//例外が発生しているかどうか

        private Stopwatch _sw = new Stopwatch();

        #endregion

        #region privateメソッド
        //初期化
        void Initialize(PixelFormat pixelFormat)
        {

            _defBitmap = DisplayCapt.CaptureWindow(_hwnd);

            if (_defBitmap == null)
                throw new InvalidHandleException();

            _defBitmap.PixelFormat = pixelFormat;
            BitmapData bitmapData = _defBitmap.Bitmap.LockBits(new Rectangle(0, 0, _defBitmap.Width, _defBitmap.Height), ImageLockMode.ReadOnly, _defBitmap.PixelFormat);
            _defBitmap.Stride = bitmapData.Stride;
            _defBitmap.Size = _defBitmap.Stride * _defBitmap.Height;
            _defBitmap.Bitmap.UnlockBits(bitmapData);

        }

        //一定時間ごとにスクショを保存する。
        void GetPict()
        {


            BITMAP bitmap = DisplayCapt.CaptureWindow(_hwnd);
            if (bitmap == null)
            {
                _exception = new InvalidHandleException();
                _isRun = false;
                return;
            }

            if (bitmap.Width != _defBitmap.Width || bitmap.Height != _defBitmap.Height)
            {
                _exception = new ChangeSizeException();
                _isRun = false;
                return;
            }


            BitmapData bdata = bitmap.Bitmap.LockBits(new Rectangle(0, 0, _defBitmap.Width, _defBitmap.Height), ImageLockMode.ReadOnly, _defBitmap.PixelFormat);

            byte[] buf = new byte[_defBitmap.Size];

            Marshal.Copy(bdata.Scan0, buf, 0, _defBitmap.Size);

            bitmap.Bitmap.UnlockBits(bdata);

            _pictureBuf.Add(buf);




        }

        //一定時間ごとに解析
        void Detection()
        {
            if (_pictureBuf.Count < 21)
                return;


            byte[][] picts = _pictureBuf.ToArray();
            int height = _defBitmap.Height;
            int width = _defBitmap.Width;
            int pix = _defBitmap.Stride / width;
            int size = _defBitmap.Size;

            byte[] divpict = new byte[size];
            byte[] pict1 = picts[1];
            byte[] pict2 = picts[0];

            //単純に画素の差分を測定
            for (int i = 0; i < size; i++)
            {
                divpict[i] = (byte)Math.Abs(pict1[i] - pict2[i]);
            }

            _divs.Add(divpict);
        }

        //検出器の起動中のメソッド
        async void Running()
        {
            await Task.Run(() =>
            {

                _sw.Start();
                long formerTime = 0;
                while (_isRun)
                {
                    try
                    {

                        GetPict();
                        Detection();

                        long time;
                        do
                        {
                            time = _sw.ElapsedMilliseconds;
                        } while (time - formerTime < 50);
                        formerTime = time;

                    }
                    catch (Exception e)
                    {
                        _isRun = false;
                        _exception = e;
                    }
                }
                _sw.Stop();
            });
        }

        #endregion

        #region publicメンバー



        //画像情報のプロパティ
        public int Height => _defBitmap.Height;
        public int Width => _defBitmap.Width;
        public int Stride => _defBitmap.Stride;


        //コンストラクタ
        public Detector(IntPtr hWnd, PixelFormat pixelFormat)
        {
            _hwnd = hWnd;//ハンドルを取得

            Initialize(pixelFormat);//初期化
        }

        //探索の開始
        public void Start()
        {
            if (_exception != null)
                throw _exception;

            if (_isRun)
                return;

            _isRun = true;
            Running();
        }

        //探索の終了
        public void Stop()
        {
            if (_exception != null)
                throw _exception;

            _isRun = false;
        }

        //画像の取得
        public byte[] GetPicture()
        {
            if (_exception != null)
                throw _exception;

            if (!_isRun)
                return null;
            return _divs.Latest();
        }
        #endregion
    }



    //キャプチャ画面のサイズが変わったときに起こる例外
    public class ChangeSizeException : Exception
    {

    }

    //キャプチャ画面のハンドルが不正であるときに起こる例外
    public class InvalidHandleException : Exception
    {

    }

}
