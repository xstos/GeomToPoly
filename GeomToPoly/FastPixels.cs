using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Size = System.Windows.Size;

public partial class FastPixels : HwndHost
{
    IntPtr _hwnd;
    IntPtr _hdc;
    int[] _pixelBuffer;
    int _bufferWidth;
    int _bufferHeight;
    bool _doubleBuffer = false;
    bool _userPaint = true;
    bool _allPaintingInWmPaint = true;
    bool _opaque = true;
    Size _minimumSize = new Size(1, 1);
    
    // Track if we need to redraw
    bool _needsRedraw = true;

    WndProcDelegate _wndProcDelegate;
    static string _className;
    BITMAPINFO _bitmapInfo;
    GCHandle gcHandle;
    public FastPixels()
    {
        _bitmapInfo = new BITMAPINFO();
        _pixelBuffer = new int[1920 * 1080];
        gcHandle = GCHandle.Alloc(_pixelBuffer, GCHandleType.Pinned);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
        
    }

    void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_hwnd == IntPtr.Zero)
        {
            BuildWindowCore(new HandleRef(this, IntPtr.Zero));
            Console.WriteLine("loaded "+ActualWidth+" "+ActualHeight);
        }
    }

    void OnUnloaded(object sender, RoutedEventArgs e)
    {
        gcHandle.Free();
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
        WNDCLASSEX wndClass = new WNDCLASSEX();
        
        _className = Guid.NewGuid().ToString();
        wndClass.cbSize = Marshal.SizeOf(typeof(WNDCLASSEX));
            
        int classStyle = 0;
        if (_allPaintingInWmPaint)
            classStyle |= CS_HREDRAW | CS_VREDRAW;
        if (_doubleBuffer)
            classStyle |= CS_SAVEBITS;
            
        wndClass.style = classStyle;
        wndClass.lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        wndClass.hInstance = Marshal.GetHINSTANCE(typeof(FastPixels).Module);
        wndClass.hbrBackground = _opaque ? IntPtr.Zero : (IntPtr)1;
        wndClass.lpszClassName = _className;
            
        ushort regResult = RegisterClassExW(ref wndClass);
        if (regResult == 0)
        {
            uint error = GetLastError();
            throw new InvalidOperationException("RegisterClassExW failed with " + error);
        }

        uint windowStyle = WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN | WS_CLIPSIBLINGS;
        int windowExStyle = WS_EX_CONTROLPARENT;
        if (!_userPaint)
        {
            windowExStyle |= WS_EX_TRANSPARENT;
        }

        //https://stackoverflow.com/questions/55910356/how-to-fix-windows-error-1407-cannot-find-window-class-when-trying-to-implemen
        _hwnd = CreateWindowExW(
            windowExStyle,
            wndClass.lpszClassName,
            nameof(FastPixels),
            windowStyle,
            0, 0,
            (int)Math.Max(ActualWidth, _minimumSize.Width),
            (int)Math.Max(ActualHeight, _minimumSize.Height),
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        if (_hwnd == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            string errorMessage = GetErrorMessage(errorCode);
        
            // Log detailed information for debugging
            string detailedError = $"CreateWindowEx failed with error {errorCode} ({errorMessage})\nClass: {_className}\nStyle: 0x{windowStyle:X8}\nExStyle: 0x{windowExStyle:X8}\nParent: {hwndParent.Handle}\nInstance: {Marshal.GetHINSTANCE(typeof(FastPixels).Module)}";
            Console.Write(detailedError);
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
            if (_needsRedraw)
            {
                DrawPixelBufferToDC(hdc);
            }
        }
        finally
        {
            EndPaint(hWnd, ref ps);
        }
    }

    void DrawPixelBufferToDC(IntPtr hdc)
    {
        var w = (int)Math.Min(ActualWidth, 1920);
        var h = (int)Math.Max(ActualHeight, 1080);
        SetBitmapInfo(ref _bitmapInfo,w,h); 
        Array.Fill(_pixelBuffer,BitConverter.ToInt32([0,0,255,0]));
        SetDIBitsToDevice(hdc, 0, 0, w, h, 0, 0, 0, h, ref _pixelBuffer[0], ref _bitmapInfo, 0);

    }
    
}