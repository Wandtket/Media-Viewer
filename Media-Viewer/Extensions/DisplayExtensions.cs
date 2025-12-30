using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System;
using System.Runtime.InteropServices;
using Windows.Graphics.Display;
using WinRT.Interop;

namespace MediaStudio.Extensions
{

    public static class DisplayExtensions
    {

        public static DisplayOrientations CurrentOrientation;

        public static string CurrentResolution { get { return CurrentResolutionWidth.ToString() + " x " + CurrentResolutionHeight.ToString(); } }

        public static int CurrentResolutionWidth;
        public static int CurrentResolutionHeight;


        /// <summary>
        /// Sets the display information of the primary display.
        /// </summary>
        public static void Initialize()
        {
            UpdateDisplayInformation();
        }

        /// <summary>
        /// Manually invoke the event.
        /// </summary>
        public static void Invoke()
        {
            UpdateDisplayInformation();
        }


        /// <summary>
        /// To be used inside a Windows Size Changed Event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        public static void CheckForOrientationUpdate(object sender, WindowSizeChangedEventArgs args)
        {
            UpdateDisplayInformation();
        }


        /// <summary>
        /// Determines if the current device is a touchscreen.
        /// </summary>
        /// <returns></returns>
        public static bool IsTouchScreen()
        {
            const int MAXTOUCHES_INDEX = 95;
            int maxTouches = GetSystemMetrics(MAXTOUCHES_INDEX);

            return maxTouches > 0;
        }

        public static bool IsPortrait()
        {
            var WindowSize = App.Current.ActiveWindow.GetAppWindow().ClientSize;

            if (WindowSize.Height > WindowSize.Width)
            {
                return true;
            }
            else { return false; }
        }


        /// <summary>
        /// Detect Screen Orientation changes. (The event will be cleared whenever the app navigates, and the ResolutionWidth & Height will change with orientation.)
        /// </summary>
        public static event OrientationChangedEventHandler? OrientationChanged;
        public delegate void OrientationChangedEventHandler(DisplayOrientations Orientation, int Width, int Height);

        /// <summary>
        /// Clearing the Orientation Events prevents memory leaks.
        /// </summary>
        public static void ClearOrientationEvents()
        {
            if (OrientationChanged != null)
            {
                OrientationChanged = (OrientationChangedEventHandler?)Delegate.RemoveAll(OrientationChanged, OrientationChanged);
            }
        }


        private static void UpdateDisplayInformation()
        {
            DEVMODE devmode = new DEVMODE();
            devmode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
            bool bRet = EnumDisplaySettingsEx(IntPtr.Zero, ENUM_CURRENT_SETTINGS, ref devmode, 0);
            if (!bRet)
            {
                devmode.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                bRet = EnumDisplaySettingsEx(IntPtr.Zero, ENUM_REGISTRY_SETTINGS, ref devmode, 0);
            }

            CurrentResolutionWidth = devmode.dmPelsWidth;
            CurrentResolutionHeight = devmode.dmPelsHeight;

            float nRatio = (float)((float)devmode.dmPelsWidth / (float)devmode.dmPelsHeight);
            if (nRatio > 1)
            {
                if (CurrentOrientation == DisplayOrientations.Portrait)
                {
                    CurrentOrientation = DisplayOrientations.Landscape;
                    OrientationChanged?.Invoke(DisplayOrientations.Landscape, devmode.dmPelsWidth, devmode.dmPelsHeight);
                }
            }
            else
            {
                if (CurrentOrientation == DisplayOrientations.Landscape)
                {

                    CurrentOrientation = DisplayOrientations.Portrait;
                    OrientationChanged?.Invoke(DisplayOrientations.Portrait, devmode.dmPelsWidth, devmode.dmPelsHeight);
                }
            }
        }




        #region Interop & User32


        [DllImport("Shcore.dll", SetLastError = true)]
        internal static extern int GetDpiForMonitor(IntPtr hmonitor, Monitor_DPI_Type dpiType, out uint dpiX, out uint dpiY);

        internal enum Monitor_DPI_Type : int
        {
            MDT_Effective_DPI = 0,
            MDT_Angular_DPI = 1,
            MDT_Raw_DPI = 2,
            MDT_Default = MDT_Effective_DPI
        }

        public static double GetScaleAdjustment()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(App.Current.ActiveWindow);
            WindowId wndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            DisplayArea displayArea = DisplayArea.GetFromWindowId(wndId, DisplayAreaFallback.Primary);
            IntPtr hMonitor = Win32Interop.GetMonitorFromDisplayId(displayArea.DisplayId);

            // Get DPI.
            int result = GetDpiForMonitor(hMonitor, Monitor_DPI_Type.MDT_Default, out uint dpiX, out uint _);
            if (result != 0)
            {
                throw new Exception("Could not get DPI for monitor.");
            }

            uint scaleFactorPercent = (uint)(((long)dpiX * 100 + (96 >> 1)) / 96);
            return scaleFactorPercent / 100.0;
        }



        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private static IntPtr WindowProcess(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
        {
            switch (message)
            {
                case WM_DISPLAYCHANGE:
                    CheckForOrientationUpdate(null, null);
                    break;
                default:
                    break;
            }
            return Interop.CallWindowProc(_oldWndProc, hwnd, message, wParam, lParam);
        }


        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettingsEx(IntPtr lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode, int dwFlags);


        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int ENUM_REGISTRY_SETTINGS = -2;

        private const int EDS_RAWMODE = 0x00000002;
        private const int EDS_ROTATEDMODE = 0x00000004;

        private const int WM_DISPLAYCHANGE = 0x007E;

        private static IntPtr _oldWndProc;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            private const int CCHDEVICENAME = 32;
            private const int CCHFORMNAME = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public DISPLAYORIENTATION dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHFORMNAME)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        private enum DISPLAYORIENTATION : int
        {
            DMDO_DEFAULT = 0,
            DMDO_90 = 1,
            DMDO_180 = 2,
            DMDO_270 = 3
        }

        [ComImport, Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ICoreWindowInterop
        {
            IntPtr WindowHandle { get; }
            bool MessageHandled { get; }
        }

        private class Interop
        {
            [DllImport("user32.dll", EntryPoint = "SetWindowLong")] //32-bit
            public static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")] // 64-bit
            public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

            [DllImport("user32.dll", CharSet = CharSet.Auto)]
            public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam);
        }

        private static class WndProc
        {
            public delegate IntPtr WndProcDelegate(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
            private const int GWLP_WNDPROC = -4;

            public static IntPtr _coreWindowHwnd;

            private static WndProcDelegate _currDelegate = null;

            public static IntPtr SetWndProc(WndProcDelegate newProc)
            {
                _currDelegate = newProc;

                IntPtr newWndProcPtr = Marshal.GetFunctionPointerForDelegate(newProc);

                if (IntPtr.Size == 8)
                {
                    return Interop.SetWindowLongPtr64(_coreWindowHwnd, GWLP_WNDPROC, newWndProcPtr);
                }
                else
                {
                    return Interop.SetWindowLong32(_coreWindowHwnd, GWLP_WNDPROC, newWndProcPtr);
                }
            }

        }

        #endregion
    }
}
