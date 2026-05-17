using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Size = System.Windows.Size;

public partial class HwndSource2Control : HwndHost
{
    IntPtr _hwnd;
    IntPtr _hdc;
    byte[] _pixelBuffer;
    int _bufferWidth;
    int _bufferHeight;
    bool _doubleBuffer = false;
    bool _userPaint = true;
    bool _allPaintingInWmPaint = true;
    bool _opaque = true;
    Size _minimumSize = new Size(1, 1);
    object _bufferLock = new object();
    
    // Track if we need to redraw
    bool _needsRedraw = true;

    WndProcDelegate _wndProcDelegate;
    static string _className;
    BITMAPINFO _bitmapInfo;

    public HwndSource2Control()
    {
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        this.SizeChanged += OnSizeChanged;
        
        // Initialize BITMAPINFO structure
        _bitmapInfo = new BITMAPINFO();
        _bitmapInfo.bmiHeader.biSize = (uint)Marshal.SizeOf(typeof(BITMAPINFOHEADER));
        _bitmapInfo.bmiHeader.biPlanes = 1;
        _bitmapInfo.bmiHeader.biBitCount = 32; // 32-bit for RGBA
        _bitmapInfo.bmiHeader.biCompression = BI_RGB;
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hwnd == IntPtr.Zero)
        {
            BuildWindowCore(new HandleRef(this, IntPtr.Zero));
        }
    }

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_hwnd == IntPtr.Zero) return;
        if (_hdc != IntPtr.Zero)
        {
            ReleaseDC(_hwnd, _hdc);
            _hdc = IntPtr.Zero;
        }
        DestroyWindow(_hwnd);
        _hwnd = IntPtr.Zero;
    }

    void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_hwnd == IntPtr.Zero) return;
        SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, (int)e.NewSize.Width, (int)e.NewSize.Height, SWP_NOZORDER | SWP_NOACTIVATE);
        InvalidateRect(_hwnd, IntPtr.Zero, true);
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _wndProcDelegate = CustomWndProc;
        
        if (string.IsNullOrEmpty(_className))
        {
            _className = Guid.NewGuid().ToString();
            WNDCLASSEX wndClass = new WNDCLASSEX();
            wndClass.cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX));
            
            uint classStyle = 0;
            if (_allPaintingInWmPaint)
                classStyle |= CS_HREDRAW | CS_VREDRAW;
            if (_doubleBuffer)
                classStyle |= CS_SAVEBITS;
            
            wndClass.style = classStyle;
            wndClass.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
            wndClass.hInstance = Marshal.GetHINSTANCE(typeof(HwndSource2Control).Module);
            wndClass.hbrBackground = _opaque ? IntPtr.Zero : (IntPtr)1;
            wndClass.lpszClassName = _className;
            
            RegisterClass(ref wndClass);
        }

        uint windowStyle = WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS;
        uint windowExStyle = WS_EX_CONTROLPARENT;
        if (!_userPaint)
        {
            windowExStyle |= WS_EX_TRANSPARENT;
        }

        _hwnd = CreateWindowEx(
            windowExStyle,
            _className,
            nameof(HwndSource2Control),
            windowStyle,
            0, 0,
            (int)Math.Max(this.ActualWidth, _minimumSize.Width),
            (int)Math.Max(this.ActualHeight, _minimumSize.Height),
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create window");
        }

        _hdc = GetDC(_hwnd);
        
        return new HandleRef(this, _hwnd);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (hwnd.Handle != IntPtr.Zero)
        {
            if (_hdc != IntPtr.Zero)
            {
                ReleaseDC(hwnd.Handle, _hdc);
                _hdc = IntPtr.Zero;
            }
            DestroyWindow(hwnd.Handle);
        }
        _hwnd = IntPtr.Zero;
    }

    IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_PAINT:
                if (_userPaint)
                {
                    HandlePaint(hWnd);
                    return IntPtr.Zero;
                }
                break;
                
            case WM_ERASEBKGND:
                if (_opaque)
                {
                    // Return 1 to indicate we handled erasing (don't erase background)
                    return (IntPtr)1;
                }
                break;
        }
        
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    void HandlePaint(IntPtr hWnd)
    {
        PAINTSTRUCT ps = new PAINTSTRUCT();
        IntPtr hdc = BeginPaint(hWnd, ref ps);
        
        try
        {
            if (_needsRedraw && _pixelBuffer != null)
            {
                DrawPixelBufferToDC(hdc, ps.rcPaint);
            }
        }
        finally
        {
            EndPaint(hWnd, ref ps);
        }
    }

    void DrawPixelBufferToDC(IntPtr hdc, RECT updateRect)
    {
        lock (_bufferLock)
        {
            if (_pixelBuffer == null || _bufferWidth == 0 || _bufferHeight == 0)
                return;
            
            // Update BITMAPINFO with current buffer dimensions
            _bitmapInfo.bmiHeader.biWidth = _bufferWidth;
            _bitmapInfo.bmiHeader.biHeight = -_bufferHeight; // Negative height for top-down DIB
            _bitmapInfo.bmiHeader.biSizeImage = (uint)(_bufferWidth * _bufferHeight * 4);
            
            // Pin the pixel buffer to get a pointer
            GCHandle handle = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);
            
            try
            {
                IntPtr pBits = handle.AddrOfPinnedObject();
                
                // Draw only the invalidated region
                int updateWidth = updateRect.right - updateRect.left;
                int updateHeight = updateRect.bottom - updateRect.top;
                
                if (updateWidth > 0 && updateHeight > 0 && updateWidth <= _bufferWidth && updateHeight <= _bufferHeight)
                {
                    // Draw the entire buffer (SetDIBitsToDevice will clip to update region)
                    SetDIBitsToDevice(
                        hdc,
                        0, 0,                          // Destination X,Y
                        _bufferWidth,                  // Destination width
                        _bufferHeight,                 // Destination height
                        0, 0,                          // Source X,Y
                        0,                             // Start scan line
                        (uint)_bufferHeight,           // Scan line count
                        pBits,                         // Pixel bits
                        ref _bitmapInfo,               // BITMAPINFO
                        DIB_RGB_COLORS);               // Color usage
                }
            }
            finally
            {
                handle.Free();
            }
            
            _needsRedraw = false;
        }
    }

    /// <summary>
    /// Update the pixel buffer and redraw the control
    /// </summary>
    /// <param name="pixelBuffer">RGBA pixel buffer (byte array of size width * height * 4)</param>
    /// <param name="width">Width of the buffer in pixels</param>
    /// <param name="height">Height of the buffer in pixels</param>
    public void UpdatePixelBuffer(byte[] pixelBuffer, int width, int height)
    {
        if (pixelBuffer == null)
            throw new ArgumentNullException(nameof(pixelBuffer));
        
        if (pixelBuffer.Length != width * height * 4)
            throw new ArgumentException($"Buffer size must be {width * height * 4} bytes for RGBA data");
        
        lock (_bufferLock)
        {
            _pixelBuffer = pixelBuffer;
            _bufferWidth = width;
            _bufferHeight = height;
            _needsRedraw = true;
        }
        
        // Request redraw
        if (_hwnd != IntPtr.Zero)
        {
            InvalidateRect(_hwnd, IntPtr.Zero, true);
        }
    }

    /// <summary>
    /// Update the pixel buffer from a WriteableBitmap
    /// </summary>
    public void UpdateFromWriteableBitmap(WriteableBitmap bitmap)
    {
        if (bitmap == null)
            throw new ArgumentNullException(nameof(bitmap));
        
        if (bitmap.Format != PixelFormats.Bgra32 && bitmap.Format != PixelFormats.Pbgra32)
            throw new ArgumentException("Bitmap must be BGRA32 or PBGRA32 format");
        
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] buffer = new byte[height * stride];
        
        bitmap.CopyPixels(buffer, stride, 0);
        UpdatePixelBuffer(buffer, width, height);
    }

    /// <summary>
    /// Update the pixel buffer from a BitmapSource
    /// </summary>
    public void UpdateFromBitmapSource(BitmapSource bitmapSource)
    {
        if (bitmapSource == null)
            throw new ArgumentNullException(nameof(bitmapSource));
        
        // Convert to BGRA32 if needed
        FormatConvertedBitmap convertedBitmap = new FormatConvertedBitmap();
        convertedBitmap.BeginInit();
        convertedBitmap.Source = bitmapSource;
        convertedBitmap.DestinationFormat = PixelFormats.Bgra32;
        convertedBitmap.EndInit();
        
        WriteableBitmap writeableBitmap = new WriteableBitmap(convertedBitmap);
        UpdateFromWriteableBitmap(writeableBitmap);
    }

    /// <summary>
    /// Create a gradient test pattern
    /// </summary>
    public void CreateTestPattern(int width, int height)
    {
        byte[] buffer = new byte[width * height * 4];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 4;
                
                // Red gradient horizontally
                buffer[index] = (byte)((double)x / width * 255);     // Blue
                buffer[index + 1] = (byte)((double)y / height * 255); // Green
                buffer[index + 2] = (byte)((double)(x + y) / (width + height) * 255); // Red
                buffer[index + 3] = 255; // Alpha (fully opaque)
            }
        }
        
        UpdatePixelBuffer(buffer, width, height);
    }

    // Properties from original class
    public bool DoubleBuffer
    {
        get => _doubleBuffer;
        set { _doubleBuffer = value; /* Would require window recreation */ }
    }

    public bool UserPaint
    {
        get => _userPaint;
        set => _userPaint = value;
    }

    public bool AllPaintingInWmPaint
    {
        get => _allPaintingInWmPaint;
        set => _allPaintingInWmPaint = value;
    }

    public bool Opaque
    {
        get => _opaque;
        set => _opaque = value;
    }

    public Size MinimumSize
    {
        get => _minimumSize;
        set => _minimumSize = value;
    }
}