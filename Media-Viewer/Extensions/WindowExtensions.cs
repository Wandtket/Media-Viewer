using MediaViewer.Enums;
using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;



namespace MediaViewer.Extensions
{


    public static class WindowExtensions
    {

        #region Window Helpers

        /// <summary>
        /// Used to get the native Window Handler.
        /// </summary>
        /// <param name="window"></param>
        /// <returns></returns>
        public static AppWindow GetAppWindow(this Window window)
        {
            var hwndd = new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(window));
            WindowId wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwndd);
            return AppWindow.GetFromWindowId(wndId);
        }

        /// <summary>
        /// Maximizes the Window
        /// </summary>
        /// <param name="window"></param>
        public static void Maximize(this Window window)
        {
            var hwndd = new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(window));
            PInvoke.ShowWindow(hwndd, SHOW_WINDOW_CMD.SW_MAXIMIZE);
        }


        /// <summary>
        /// Maximizes the Window
        /// </summary>
        /// <param name="window"></param>
        public static void Resize(this Window window, int Height, int Width)
        {
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32(Width, Height));
        }

        /// <summary>
        /// Resize the window based on image / video dimensions
        /// </summary>
        /// <param name="window"></param>
        /// <param name="File"></param>
        public static async Task Resize(this Window window, StorageFile File)
        {
            var Type = Files.GetMediaType(File.Path);

            // Get monitor bounds where mouse cursor is located
            RECT monitorBounds = GetMouseMonitorBounds();
            int monitorWidth = monitorBounds.Right - monitorBounds.Left;
            int monitorHeight = monitorBounds.Bottom - monitorBounds.Top;

            if (Type == MediaType.Image || Type == MediaType.Gif)
            {
                var ImageProperties = await File.Properties.GetImagePropertiesAsync();
                int imageHeight = (int)ImageProperties.Height;
                int imageWidth = (int)ImageProperties.Width;

                if (imageHeight <= 0 || imageWidth <= 0)
                    return;

                // Determine scaling multiplier based on resolution tiers
                int scaleMultiplier = GetScaleMultiplier(imageHeight, imageWidth);

                // Apply scaling
                int scaledImageWidth = imageWidth * scaleMultiplier;
                int scaledImageHeight = imageHeight * scaleMultiplier;

                // Check if scaled image is 75% or greater of monitor size
                double widthRatio = (double)scaledImageWidth / monitorWidth;
                double heightRatio = (double)scaledImageHeight / monitorHeight;
                bool isTooLarge = widthRatio >= 0.75 || heightRatio >= 0.75;

                int targetWidth;
                int targetHeight;

                if (isTooLarge)
                {
                    // Shrink to 50% of monitor size while preserving aspect ratio
                    double scale = Math.Min((monitorWidth * 0.5) / scaledImageWidth, (monitorHeight * 0.75) / scaledImageHeight);
                    targetWidth = Math.Max(100, (int)Math.Round(scaledImageWidth * scale));
                    targetHeight = Math.Max(100, (int)Math.Round(scaledImageHeight * scale));
                }
                else if (scaledImageWidth > monitorWidth || scaledImageHeight > monitorHeight)
                {
                    // Shrink to fit within monitor bounds while preserving aspect ratio
                    double scale = Math.Min((double)monitorWidth / scaledImageWidth, (double)monitorHeight / scaledImageHeight);
                    scale = Math.Min(scale, 1.0); // Don't upscale
                    targetWidth = Math.Max(100, (int)Math.Round(scaledImageWidth * scale));
                    targetHeight = Math.Max(100, (int)Math.Round(scaledImageHeight * scale));
                }
                else
                {
                    // Use scaled dimensions as-is
                    targetWidth = scaledImageWidth;
                    targetHeight = scaledImageHeight;
                }

                // Add a small padding to account for window chrome/title bar if desired.
                const int paddingWidth = 20;
                const int paddingHeight = 40;

                // Use the existing Resize(height, width) helper (height first).
                window.Resize(targetHeight + paddingHeight, targetWidth + paddingWidth);
            }
            else if (Type == MediaType.Video)
            {
                var videoProps = await File.Properties.GetVideoPropertiesAsync();
                int videoHeight = (int)videoProps.Height;
                int videoWidth = (int)videoProps.Width;

                if (videoHeight <= 0 || videoWidth <= 0)
                    return;

                // Determine scaling multiplier based on resolution tiers
                int scaleMultiplier = GetScaleMultiplier(videoHeight, videoWidth);

                // Apply scaling
                int scaledVideoWidth = videoWidth * scaleMultiplier;
                int scaledVideoHeight = videoHeight * scaleMultiplier;

                // Check if scaled video is 75% or greater of monitor size
                double widthRatio = (double)scaledVideoWidth / monitorWidth;
                double heightRatio = (double)scaledVideoHeight / monitorHeight;
                bool isTooLarge = widthRatio >= 0.75 || heightRatio >= 0.75;

                int targetWidth;
                int targetHeight;

                if (isTooLarge)
                {
                    // Shrink to 50% of monitor size while preserving aspect ratio
                    double scale = Math.Min((monitorWidth * 0.5) / scaledVideoWidth, (monitorHeight * 0.5) / scaledVideoHeight);
                    targetWidth = Math.Max(100, (int)Math.Round(scaledVideoWidth * scale));
                    targetHeight = Math.Max(100, (int)Math.Round(scaledVideoHeight * scale));
                }
                else if (scaledVideoWidth > monitorWidth || scaledVideoHeight > monitorHeight)
                {
                    // Shrink to fit within monitor bounds while preserving aspect ratio
                    double scale = Math.Min((double)monitorWidth / scaledVideoWidth, (double)monitorHeight / scaledVideoHeight);
                    scale = Math.Min(scale, 1.0); // Don't upscale
                    targetWidth = Math.Max(100, (int)Math.Round(scaledVideoWidth * scale));
                    targetHeight = Math.Max(100, (int)Math.Round(scaledVideoHeight * scale));
                }
                else
                {
                    // Use scaled dimensions as-is
                    targetWidth = scaledVideoWidth;
                    targetHeight = scaledVideoHeight;
                }

                // Add a small padding to account for window chrome/title bar.
                const int paddingWidthVideo = 20;
                const int paddingHeightVideo = 40;

                window.Resize(targetHeight + paddingHeightVideo, targetWidth + paddingWidthVideo);
            }
        }

        /// <summary>
        /// Determines the scaling multiplier based on resolution tiers
        /// </summary>
        /// <param name="height">Media height in pixels</param>
        /// <param name="width">Media width in pixels</param>
        /// <returns>Scale multiplier (1x, 2x, 3x, or 4x)</returns>
        private static int GetScaleMultiplier(int height, int width)
        {
            // 144p or less (256x144): 4x scaling
            if (height <= 144 || width <= 256)
                return 4;

            // 240p or less (426x240): 3x scaling
            if (height <= 240 || width <= 426)
                return 3;

            // 480p or less (854x480): 3x scaling
            if (height <= 480 || width <= 854)
                return 3;

            // 720p or less (1280x720): 2x scaling
            if (height < 720 || width < 1280)
                return 2;

            // 720p and above: no scaling
            return 1;
        }


        /// <summary>
        /// Replaces default TitleBar in Window
        /// </summary>
        /// <param name="window"></param>
        public static void ExtendContentIntoTitleBar(this Window window)
        {
            if (AppWindowTitleBar.IsCustomizationSupported())
            {
                var m_AppWindow = GetAppWindow(window);

                var titleBar = m_AppWindow.TitleBar;
                titleBar.ExtendsContentIntoTitleBar = true;
                titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;                

                var Foreground = Application.Current.Resources["Foreground"] as SolidColorBrush;
                if (Foreground != null) { titleBar.ButtonForegroundColor = Foreground.Color; };

                titleBar.ButtonBackgroundColor = Colors.Transparent;
                titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                var brush = Application.Current.Resources["BoxColor"] as SolidColorBrush;
                if (brush != null) { titleBar.ButtonHoverBackgroundColor = brush.Color; }
            }
        }

        #endregion


        #region Backdrop Helpers

        public static void SetBackdrop(this Window window, Backdrop backdrop)
        {
            if (backdrop == Backdrop.Mica)
            {
                EnableMICABackdrop(window);
            }
            else if (backdrop == Backdrop.Accrylic)
            {
                EnableAcryllicBackdrop(window);
            }
            else if (backdrop == Backdrop.System)
            {
                EnableCustomBackdrop(window);
            }
        }


        /// <summary>
        /// Enables MICA Backdrop on MainWindow
        /// </summary>
        /// <param name="window"></param>
        public static void EnableMICABackdrop(this Window window)
        {
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                MicaBackdrop micaBackdrop = new MicaBackdrop();
                micaBackdrop.Kind = MicaKind.BaseAlt;
                window.SystemBackdrop = micaBackdrop;

                Application.Current.Resources["GridBackdrop"] = new SolidColorBrush(Colors.Transparent);
            }
        }


        /// <summary>
        /// Enables Accrylic Backdrop on MainWindow
        /// </summary>
        /// <param name="window"></param>
        public static void EnableAcryllicBackdrop(this Window window)
        {
            if (DesktopAcrylicController.IsSupported())
            {
                DesktopAcrylicBackdrop DesktopAcrylicBackdrop = new DesktopAcrylicBackdrop();                
                window.SystemBackdrop = DesktopAcrylicBackdrop;

                Application.Current.Resources["GridBackdrop"] = new SolidColorBrush(Colors.Transparent);
            }
        }


        public static void EnableCustomBackdrop(this Window window)
        {
            if (App.Current.RequestedTheme == ApplicationTheme.Dark)
            {
                Application.Current.Resources["GridBackdrop"] = new SolidColorBrush(Colors.Black);
            }
            else if (App.Current.RequestedTheme == ApplicationTheme.Light)
            {
                Application.Current.Resources["GridBackdrop"] = new SolidColorBrush(Colors.White);
            }
        }

        #endregion


        #region Mouse Monitor Helpers

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public uint cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }



        /// <summary>
        /// Gets the work area bounds of the monitor where the mouse cursor is currently located.
        /// </summary>
        /// <returns>RECT structure containing the monitor's work area bounds (excluding taskbar)</returns>
        public static RECT GetMouseMonitorBounds()
        {
            GetCursorPos(out POINT cursorPos);
            IntPtr hMonitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);

            MONITORINFO monitorInfo = new MONITORINFO();
            monitorInfo.cbSize = (uint)Marshal.SizeOf(typeof(MONITORINFO));

            if (GetMonitorInfo(hMonitor, ref monitorInfo))
            {
                return monitorInfo.rcWork; // rcWork excludes taskbar, rcMonitor includes it
            }

            return default;
        }


        /// <summary>
        /// Moves the window to the monitor where the mouse cursor is located without changing its size.
        /// Centers the window on that monitor.
        /// </summary>
        /// <param name="window">The window to move</param>
        public static void MoveToMouseMonitor(this Window window)
        {
            RECT monitorBounds = GetMouseMonitorBounds();

            var appWindow = window.GetAppWindow();

            // Use provided dimensions or current window size
            int windowWidth = appWindow.Size.Width;
            int windowHeight = appWindow.Size.Height;

            // Calculate center position on the monitor
            int centerX = monitorBounds.Left + (monitorBounds.Right - monitorBounds.Left - windowWidth) / 2;
            int centerY = monitorBounds.Top + (monitorBounds.Bottom - monitorBounds.Top - windowHeight) / 2;

            // Move to calculated position
            appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
        }

        #endregion


        #region Dark Mode Helpers

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        public static bool UseImmersiveDarkMode(IntPtr handle, bool enabled)
        {
            if (IsWindows10OrGreater(17763))
            {
                var attribute = DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1;
                if (IsWindows10OrGreater(18985))
                {
                    attribute = DWMWA_USE_IMMERSIVE_DARK_MODE;
                }

                int useImmersiveDarkMode = enabled ? 1 : 0;
                return DwmSetWindowAttribute(handle, attribute, ref useImmersiveDarkMode, sizeof(int)) == 0;
            }

            return false;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        private static bool IsWindows10OrGreater(int build = -1)
        {
            return Environment.OSVersion.Version.Major >= 10 && Environment.OSVersion.Version.Build >= build;
        }

        #endregion

    }


    class WindowsSystemDispatcherQueueHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DispatcherQueueOptions
        {
            internal int dwSize;
            internal int threadType;
            internal int apartmentType;
        }

        [DllImport("CoreMessaging.dll")]
        private static extern int CreateDispatcherQueueController([In] DispatcherQueueOptions options, [In, Out, MarshalAs(UnmanagedType.IUnknown)] ref object dispatcherQueueController);

        object m_dispatcherQueueController = null;
        public void EnsureWindowsSystemDispatcherQueueController()
        {
            if (Windows.System.DispatcherQueue.GetForCurrentThread() != null)
            {
                // one already exists, so we'll just use it.
                return;
            }

            if (m_dispatcherQueueController == null)
            {
                DispatcherQueueOptions options;
                options.dwSize = Marshal.SizeOf(typeof(DispatcherQueueOptions));
                options.threadType = 2;    // DQTYPE_THREAD_CURRENT
                options.apartmentType = 2; // DQTAT_COM_STA

                CreateDispatcherQueueController(options, ref m_dispatcherQueueController);
            }
        }
   
    }

}
