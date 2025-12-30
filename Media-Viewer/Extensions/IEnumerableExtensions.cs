using MediaStudio.Controls.Dialogs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.System;
using CsvHelper;


namespace MediaStudio.Extensions
{

    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Creates a CSV file with the records being from <paramref name="items"/>.
        /// </summary>
        /// <param name="Filename"></param>
        /// <param name="startLocation"></param>
        /// <returns>Whether the export succeeded or not.</returns>
        public static async Task<bool> ExportToExcel(this IEnumerable enumerable, string Filename,
            PickerLocationId startLocation = PickerLocationId.DocumentsLibrary,
            bool IncludeDate = true, string? Macro = null)
        {
            if (IncludeDate == true) { Filename = Filename + " " + DateTime.Now.ToString("MM-dd-yyyy"); }

            FileSavePicker picker = new FileSavePicker()
            {
                SuggestedStartLocation = startLocation,
                FileTypeChoices =
                {
                    ["CSV File"] = new List<string>() { ".csv" }
                },
                SuggestedFileName = Filename
            };

            picker.SetOwnerWindow(App.Current.ActiveWindow);

            var SaveFile = await picker.PickSaveFileAsync();
            if (SaveFile == null)
            {
                return false;
            }

            using (var writer = new StreamWriter(SaveFile.Path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(enumerable);
            }         

            return true;
        }

        /// <summary>
        /// Opens a CSV file in excel with the records being from <paramref name="items"/>.
        /// </summary>
        /// <param name="enumerable"></param>
        /// <param name="Filename"></param>
        /// <param name="IncludeDate"></param>
        /// <param name="Macro"></param>
        /// <returns></returns>
        public static async Task<bool> OpenInExcel(this IEnumerable enumerable, string Filename, bool IncludeDate = true)
        {
            if (IncludeDate == true) { Filename = Filename + " " + DateTime.Now.ToString("MM-dd-yyyy"); }

            IStorageFile file = await Files.SaveTempFile(Filename, ".csv", "");

            using (var writer = new StreamWriter(file.Path))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                
                await csv.WriteRecordsAsync(enumerable);
            }

            //Application picker is necessary in case the user has never opened a CSV file before.
            await Launcher.LaunchFileAsync(file, new LauncherOptions() { DisplayApplicationPicker = true });
            
            return true;
        }

    }
}
