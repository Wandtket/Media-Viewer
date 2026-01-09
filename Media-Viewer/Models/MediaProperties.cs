using CommunityToolkit.Mvvm.ComponentModel;
using MediaViewer.Controls.Dialogs;
using MediaViewer.Enums;
using MediaViewer.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Windows.Storage;
using File = System.IO.File;

namespace MediaViewer.Models
{

    public partial class MediaProperties : ObservableObject
    {
        private bool _suppressAutoSave = false;

        public MediaProperties()
        {

        }

        public MediaProperties(StorageFile file)
        {
            // Suppress auto-save during initial load
            _suppressAutoSave = true;
            GetProperties(file);
            _suppressAutoSave = false;
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
        [PropertyTopic("Title")]
        private string? title;
        partial void OnTitleChanged(string? value) 
        {
            if (!string.Equals(title, value, StringComparison.Ordinal))
            {
                SaveTopicValue(Path, nameof(title), value);
            }
        }


        [ObservableProperty]
        [PropertyTopic("Description")]
        private string? subject;
        partial void OnSubjectChanged(string? value) 
        {
            if (!string.Equals(subject, value, StringComparison.Ordinal))
            {
                SaveTopicValue(Path, nameof(subject), value);
            }
        }


        [ObservableProperty]
        [PropertyTopic("FirstAlbumArtist")]
        private string? author;
        partial void OnAuthorChanged(string? value) 
        {
            if (!string.Equals(author, value, StringComparison.Ordinal))
            {
                SaveTopicValue(Path, nameof(author), value);
            }
        }


        [ObservableProperty]
        [PropertyTopic("Comment")]
        private string? comments;
        partial void OnCommentsChanged(string? value) 
        {
            if (!string.Equals(comments, value, StringComparison.Ordinal))
            {
                SaveTopicValue(Path, nameof(comments), value);
            }
        }


        [ObservableProperty]
        [PropertyTopic("Rating")]
        private UInt32? rating;
        partial void OnRatingChanged(UInt32? value) 
        {
            if (!UInt32.Equals(rating, value))
            {
                SaveTopicValue(Path, nameof(rating), value);
            }
        }


        [ObservableProperty]
        [PropertyTopic("Album")]
        private string? album;
        partial void OnAlbumChanged(string? value) 
        {
            if (!string.Equals(album, value, StringComparison.Ordinal))
            {
                SaveTopicValue(Path, nameof(album), value);
            }
        }


        [ObservableProperty]
        [PropertyTopic("FirstGenre")]
        private string? genre;
        partial void OnGenreChanged(string? value) 
        {
            if (!string.Equals(genre, value, StringComparison.Ordinal))
            {
                SaveTopicValue(Path, nameof(genre), value);
            }
        }


        [ObservableProperty]
        [PropertyTopic("Year")]
        private UInt32? year;
        partial void OnYearChanged(UInt32? value) 
        {
            if (!UInt32.Equals(year, value))
            {
                SaveTopicValue(Path, nameof(year), value);
            }
        }


        [ObservableProperty]
        [PropertyTopic("Track")]
        private UInt32? track;
        partial void OnTrackChanged(UInt32? value) 
        {
            if (!UInt32.Equals(track, value))
            {
                SaveTopicValue(Path, nameof(track), value);
            }
        }


        [ObservableProperty]
        [PropertyTopic("Lyrics")]
        private string? lyrics;
        partial void OnLyricsChanged(string? value) 
        {
            if (!string.Equals(lyrics, value, StringComparison.Ordinal))
            {
                SaveTopicValue(Path, nameof(lyrics), value);
            }
        }


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
        /// Retrieves properties associated with the file using TagLibSharp.
        /// </summary>
        /// <param name="File"></param>
        private async void GetProperties(StorageFile File)
        {
            try
            {
                FileName = File.DisplayName + File.FileType;
                DisplayName = File.DisplayName;
                Extension = File.FileType;
                Path = File.Path;

                MediaType = Files.GetMediaType(File.Path);
                DetermineMetaDataSupport();

                // Get basic file system properties
                var fileInfo = new FileInfo(File.Path);
                DateModified = fileInfo.LastWriteTime;
                TimeModified = fileInfo.LastWriteTime.TimeOfDay;
                Size = ((ulong)fileInfo.Length).toByteString();

                if (File.Path.StartsWith("C")) { Source = "This PC"; }
                else if (File.Path.StartsWith("http")) { Source = "Web"; }
                else { Source = "Network"; }

                // Check if this is an image file with poor TagLib support
                string ext = System.IO.Path.GetExtension(File.Path).ToUpperInvariant();
                bool useFallback = ext == ".PNG" || ext == ".BMP" || ext == ".WEBP";

                // Use TagLibSharp to read media properties
                using (var tagFile = TagLib.File.Create(File.Path))
                {
                    var tag = tagFile.Tag;

                    // For formats with poor TagLib support, read metadata from Windows API instead
                    if (useFallback && (MediaType == MediaType.Image || MediaType == MediaType.Gif))
                    {
                        await ReadMetadataWithWindowsAPI(File);
                    }
                    else
                    {
                        // Extract metadata using TagLib
                        Title = string.IsNullOrWhiteSpace(tag.Title) ? null : tag.Title;
                        Subject = string.IsNullOrWhiteSpace(tag.Description) ? null : tag.Description;
                        Author = tag.FirstAlbumArtist ?? tag.FirstPerformer;
                        Comments = string.IsNullOrWhiteSpace(tag.Comment) ? null : tag.Comment;
                        
                        // Extract additional metadata
                        Album = string.IsNullOrWhiteSpace(tag.Album) ? null : tag.Album;
                        Genre = tag.FirstGenre;
                        Year = tag.Year > 0 ? tag.Year : null;
                        Track = tag.Track > 0 ? tag.Track : null;
                        Lyrics = string.IsNullOrWhiteSpace(tag.Lyrics) ? null : tag.Lyrics;
                        
                        // Extract rating using format-specific logic
                        Rating = ReadRating(tagFile);
                    }

                    var properties = tagFile.Properties;

                    if (MediaType == MediaType.Image ||
                        MediaType == MediaType.Gif)
                    {
                        if (properties.PhotoWidth > 0 && properties.PhotoHeight > 0)
                        {
                            Resolution = $"{properties.PhotoWidth} x {properties.PhotoHeight}";
                            AspectRatio = CalculateAspectRatio(properties.PhotoWidth, properties.PhotoHeight);
                        }

                        // Photo quality/bit depth
                        if (properties.BitsPerSample > 0)
                        {
                            BitDepth = $"{properties.BitsPerSample} bit";
                        }

                        // DPI information (if available through codec-specific properties)
                        if (tagFile is TagLib.Image.File imageFile)
                        {
                            var imageTag = imageFile.ImageTag;
                            // Some formats might have DPI info, typically 72 or 96 for digital images
                            DPI = "96 dpi"; // Default fallback
                        }
                    }
                    else if (MediaType == MediaType.Video)
                    {
                        if (properties.VideoWidth > 0 && properties.VideoHeight > 0)
                        {
                            Resolution = $"{properties.VideoWidth} x {properties.VideoHeight}";
                            AspectRatio = CalculateAspectRatio(properties.VideoWidth, properties.VideoHeight);
                        }

                        // Video-specific properties
                        if (properties.Duration > TimeSpan.Zero)
                        {
                            // Duration is available in properties.Duration if needed elsewhere
                        }

                        // For video resolution description (e.g., "1080p")
                        DPI = properties.VideoHeight > 0 ? $"{properties.VideoHeight}p" : string.Empty;
                    }
                    else if (MediaType == MediaType.Audio)
                    {
                        // Audio-specific properties
                        if (properties.AudioBitrate > 0)
                        {
                            BitDepth = $"{properties.AudioBitrate} kbps";
                        }

                        if (properties.AudioSampleRate > 0)
                        {
                            DPI = $"{properties.AudioSampleRate / 1000.0:F1} kHz";
                        }
                    }
                }
            }
            catch (TagLib.CorruptFileException ex)
            {
                await ErrorBox.Show(ex, Title: "Corrupt Media File");
            }
            catch (TagLib.UnsupportedFormatException ex)
            {
                await ErrorBox.Show(ex, Title: "Unsupported Format");
            }
            catch (Exception ex)
            {
                await ErrorBox.Show(ex, Title: "Error Retrieving Media Properties");
            }
        }

        /// <summary>
        /// Reads metadata using Windows Storage API for formats that TagLibSharp doesn't support well.
        /// </summary>
        private async System.Threading.Tasks.Task ReadMetadataWithWindowsAPI(StorageFile file)
        {
            try
            {
                var propsToRetrieve = new[] 
                { 
                    "System.Title", 
                    "System.Subject", 
                    "System.Author", 
                    "System.Comment", 
                    "System.Rating" 
                };

                IDictionary<string, object> props = await file.Properties.RetrievePropertiesAsync(propsToRetrieve);

                if (props.TryGetValue("System.Title", out object titleValue) && titleValue != null)
                {
                    Title = titleValue.ToString();
                }

                if (props.TryGetValue("System.Subject", out object subjectValue) && subjectValue != null)
                {
                    Subject = subjectValue.ToString();
                }

                if (props.TryGetValue("System.Author", out object authorValue) && authorValue != null)
                {
                    if (authorValue is string[] authorArray && authorArray.Length > 0)
                    {
                        Author = authorArray[0];
                    }
                    else
                    {
                        Author = authorValue.ToString();
                    }
                }

                if (props.TryGetValue("System.Comment", out object commentValue) && commentValue != null)
                {
                    Comments = commentValue.ToString();
                }

                if (props.TryGetValue("System.Rating", out object ratingValue) && ratingValue != null)
                {
                    if (ratingValue is uint ratingUint)
                    {
                        Rating = ratingUint;
                    }
                    else if (uint.TryParse(ratingValue.ToString(), out uint parsed))
                    {
                        Rating = parsed;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to read metadata with Windows API: {ex.Message}");
            }
        }


        /// <summary>
        /// Reads rating from media file using format-specific implementations.
        /// Returns Windows-compatible rating value (0-99).
        /// </summary>
        private static uint? ReadRating(TagLib.File file)
        {
            try
            {
                // MP3 files with ID3v2 tags
                if (file is TagLib.Mpeg.AudioFile mpegFile)
                {
                    var id3v2Tag = mpegFile.GetTag(TagLib.TagTypes.Id3v2) as TagLib.Id3v2.Tag;
                    if (id3v2Tag != null)
                    {
                        // Try to read POPM (Popularimeter) frame
                        var popmFrame = TagLib.Id3v2.PopularimeterFrame.Get(id3v2Tag, "Windows Media Player 9 Series", false);
                        if (popmFrame != null && popmFrame.Rating > 0)
                        {
                            // ID3v2 POPM uses 0-255, convert to Windows 0-99 scale
                            return ConvertFromId3Rating(popmFrame.Rating);
                        }
                    }
                }
                // WMA files
                else if (file is TagLib.Asf.File asfFile)
                {
                    var asfTag = asfFile.GetTag(TagLib.TagTypes.Asf) as TagLib.Asf.Tag;
                    if (asfTag != null)
                    {
                        // WMA uses "WM/SharedUserRating" which is 0-99
                        var ratingDesc = asfTag.GetDescriptorStrings("WM/SharedUserRating").FirstOrDefault();
                        if (ratingDesc != null && uint.TryParse(ratingDesc, out uint wmaRating))
                        {
                            return wmaRating;
                        }
                    }
                }
                // FLAC files
                else if (file is TagLib.Flac.File flacFile)
                {
                    var xiph = flacFile.GetTag(TagLib.TagTypes.Xiph) as TagLib.Ogg.XiphComment;
                    if (xiph != null)
                    {
                        // FMPS_Rating is 0.0-1.0 scale
                        var ratingStr = xiph.GetFirstField("FMPS_RATING");
                        if (ratingStr != null && double.TryParse(ratingStr, out double flacRating))
                        {
                            // Convert 0.0-1.0 to 0-99
                            return (uint)(flacRating * 99);
                        }
                    }
                }
                // OGG Vorbis files
                else if (file is TagLib.Ogg.File oggFile)
                {
                    var xiph = oggFile.GetTag(TagLib.TagTypes.Xiph) as TagLib.Ogg.XiphComment;
                    if (xiph != null)
                    {
                        var ratingStr = xiph.GetFirstField("FMPS_RATING");
                        if (ratingStr != null && double.TryParse(ratingStr, out double oggRating))
                        {
                            return (uint)(oggRating * 99);
                        }
                    }
                }
            }
            catch
            {
                // If reading rating fails, return null
            }

            return null;
        }

        /// <summary>
        /// Writes rating to media file using format-specific implementations.
        /// Accepts Windows-compatible rating value (0-99).
        /// </summary>
        private static void WriteRating(TagLib.File file, uint? rating)
        {
            try
            {
                // MP3 files with ID3v2 tags
                if (file is TagLib.Mpeg.AudioFile mpegFile)
                {
                    var id3v2Tag = mpegFile.GetTag(TagLib.TagTypes.Id3v2, true) as TagLib.Id3v2.Tag;
                    if (id3v2Tag != null)
                    {
                        // Set POPM (Popularimeter) frame
                        var popmFrame = TagLib.Id3v2.PopularimeterFrame.Get(id3v2Tag, "Windows Media Player 9 Series", true);
                        if (rating.HasValue && rating.Value > 0)
                        {
                            popmFrame.Rating = ConvertToId3Rating(rating.Value);
                        }
                        else
                        {
                            // Remove the frame if rating is 0 or null
                            id3v2Tag.RemoveFrame(popmFrame);
                        }
                    }
                }
                // WMA files
                else if (file is TagLib.Asf.File asfFile)
                {
                    var asfTag = asfFile.GetTag(TagLib.TagTypes.Asf, true) as TagLib.Asf.Tag;
                    if (asfTag != null)
                    {
                        if (rating.HasValue && rating.Value > 0)
                        {
                            // WMA uses "WM/SharedUserRating" (0-99)
                            asfTag.SetDescriptorStrings(new[] { rating.Value.ToString() }, "WM/SharedUserRating");
                        }
                        else
                        {
                            asfTag.RemoveDescriptors("WM/SharedUserRating");
                        }
                    }
                }
                // FLAC files
                else if (file is TagLib.Flac.File flacFile)
                {
                    var xiph = flacFile.GetTag(TagLib.TagTypes.Xiph, true) as TagLib.Ogg.XiphComment;
                    if (xiph != null)
                    {
                        if (rating.HasValue && rating.Value > 0)
                        {
                            // Convert 0-99 to 0.0-1.0 scale
                            double flacRating = rating.Value / 99.0;
                            xiph.SetField("FMPS_RATING", flacRating.ToString("F2"));
                        }
                        else
                        {
                            xiph.RemoveField("FMPS_RATING");
                        }
                    }
                }
                // OGG Vorbis files
                else if (file is TagLib.Ogg.File oggFile)
                {
                    var xiph = oggFile.GetTag(TagLib.TagTypes.Xiph, true) as TagLib.Ogg.XiphComment;
                    if (xiph != null)
                    {
                        if (rating.HasValue && rating.Value > 0)
                        {
                            double oggRating = rating.Value / 99.0;
                            xiph.SetField("FMPS_RATING", oggRating.ToString("F2"));
                        }
                        else
                        {
                            xiph.RemoveField("FMPS_RATING");
                        }
                    }
                }
            }
            catch
            {
                // Silently fail if writing rating is not supported
            }
        }

        /// <summary>
        /// Converts ID3v2 POPM rating (0-255) to Windows rating (0-99).
        /// Uses standard mapping: 1-31=1, 32-95=13, 96-159=38, 160-223=63, 224-255=88
        /// </summary>
        private static uint ConvertFromId3Rating(byte id3Rating)
        {
            if (id3Rating == 0) return 0;
            if (id3Rating >= 1 && id3Rating <= 31) return 1;
            if (id3Rating >= 32 && id3Rating <= 95) return 13;
            if (id3Rating >= 96 && id3Rating <= 159) return 38;
            if (id3Rating >= 160 && id3Rating <= 223) return 63;
            if (id3Rating >= 224) return 88;
            return 0;
        }

        /// <summary>
        /// Converts Windows rating (0-99) to ID3v2 POPM rating (0-255).
        /// Uses standard mapping aligned with Windows Media Player.
        /// </summary>
        private static byte ConvertToId3Rating(uint windowsRating)
        {
            if (windowsRating == 0) return 0;
            if (windowsRating >= 1 && windowsRating <= 12) return 1;
            if (windowsRating >= 13 && windowsRating <= 37) return 64;
            if (windowsRating >= 38 && windowsRating <= 62) return 128;
            if (windowsRating >= 63 && windowsRating <= 87) return 196;
            if (windowsRating >= 88) return 255;
            return 0;
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
        /// Saves a value to a specific topic in the file properties using TagLibSharp.
        /// For image formats with poor TagLib support, falls back to Windows Storage API.
        /// </summary>
        /// <param name="Path"></param>
        /// <param name="PropertyName"></param>
        /// <param name="Value"></param>
        private async void SaveTopicValue(string Path, string PropertyName, object Value)
        {
            try
            {
                if (!MetaDataEditable || string.IsNullOrEmpty(Path))
                    return;

                // Check if this is an image file with poor TagLib support
                string ext = System.IO.Path.GetExtension(Path).ToUpperInvariant();
                bool useFallback = ext == ".PNG" || ext == ".BMP" || ext == ".WEBP";

                // For PNG, BMP, WEBP - use Windows Storage API as they have poor TagLib support
                if (useFallback && (MediaType == MediaType.Image || MediaType == MediaType.Gif))
                {
                    await SaveWithWindowsAPI(Path, PropertyName, Value);
                    return;
                }

                using (var tagFile = TagLib.File.Create(Path))
                {
                    // For image files, we need to ensure we can write tags
                    // TagLib might return a read-only or null tag for some image formats
                    // We need to explicitly get or create the appropriate tag type
                    TagLib.Tag tag = null;
                    
                    if (tagFile is TagLib.Image.File imageFile)
                    {
                        // For images, try to get the ImageTag which supports EXIF/XMP/IPTC
                        tag = imageFile.ImageTag ?? imageFile.GetTag(TagLib.TagTypes.TiffIFD, true)
                              ?? imageFile.GetTag(TagLib.TagTypes.XMP, true)
                              ?? imageFile.GetTag(TagLib.TagTypes.IPTCIIM, true);
                        
                        // If still null, fall back to the generic Tag property
                        if (tag == null)
                            tag = imageFile.Tag;
                    }
                    else
                    {
                        // For non-image files, use the standard tag
                        tag = tagFile.Tag;
                    }

                    if (tag == null)
                        return; // Can't write to this file type

                    string topic = GetPropertyTopic(PropertyName);

                    switch (topic)
                    {
                        case "Title":
                            tag.Title = Value?.ToString();
                            break;
                        case "Description":
                            tag.Description = Value?.ToString();
                            break;
                        case "FirstAlbumArtist":
                            if (Value != null)
                            {
                                tag.AlbumArtists = new[] { Value.ToString() };
                            }
                            else
                            {
                                tag.AlbumArtists = null;
                            }
                            break;
                        case "Comment":
                            tag.Comment = Value?.ToString();
                            break;
                        case "Rating":
                            uint? ratingValue = Value as uint?;
                            WriteRating(tagFile, ratingValue);
                            break;
                        case "Album":
                            tag.Album = Value?.ToString();
                            break;
                        case "FirstGenre":
                            if (Value != null)
                            {
                                tag.Genres = new[] { Value.ToString() };
                            }
                            else
                            {
                                tag.Genres = null;
                            }
                            break;
                        case "Year":
                            if (Value is uint yearValue)
                            {
                                tag.Year = yearValue;
                            }
                            else if (Value == null)
                            {
                                tag.Year = 0;
                            }
                            break;
                        case "Track":
                            if (Value is uint trackValue)
                            {
                                tag.Track = trackValue;
                            }
                            else if (Value == null)
                            {
                                tag.Track = 0;
                            }
                            break;
                        case "Lyrics":
                            tag.Lyrics = Value?.ToString();
                            break;
                    }

                    tagFile.Save();
                }
            }
            catch (Exception ex)
            {
                // Silently fail or log the error
                System.Diagnostics.Debug.WriteLine($"Failed to save metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Fallback method to save metadata using Windows Storage API for formats
        /// that TagLibSharp doesn't support well (PNG, BMP, WEBP).
        /// </summary>
        private async System.Threading.Tasks.Task SaveWithWindowsAPI(string path, string propertyName, object value)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                var properties = await file.Properties.RetrievePropertiesAsync(new string[] { });
                
                string topic = GetPropertyTopic(propertyName);
                
                IDictionary<string, object> propertiesToSave = new System.Collections.Generic.Dictionary<string, object>();
                
                switch (topic)
                {
                    case "Title":
                        propertiesToSave["System.Title"] = value?.ToString() ?? string.Empty;
                        break;
                    case "Description":
                        propertiesToSave["System.Subject"] = value?.ToString() ?? string.Empty;
                        break;
                    case "FirstAlbumArtist":
                        if (value != null)
                        {
                            propertiesToSave["System.Author"] = new[] { value.ToString() };
                        }
                        break;
                    case "Comment":
                        propertiesToSave["System.Comment"] = value?.ToString() ?? string.Empty;
                        break;
                    case "Rating":
                        if (value is uint ratingValue)
                        {
                            propertiesToSave["System.Rating"] = ratingValue;
                        }
                        else if (value == null)
                        {
                            propertiesToSave["System.Rating"] = (uint)0;
                        }
                        break;
                    case "Album":
                        propertiesToSave["System.Music.AlbumTitle"] = value?.ToString() ?? string.Empty;
                        break;
                    case "FirstGenre":
                        if (value != null)
                        {
                            propertiesToSave["System.Music.Genre"] = new[] { value.ToString() };
                        }
                        break;
                    case "Year":
                        if (value is uint yearValue)
                        {
                            propertiesToSave["System.Media.Year"] = yearValue;
                        }
                        break;
                }

                if (propertiesToSave.Count > 0)
                {
                    await file.Properties.SavePropertiesAsync(propertiesToSave);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save metadata with Windows API: {ex.Message}");
            }
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
