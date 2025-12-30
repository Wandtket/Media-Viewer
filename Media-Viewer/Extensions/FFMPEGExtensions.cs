using FFMpegCore;
using MediaViewer.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MediaViewer.Extensions
{
    public static class FFmpegExtensions
    {


        /// <summary>
        /// Extracts chapters from a video file using FFMpegCore.
        /// </summary>
        public static async Task<List<ChapterInfo>> ExtractChaptersAsync(string videoPath)
        {
            var chapters = new List<ChapterInfo>();

            try
            {
                // Use FFProbe to analyze the video file
                var mediaInfo = await FFProbe.AnalyseAsync(videoPath);

                if (mediaInfo.Chapters != null && mediaInfo.Chapters.Any())
                {
                    foreach (var chapter in mediaInfo.Chapters)
                    {
                        var chapterInfo = new ChapterInfo
                        {
                            StartTime = chapter.Start,
                            Title = !string.IsNullOrWhiteSpace(chapter.Title)
                                ? chapter.Title
                                : $"Chapter {chapters.Count + 1}"
                        };

                        chapters.Add(chapterInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error extracting chapters: {ex.Message}");
            }

            return chapters;
        }

        public static async Task<TimeSpan> GetVideoDurationAsync(string videoPath)
        {
            try
            {
                var mediaInfo = await FFProbe.AnalyseAsync(videoPath);
                return mediaInfo.Duration;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting video duration: {ex.Message}");
                return TimeSpan.Zero;
            }
        }


    }
}
