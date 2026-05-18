using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace GeomToPoly;

//https://github.com/LK-team/3D-Engine/blob/main/Window.cs
internal class SimpleHwndHost : HwndHost
{
    const int CS_VREDRAW = 0x1;
    const int CS_HREDRAW = 0x2;
    const int CS_SAVEBITS = 0x800;
    const int WS_VISIBLE = 0x10000000;
    const int WS_CHILD = 0x40000000;
    const int WS_CLIPSIBLINGS = 0x04000000;
    const int WM_PAINT = 0x000F;
    const int WM_ERASEBKGND = 0x0014;
    const int WM_USER = 0x0400;
    const int WM_MY_DUMMY_MSG = WM_USER + 1;
    IntPtr _hdc1;
    public int[] Pixels;
    GCHandle gcHandle;
    BITMAPINFO _bitmapInfo;
        
    static void SetBitmapInfo(ref BITMAPINFO info, int width, int height)
    {
        info.biHeader.biBitCount = 32;
        info.biHeader.biPlanes = 1;
        info.biHeader.biSize = 40;
        info.biHeader.biWidth = width;
        info.biHeader.biHeight = -height;
        info.biHeader.biSizeImage = (uint)(width * height) << 2;
    }
        
    public SimpleHwndHost()
    {
        _bitmapInfo = new BITMAPINFO();
        Pixels = new int[1920 * 1080];
        Array.Fill(Pixels,0);
        gcHandle = GCHandle.Alloc(Pixels, GCHandleType.Pinned);
        
    }
        
    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hwnd = CreateWindowEx(
            dwExStyle: 0, 
            lpClassName: "static", 
            lpWindowName: "", 
            dwStyle: WS_VISIBLE | WS_CHILD | WS_CLIPSIBLINGS,
            x: 0, 
            y: 0, 
            nWidth: 1920, 
            nHeight: 1080, 
            hWndParent: hwndParent.Handle, 
            hMenu: IntPtr.Zero, 
            hInstance: IntPtr.Zero, 
            lpParam: IntPtr.Zero
        );

        //_hdc1 = GetDC(_hwnd);
        return new HandleRef(wrapper: this, handle: _hwnd);
    }
    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        DestroyWindow(hwnd.Handle);
    }
    Random r = new Random();
    IntPtr _hwnd;

    public void Paint()
    {
        const uint RDW_INVALIDATE = 0x0001;
        const uint RDW_INTERNALPAINT = 0x0002;
        const uint RDW_ERASE = 0x0004;
        const uint RDW_UPDATENOW = 0x0100;
        RedrawWindow(_hwnd, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_UPDATENOW);
    
    }
    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch (msg)
        {
            case WM_PAINT:
                IntPtr hdc = BeginPaint(hwnd, out PAINTSTRUCT ps);
                var w = (int)Math.Min(ActualWidth, 1920);
                var h = (int)Math.Min(ActualHeight, 1080);
                SetBitmapInfo(ref _bitmapInfo,w,h);
                SetDIBitsToDevice(hdc, 0, 0, w, h, 0, 0, 0, (uint)h, Pixels, ref _bitmapInfo, 0);
                EndPaint(hwnd, ref ps);
                break;
                
            case WM_ERASEBKGND:
                break;
        }
        handled = false;
        
        return IntPtr.Zero;
    }
    [DllImport("user32.dll")]
    public static extern bool RedrawWindow(IntPtr hWnd, IntPtr lprcUpdate, IntPtr hrgnUpdate, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PAINTSTRUCT
    {
        public IntPtr hdc;
        public bool fErase;
        public RECT rcPaint;
        public bool fRestore;
        public bool fIncUpdate;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] rgbReserved;
    }
    [DllImport("user32.dll")]
    static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll")]
    static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("user32.dll")]
    static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(IntPtr hwnd);
        
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern IntPtr CreateWindowEx(
        uint dwExStyle,
        string lpClassName,
        string lpWindowName,
        uint dwStyle,
        int x,
        int y,
        int nWidth,
        int nHeight,
        IntPtr hWndParent,
        IntPtr hMenu,
        IntPtr hInstance,
        IntPtr lpParam);
        
    [DllImport("user32.dll")]
    static extern IntPtr GetDC(IntPtr hWnd);
        
    [DllImport("gdi32.dll")]
    static extern int SetDIBitsToDevice(IntPtr hdc, int xDest, int yDest, int w, int h, int xSrc, int ySrc, uint StartScan, uint cLines, int[] lpvBits, ref BITMAPINFO lpbmi, uint ColorUse);
   
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }
        
    [StructLayout(LayoutKind.Sequential)]
    struct BITMAPINFO
    {
        public BITMAPINFOHEADER biHeader;
        public int biColors;
    }
        
}