using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace OnScreenOCR
{
    class Native
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        public static extern int SetLayeredWindowAttributes(IntPtr hWnd, int crKey, byte bAlpha, int dwFlags);

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_LAYERED = 0x80000;
        public const int WS_EX_TRANSPARENT = 0x20;
        public const int WS_EX_TOPMOST = 0x8;
        public const int LWA_COLORKEY = 0x00000001;
        public const int WM_NCHITTEST = 0x0084;
        public const int HTTRANSPARENT = (-1);
    }
}
