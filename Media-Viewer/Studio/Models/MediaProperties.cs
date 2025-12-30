using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.Controls;
using MediaStudio.Controls.Dialogs;
using MediaStudio.Extensions;
using MediaStudio.Studio.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace MediaStudio.Studio.Models
{

    public partial class MediaProperties : ObservableObject
    {

        public MediaProperties()
        {

        }

        public MediaProperties(StorageFile file)
        {
            GetProperties(file);
        }


        [ObservableProperty]
        private string displayName;

        [ObservableProperty]
        private string fileName;

        [ObservableProperty]
        private string extension;

        [ObservableProperty]
        private string path;

        [ObservableProperty]
        private MediaType mediaType;

        [ObservableProperty]
        private bool metaDataEditable;


        [ObservableProperty]
        [PropertyTopic("System.Title")]
        private string? title;
        partial void OnTitleChanged(string? value) { SaveTopicValue(Path, nameof(title), value); }


        [ObservableProperty]
        [PropertyTopic("System.Subject")]
        private string? subject;
        partial void OnSubjectChanged(string? value) { SaveTopicValue(Path, nameof(subject), value); }


        [ObservableProperty]
        [PropertyTopic("System.Author")]
        private string? author;
        partial void OnAuthorChanged(string? value) { SaveTopicValue(Path, nameof(author), value); }


        [ObservableProperty]
        [PropertyTopic("System.Comment")]
        private string? comments;
        partial void OnCommentsChanged(string? value) { SaveTopicValue(Path, nameof(comments), value); }


        [ObservableProperty]
        [PropertyTopic("System.Rating")]
        private UInt32? rating;
        partial void OnRatingChanged(UInt32? value) { SaveTopicValue(Path, nameof(rating), value); }



        [ObservableProperty]
        private DateTimeOffset dateModified;

        [ObservableProperty]
        private TimeSpan timeModified;

        [ObservableProperty]
        private string size;

        [ObservableProperty]
        private string source;


        [ObservableProperty]
        private string resolution;

        [ObservableProperty]
        private string aspectRatio;

        [ObservableProperty]
        private string dPI;

        [ObservableProperty]
        private string bitDepth;


        [ObservableProperty]
        private string frameRate;



        /// <summary>
        /// Retrieves properties associated with the file.
        /// https://learn.microsoft.com/en-us/windows/win32/properties/core-bumper
        /// </summary>
        /// <param name="File"></param>
        private async void GetProperties(StorageFile File)
        {
            try
            {
                var BasicProperties = await File.GetBasicPropertiesAsync();

                FileName = File.DisplayName + File.FileType;
                DisplayName = File.DisplayName;
                Extension = File.FileType;
                Path = File.Path;

                MediaType = Files.GetMediaType(File.Path);
                DetermineMetaDataSupport();

                DateModified = BasicProperties.DateModified.DateTime;
                TimeModified = BasicProperties.DateModified.TimeOfDay;
                Size = BasicProperties.Size.toByteString();

                Title = NormalizeToString(await FetchTopicValue(File, GetPropertyTopic(nameof(title))));
                Subject = NormalizeToString(await FetchTopicValue(File, GetPropertyTopic(nameof(subject))));
                Rating = NormalizeToUInt32(await FetchTopicValue(File, GetPropertyTopic(nameof(rating))));
                Author = NormalizeToString(await FetchTopicValue(File, GetPropertyTopic(nameof(author))));
                Comments = NormalizeToString(await FetchTopicValue(File, GetPropertyTopic(nameof(comments))));

                if (File.Path.StartsWith("C")) { Source = "This PC"; }
                else if (File.Path.StartsWith("http")) { Source = "Web"; }
                else { Source = "Network"; }

                if (MediaType == MediaType.Image ||
                    MediaType == MediaType.Gif)
                {
                    var ImageProperties = await File.Properties.GetImagePropertiesAsync();
                    using (IRandomAccessStream stream = await File.OpenAsync(FileAccessMode.Read))
                    {
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                        var pixelFormat = decoder.BitmapPixelFormat;
                        int bitsPerPixel = pixelFormat switch
                        {
                            BitmapPixelFormat.Bgra8 => 32,
                            BitmapPixelFormat.Rgba8 => 32,
                            BitmapPixelFormat.Rgba16 => 64,
                            BitmapPixelFormat.Gray8 => 8,
                            BitmapPixelFormat.Gray16 => 16,
                            BitmapPixelFormat.Nv12 => 12,  // Special case for YUV
                            _ => 32 // default assumption
                        };

                        bool hasAlpha = pixelFormat == BitmapPixelFormat.Bgra8 ||
                                          pixelFormat == BitmapPixelFormat.Rgba8 ||
                                          pixelFormat == BitmapPixelFormat.Rgba16;

                        int windowsReportedBitDepth = hasAlpha ? 24 : 32;
                        if (pixelFormat == BitmapPixelFormat.Rgba16) windowsReportedBitDepth = 64;

                        DPI = decoder.DpiX.ToString() + " dpi";
                        Resolution = ImageProperties.Width + " x " + ImageProperties.Height;
                        AspectRatio = CalculateAspectRatio((int)ImageProperties.Width, (int)ImageProperties.Height);
                        BitDepth = windowsReportedBitDepth.ToString() + " bit";

                        stream.Dispose();
                    }
                }
                else if (MediaType == MediaType.Video)
                {
                    var VideoProperties = await File.Properties.GetVideoPropertiesAsync();
                    var propsToRetrieve = new[] { "System.Video.FrameRate" };

                    IDictionary<string, object> props =
                            await File.Properties.RetrievePropertiesAsync(propsToRetrieve);

                    if (props.ContainsKey("System.Video.FrameRate"))
                    {
                        uint rawFrameRateX1000 = (uint)props["System.Video.FrameRate"];
                        double frameRate = rawFrameRateX1000 / 1000.0;
                        DPI = frameRate.ToString();
                    }

                    Resolution = VideoProperties.Width + " x " + VideoProperties.Height;
                    AspectRatio = CalculateAspectRatio((int)VideoProperties.Width, (int)VideoProperties.Height);
                }
            }
            catch (Exception ex)
            {
                await ErrorBox.Show(ex, Title: "Error Retrieving Media Properties");
            }
        }


        /// <summary>
        /// Calculates the aspect ratio in simplified form, rounding to standard ratios when close
        /// (e.g., "16:9", "4:3", "21:9", "1:1")
        /// </summary>
        /// <param name="width">Width in pixels</param>
        /// <param name="height">Height in pixels</param>
        /// <returns>Formatted aspect ratio string</returns>
        private static string CalculateAspectRatio(int width, int height)
        {
            if (width <= 0 || height <= 0)
                return string.Empty;

            // Calculate the actual ratio
            double actualRatio = (double)width / height;

            // Define standard aspect ratios with tolerance
            var standardRatios = new[]
            {
                (ratio: 16.0 / 9.0, display: "16:9"),      // 1.778
                (ratio: 21.0 / 9.0, display: "21:9"),      // 2.333
                (ratio: 9.0 / 16.0, display: "9:16"),      // 0.563
                (ratio: 4.0 / 3.0, display: "4:3"),        // 1.333
                (ratio: 3.0 / 4.0, display: "3:4"),        // 0.75
                (ratio: 3.0 / 2.0, display: "3:2"),        // 1.5
                (ratio: 2.0 / 3.0, display: "2:3"),        // 0.667
                (ratio: 1.0, display: "1:1"),              // 1.0
                (ratio: 2.35 / 1.0, display: "2.35:1"),    // 2.35 (Cinemascope)
                (ratio: 2.39 / 1.0, display: "2.39:1"),    // 2.39 (Anamorphic)
                (ratio: 2.40 / 1.0, display: "2.40:1"),    // 2.40 (Widescreen)
                (ratio: 1.85 / 1.0, display: "1.85:1"),    // 1.85 (American widescreen)
            };

            // Tolerance for matching (within 2%)
            const double tolerance = 0.02;

            // Check if the ratio matches any standard ratio
            foreach (var standard in standardRatios)
            {
                double difference = Math.Abs(actualRatio - standard.ratio);
                double percentDifference = difference / standard.ratio;

                if (percentDifference <= tolerance)
                {
                    return standard.display;
                }
            }

            // If no standard ratio matches, calculate GCD and return simplified ratio
            int gcd = GCD(width, height);
            int aspectWidth = width / gcd;
            int aspectHeight = height / gcd;

            // If the simplified ratio is too large (e.g., 1920:1080), show decimal format
            if (aspectWidth > 100 || aspectHeight > 100)
            {
                return $"{actualRatio:F2}:1";
            }

            return $"{aspectWidth}:{aspectHeight}";
        }

        /// <summary>
        /// Calculates the Greatest Common Divisor using Euclidean algorithm
        /// </summary>
        private static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }


        /// <summary>
        /// Normalize property values to a single string.
        /// Handles string, string[], IEnumerable<object>, and other values.
        /// </summary>
        private static string? NormalizeToString(object? value)
        {
            if (value == null) return null;

            switch (value)
            {
                case string s:
                    return s;
                case string[] sa:
                    return sa.Length == 1 ? sa[0] : string.Join("; ", sa);
                case IEnumerable<object> eo:
                    return string.Join("; ", eo.Select(o => o?.ToString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)));
                default:
                    return value.ToString();
            }
        }

        /// <summary>
        /// Normalize property values to nullable UInt32 (for rating and similar numeric properties).
        /// Accepts numeric types, numeric strings or string[] where first element is numeric.
        /// </summary>
        private static uint? NormalizeToUInt32(object? value)
        {
            if (value == null) return null;

            switch (value)
            {
                case uint u:
                    return u;
                case int i when i >= 0:
                    return (uint)i;
                case long l when l >= 0:
                    return (uint)l;
                case ushort us:
                    return us;
                case string s when uint.TryParse(s, out var parsedFromString):
                    return parsedFromString;
                case string[] sa when sa.Length > 0 && uint.TryParse(sa[0], out var parsedFromArray):
                    return parsedFromArray;
                case IEnumerable<object> eo:
                    var first = eo.FirstOrDefault()?.ToString();
                    if (!string.IsNullOrEmpty(first) && uint.TryParse(first, out var parsedFromEnum))
                        return parsedFromEnum;
                    break;
            }

            // As a last resort, try ToString parse
            if (uint.TryParse(value.ToString(), out var fallbackParsed))
            {
                return fallbackParsed;
            }

            return null;
        }


        /// <summary>
        /// Windows doesn't support editing metadata of these files by default
        /// </summary>
        private void DetermineMetaDataSupport()
        {
            if (Extension.ToUpper() == ".MKV") MetaDataEditable = false;
            else if (Extension.ToUpper() == ".GIF") MetaDataEditable = false;
            else MetaDataEditable = true;
        }


        /// <summary>
        /// Fetches the PropertyTopic attribute value for a given property name.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        private static string GetPropertyTopic(string PropertyName)
        {
            var field = typeof(MediaProperties).GetField(PropertyName, BindingFlags.Instance | BindingFlags.NonPublic);

            if (field == null)
            {
                field = typeof(MediaProperties).GetField($"_{char.ToLower(PropertyName[0])}{PropertyName.Substring(1)}",
                                          BindingFlags.Instance | BindingFlags.NonPublic);
            }

            var tagAttribute = field?.GetCustomAttribute<PropertyTopic>();
            return tagAttribute?.TopicValue;
        }


        /// <summary>
        /// Fetches the value of a specific topic from the file properties.
        /// </summary>
        /// <param name="File">Pass in a standard Storage File</param>
        /// <param name="Topic">Use "nameof() and give the lowercase property"</param>
        /// <returns></returns>
        private async Task<object?> FetchTopicValue(StorageFile File, string Topic)
        {
            var Property = await File.Properties.RetrievePropertiesAsync(new List<string> { Topic });
            if (Property.TryGetValue(Topic, out object value) && value != null)
            {
                return (object?)value;
            }
            else { return null; }
        }

        /// <summary>
        /// Saves a value to a specific topic in the file properties.
        /// </summary>
        /// <param name="Path"></param>
        /// <param name="PropertyName"></param>
        /// <param name="Value"></param>
        private void SaveTopicValue(string Path, string PropertyName, object Value)
        {
            StorageFile File = StorageFile.GetFileFromPathAsync(Path).AsTask().Result;

            IDictionary<string, object> propertiesToSave = new Dictionary<string, object>();
            propertiesToSave[GetPropertyTopic(PropertyName)] = Value;
            File.Properties.SavePropertiesAsync(propertiesToSave).AsTask();
        }

    }

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public class PropertyTopic : Attribute
    {
        public string TopicValue { get; }

        public PropertyTopic(string topicValue)
        {
            TopicValue = topicValue;
        }
    }

}
