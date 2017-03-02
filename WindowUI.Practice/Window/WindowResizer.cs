using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace WindowUI.Practice
{
    public enum WindowDockPosition
    {
        Undocked,
        Left,
        Right,
    }

    public class WindowResizer
    {
        private Window mWindow;
        private Rect mScreenSize = new Rect();
        private int mEdgeTolerance = 2;
        private Matrix mTransformToDevice;
        private IntPtr mLastScreen;
        private WindowDockPosition mLastDock = WindowDockPosition.Undocked;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        static extern bool GetMonitorInfo(IntPtr hMonitor, MONITORINFO lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr MonitorFromPoint(POINT pt, MonitorOptions dwFlags);

        public event Action<WindowDockPosition> WindowDockChanged = (dock) => { };

        public WindowResizer(Window window)
        {
            mWindow = window;
            GetTransform();
            mWindow.SourceInitialized += Window_SourceInitialized;
            mWindow.SizeChanged += Window_SizeChanged;
        }

        private void GetTransform()
        {
            var source = PresentationSource.FromVisual(mWindow);

            mTransformToDevice = default(Matrix);

            if (source == null)
                return;

            mTransformToDevice = source.CompositionTarget.TransformFromDevice;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            var handle = (new WindowInteropHelper(mWindow)).Handle;
            var handleSource = HwndSource.FromHwnd(handle);

            if (handleSource == null)
                return;

            handleSource.AddHook(WindowProc);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (mTransformToDevice == default(Matrix))
                return;

            var size = e.NewSize;

            double top = mWindow.Top;
            double left = mWindow.Left;
            double bottom = top + size.Height;
            double right = left + mWindow.Width;

            var windowTopLeft = mTransformToDevice.Transform(new Point(left, top));
            var windowBottomRight = mTransformToDevice.Transform(new Point(right, bottom));

            bool edgeTop = windowTopLeft.Y <= (mScreenSize.Top + mEdgeTolerance);
            bool edgeLeft = windowTopLeft.X <= (mScreenSize.Left + mEdgeTolerance);
            bool edgeBottom = windowBottomRight.Y >= (mScreenSize.Bottom - mEdgeTolerance);
            bool edgeRight = windowBottomRight.X >= (mScreenSize.Right - mEdgeTolerance);

            var dock = WindowDockPosition.Undocked;

            if (edgeTop && edgeBottom && edgeLeft)
                dock = WindowDockPosition.Left;
            else if (edgeTop && edgeBottom && edgeRight)
                dock = WindowDockPosition.Right;
            else
                dock = WindowDockPosition.Undocked;

            if (dock != mLastDock)
                WindowDockChanged(dock);

            mLastDock = dock;
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case 0x0024:
                    WmGetMinMaxInfo(hwnd, lParam);
                    handled = true;
                    break;
            }

            return (IntPtr)0;
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            POINT lMousePosition;
            GetCursorPos(out lMousePosition);

            var lPrimaryScreen = MonitorFromPoint(new POINT(0, 0), MonitorOptions.MONITOR_DEFAULTTOPRIMARY);

            var lPrimaryScreenInfo = new MONITORINFO();
            if (!GetMonitorInfo(lPrimaryScreen, lPrimaryScreenInfo))
                return;

            var lCurrentScreen = MonitorFromPoint(lMousePosition, MonitorOptions.MONITOR_DEFAULTTONEAREST);

            if (lCurrentScreen != mLastScreen || mTransformToDevice == default(Matrix))
                GetTransform();

            mLastScreen = lCurrentScreen;

            var lMmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO));

            if (lPrimaryScreen.Equals(lCurrentScreen))
            {
                lMmi.ptMaxPosition.X = lPrimaryScreenInfo.rcWork.Left;
                lMmi.ptMaxPosition.Y = lPrimaryScreenInfo.rcWork.Top;
                lMmi.ptMaxSize.X = lPrimaryScreenInfo.rcWork.Right - lPrimaryScreenInfo.rcWork.Left;
                lMmi.ptMaxSize.Y = lPrimaryScreenInfo.rcWork.Bottom - lPrimaryScreenInfo.rcWork.Top;
            }
            else
            {
                lMmi.ptMaxPosition.X = lPrimaryScreenInfo.rcMonitor.Left;
                lMmi.ptMaxPosition.Y = lPrimaryScreenInfo.rcMonitor.Top;
                lMmi.ptMaxSize.X = lPrimaryScreenInfo.rcMonitor.Right - lPrimaryScreenInfo.rcMonitor.Left;
                lMmi.ptMaxSize.Y = lPrimaryScreenInfo.rcMonitor.Bottom - lPrimaryScreenInfo.rcMonitor.Top;
            }

            var minSize = mTransformToDevice.Transform(new Point(mWindow.MinWidth, mWindow.MinHeight));

            lMmi.ptMinTrackSize.X = (int)minSize.X;
            lMmi.ptMinTrackSize.Y = (int)minSize.Y;

            mScreenSize = new Rect(lMmi.ptMaxPosition.X, lMmi.ptMaxPosition.Y, lMmi.ptMaxSize.X, lMmi.ptMaxSize.Y);

            Marshal.StructureToPtr(lMmi, lParam, true);
        }

        enum MonitorOptions : uint
        {
            MONITOR_DEFAULTTONULL = 0x00000000,
            MONITOR_DEFAULTTOPRIMARY = 0x00000001,
            MONITOR_DEFAULTTONEAREST = 0x00000002,
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public class MONITORINFO
        {
            public int cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            public Rectangle rcMonitor = new Rectangle();
            public Rectangle rcWork = new Rectangle();
            public int dwFlags = 0;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Rectangle
        {
            public int Left, Top, Right, Bottom;

            public Rectangle(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }
    }
}
