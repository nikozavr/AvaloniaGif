using AvaloniaGif.Decoding;
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Animation;
using System.Threading;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Rendering;
using Avalonia.Logging;

namespace AvaloniaGif
{
    public class GifInstance : IDisposable
    {
        private readonly Image _targerImage;
        private readonly Stream _stream;
        private readonly IterationCount _iterationCount;
        private readonly bool _autoStart;

        private GifDecoder _gifDecoder;
        private GifBackgroundWorker _bgWorker;
        private WriteableBitmap _targetBitmap;
        private bool _hasNewFrame;
        private bool _isDisposed;

        private readonly object _bitmapSync = new object();
        private static readonly object _globalUIThreadUpdateLock = new object();

        public GifInstance(Image target, Stream stream, IterationCount iterationCount, bool autoStart = true)
        {
            _targerImage = target;
            _stream = stream;
            _iterationCount = iterationCount;
            _autoStart = autoStart;
        }

        public void Process()
        {
            GifRepeatBehavior gifRepeatBehavior = new GifRepeatBehavior();
            if (_iterationCount.IsInfinite)
            {
                gifRepeatBehavior.LoopForever = true;
            }
            else
            {
                gifRepeatBehavior.LoopForever = false;
                gifRepeatBehavior.Count = (int)_iterationCount.Value;
            }

            _gifDecoder = new GifDecoder(_stream);
            var pixSize = new PixelSize(_gifDecoder.Header.Dimensions.Width, _gifDecoder.Header.Dimensions.Height);
            _targetBitmap = new WriteableBitmap(pixSize, new Vector(96, 96), PixelFormat.Bgra8888);
           // _bgWorker.
            //FrameChanged();
            _targerImage.Source = _targetBitmap;
            _bgWorker = new GifBackgroundWorker(_gifDecoder, gifRepeatBehavior);
            //TargetControl.DetachedFromVisualTree += delegate { this.Dispose(); };
            _bgWorker.CurrentFrameChanged += FrameChanged;

            Run();
        }

        private void RenderTick(TimeSpan time)
        {
            if (_isDisposed | !_hasNewFrame) return;
            lock (_globalUIThreadUpdateLock)
            lock (_bitmapSync)
            {
                _targerImage?.InvalidateVisual();
                _hasNewFrame = false;
            }
        }

        private void FrameChanged()
        {
            lock (_bitmapSync)
            {
                if (_isDisposed) return;
                _hasNewFrame = true;
                using (var lockedBitmap = _targetBitmap?.Lock())
                    _gifDecoder?.WriteBackBufToFb(lockedBitmap.Address);
            }
        }

        private void Run()
        {
            if (!_stream.CanSeek)
                throw new ArgumentException("The stream is not seekable");

            AvaloniaLocator.Current.GetService<IRenderTimer>().Tick += RenderTick;
            _bgWorker?.SendCommand(BgWorkerCommand.Play);
        }

        public void Dispose()
        {
            _stream?.Dispose();
            _gifDecoder?.Dispose();
            _targetBitmap?.Dispose();
        }
    }
}