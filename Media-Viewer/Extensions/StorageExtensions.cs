using MediaViewer.Controls.Dialogs;
using MediaViewer.Enums;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Pickers;
using Windows.Storage.Search;
using Windows.System;
using WinRT;
using Image = Microsoft.UI.Xaml.Controls.Image;

namespace MediaViewer.Extensions
{

    internal static class Files
    {

        public static async Task<StorageFile?> SelectFile()
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                FileTypeFilter = { ".JPG", ".JPEG", ".JPE", ".BMP", ".GIF", ".PNG", ".WEBP", ".FLV", ".AVI", ".MPG", ".MOV", ".WMV", ".MP4", ".M4V", ".MPEG", ".M4V", ".F4V", ".MKV" }
            };

            picker.SetOwnerWindow(App.Current.ActiveWindow);

            return await picker.PickSingleFileAsync();
        }


        public static async Task<StorageFile?> SelectExecutable()
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                FileTypeFilter = { ".exe" }
            };

            picker.SetOwnerWindow(App.Current.ActiveWindow);

            return await picker.PickSingleFileAsync();
        }


        public static String? GetAppDisplayName(string FilePath)
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(FilePath);
            string displayName = versionInfo.ProductName ?? versionInfo.FileDescription;

            return displayName;
        }


        public static async Task<ImageIcon> GetAppIconAsync(string FilePath)
        {
            var exe = await StorageFile.GetFileFromPathAsync(FilePath);
            var thumb = await exe.GetThumbnailAsync(ThumbnailMode.SingleItem, 64, ThumbnailOptions.UseCurrentScale);

            var bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(thumb);

            var ImageIcon = new ImageIcon
            {
                Source = bitmapImage
            };

            return ImageIcon;
        }



        /// <summary>
        /// Provide any image file or image URL and this will open it in the default photo viewer. 
        /// </summary>
        /// <param name="ImageFilePath"></param>
        public static async Task OpenImage(string FilePath)
        {
            try
            {
                StorageFile Image = await StorageFile.GetFileFromPathAsync(FilePath);

                StorageFolder TempFolder = await Folders.GetTempFolder();
                StorageFile NewFile = await Image.CopyAsync(TempFolder, Image.Name, NameCollisionOption.GenerateUniqueName);

                await Launcher.LaunchFileAsync(NewFile);              
            }
            catch (Exception e)
            {
                await MessageBox.Show("An error has occurred attempting to open the image");
            }
        }

        public static async Task Rotate(this Image image, StorageFile File, BitmapRotation rotation)
        {
            try
            {
                using var inputStream = await File.OpenAsync(FileAccessMode.ReadWrite);

                // Decode image
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(inputStream);

                // Re-encode with rotation applied
                BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(
                    inputStream,
                    decoder);

                encoder.BitmapTransform.Rotation = rotation;

                await encoder.FlushAsync();
            }
            catch (Exception ex)
            {
                await MessageBox.Show("An error occurred while rotating the image: " + ex.Message);
            }
        }


        /// <summary>
        /// Store files temporarily (ex: Webview2 2MB NavigateToString Bug)
        /// <see href="https://github.com/MicrosoftEdge/WebView2Feedback/issues/1355"/>
        /// </summary>
        /// <param name="FileName"></param>
        /// <param name="FileExtension"></param>
        /// <param name="Contents"></param>
        public static async Task<StorageFile> SaveTempFile(string FileName, string FileExtension, string Contents)
        {
            StorageFolder TempFolder = await Folders.GetTempFolder();

            StorageFile TempFile = await TempFolder.CreateFileAsync(FileName + FileExtension, CreationCollisionOption.GenerateUniqueName);
            await FileIO.WriteTextAsync(TempFile, Contents, Windows.Storage.Streams.UnicodeEncoding.Utf8);

            return TempFile;
        }

        /// <summary>
        /// Delete the temporary file once it has been used.
        /// </summary>
        /// <param name="FileName"></param>
        /// <param name="FileExtension"></param>
        public static async Task DeleteTempFile(string FileName, string FileExtension)
        {
            StorageFolder LocalFolder = ApplicationData.Current.LocalFolder;
            await LocalFolder.CreateFolderAsync("Temp", CreationCollisionOption.OpenIfExists);

            StorageFolder TempFolder = await LocalFolder.GetFolderAsync("Temp");

            StorageFile TabFile = await TempFolder.GetFileAsync(FileName + FileExtension);
            File.Delete(TabFile.Path);
        }



        private static PrivateFontCollection _privateFontCollection = new PrivateFontCollection();

        public static FontFamily GetFontFamilyByName(string name)
        {
            return _privateFontCollection.Families.FirstOrDefault(x => x.Name == name);
        }

        public static void AddFont(string fullFileName)
        {
            AddFont(File.ReadAllBytes(fullFileName));
        }

        public static void AddFont(byte[] fontBytes)
        {
            var handle = GCHandle.Alloc(fontBytes, GCHandleType.Pinned);
            IntPtr pointer = handle.AddrOfPinnedObject();
            try
            {
                _privateFontCollection.AddMemoryFont(pointer, fontBytes.Length);
            }
            finally
            {
                handle.Free();
            }
        }



        private static readonly List<string> ImageExtensions = new List<string> { ".JPG", ".JPEG", ".JPE", ".BMP", ".PNG", ".WEBP" };

        private static readonly List<string> GifExtensions = new List<string> { ".GIF", };

        private static readonly List<string> AudioExtensions = new List<string> { ".3GP", ".FLAC", ".M4A", ".MP2", ".MP3", ".WAV", ".WMA", ".WEBM" };

        private static readonly List<string> VideoExtensions = new List<string> { ".FLV", ".AVI", ".MPG", ".MOV", ".WMV", ".MP4", ".M4V", ".MPEG", ".F4V", ".MKV" };


        public static MediaType GetMediaType(string FilePath)
        {

            if (ImageExtensions.Contains(Path.GetExtension(FilePath).ToUpperInvariant()))
            {
                return MediaType.Image;
            }
            else if (GifExtensions.Contains(Path.GetExtension(FilePath).ToUpperInvariant()))
            {
                return MediaType.Gif;
            }
            else if (AudioExtensions.Contains(Path.GetExtension(FilePath).ToUpperInvariant()))
            {
                return MediaType.Audio;
            }
            else if (VideoExtensions.Contains(Path.GetExtension(FilePath).ToUpperInvariant()))
            {
                return MediaType.Video;
            }

            return MediaType.Unknown;
        }

        public static async Task<StorageFolder> GetFolder(this StorageFile File)
        {
            return await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(File.Path));
        }

    }

    internal static class Folders
    {

        /// <summary>
        /// A temporary folder is sometimes required for opening files without modifying the original.
        /// </summary>
        /// <returns></returns>
        public static async Task<StorageFolder> GetTempFolder()
        {
            StorageFolder LocalFolder = ApplicationData.Current.LocalFolder;
            await LocalFolder.CreateFolderAsync("Temp", CreationCollisionOption.OpenIfExists);

            StorageFolder TempFolder = await LocalFolder.GetFolderAsync("Temp");
            return TempFolder;
        }

        /// <summary>
        /// Clearing the temporary folder on startup will prevent the app file size from getting too large.
        /// </summary>
        public static async Task ClearTempFolder()
        {
            StorageFolder folder = await Folders.GetTempFolder();
            try
            {
                //Attempt to delete the folder
                await folder.DeleteAsync();

            }
            catch { }

        }


        public static async Task<StorageFolder> GetIconFolder()
        {
            StorageFolder LocalFolder = ApplicationData.Current.LocalFolder;
            await LocalFolder.CreateFolderAsync("Icons", CreationCollisionOption.OpenIfExists);

            StorageFolder IconFolder = await LocalFolder.GetFolderAsync("Icons");
            return IconFolder;
        }




        public static async Task<SortEntry?> GetSortOrder(string Path)
        {
            string path = System.IO.Path.GetDirectoryName(Path);
            int ascend = -1;
            StringBuilder sb = new StringBuilder(200);

            try
            {
                int res = GetExplorerSortOrder(path, ref sb, sb.Capacity, ref ascend);
                if (res == 0)
                {
                    string propertyName = sb.ToString();
                    return new SortEntry(propertyName, ascend > 0);
                }
                else
                {
                    return null;
                }
            }
            catch (BadImageFormatException)
            {
                // DLL architecture mismatch - log or handle appropriately
                Debug.WriteLine("explorer.dll architecture mismatch with application");
                return null;
            }
            catch (DllNotFoundException)
            {
                Debug.WriteLine("explorer.dll not found");
                return null;
            }
        }


        [DllImport(@"Dependencies\explorer.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern int GetExplorerSortOrder(string path, ref StringBuilder str, int len, ref Int32 ascend);


    }


    internal static class DataTransferManagerInterop
    {
        [System.Runtime.InteropServices.ComImport]
        [System.Runtime.InteropServices.Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
        [System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IDataTransferManagerInterop
        {
            IntPtr GetForWindow([System.Runtime.InteropServices.In] IntPtr appWindow, [System.Runtime.InteropServices.In] ref Guid riid);
            void ShowShareUIForWindow(IntPtr appWindow);
        }

        private static readonly Guid _dtm_iid = new Guid(0xa5caee9b, 0x8708, 0x49d1, 0x8d, 0x36, 0x67, 0xd2, 0x5a, 0x8d, 0xa0, 0x0c);

        public static DataTransferManager GetForWindow(IntPtr appWindow)
        {
            var dataTransferManagerInterop = DataTransferManager.As<IDataTransferManagerInterop>();
            IntPtr result = dataTransferManagerInterop.GetForWindow(appWindow, _dtm_iid);
            return WinRT.MarshalInterface<DataTransferManager>.FromAbi(result);
        }

        public static void ShowShareUIForWindow(IntPtr appWindow)
        {
            var dataTransferManagerInterop = DataTransferManager.As<IDataTransferManagerInterop>();
            dataTransferManagerInterop.ShowShareUIForWindow(appWindow);
        }
    }


    /// <summary>
    /// Required to set owner window for File Picker.
    /// </summary>
    public static class WindowsStoragePickerExtensions
    {
        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("EECDBF0E-BAE9-4CB6-A68E-9598E1CB57BB")]
        internal interface IWindowNative
        {
            IntPtr WindowHandle { get; }
        }

        /// <summary>
        /// Sets the owner window for this <see cref="FileOpenPicker"/>. This is required when running in WinUI for Desktop.
        /// </summary>
        public static void SetOwnerWindow(this FileOpenPicker picker, Window window)
        {
            SetOwnerWindow(picker.As<IInitializeWithWindow>(), window);
        }

        /// <summary>
        /// Sets the owner window for this <see cref="FileSavePicker"/>. This is required when running in WinUI for Desktop.
        /// </summary>
        public static void SetOwnerWindow(this FileSavePicker picker, Window window)
        {
            SetOwnerWindow(picker.As<IInitializeWithWindow>(), window);
        }

        /// <summary>
        /// Sets the owner window for this <see cref="FolderPicker"/>. This is required when running in WinUI for Desktop.
        /// </summary>
        public static void SetOwnerWindow(this FolderPicker picker, Window window)
        {
            SetOwnerWindow(picker.As<IInitializeWithWindow>(), window);
        }

        private static void SetOwnerWindow(IInitializeWithWindow picker, Window window)
        {
            // See https://github.com/microsoft/microsoft-ui-xaml/issues/4100#issuecomment-774346918
            picker.Initialize(window.As<IWindowNative>().WindowHandle);
        }
    }




}
