using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MediaViewer.Extensions
{
    public static class StringExtensions
    {

        public static string Between(this string text, string start, string end, BetweenOptions options = BetweenOptions.KeepStartEnd)
        {
            var startIndex = text.IndexOf(start);
            var endIndex = text.IndexOf(end);

            //var endIndex = text.LastIndexOf(end);


            if (options == BetweenOptions.KeepStartEnd)
                return text.Substring(startIndex, (endIndex + end.Length) - startIndex);
            else if (options == BetweenOptions.KeepStart)
                return text.Substring(startIndex, (endIndex + end.Length) - startIndex).ReplaceFirstOccurrence(end, "");
            else if (options == BetweenOptions.KeepEnd)
                return text.Substring(startIndex, (endIndex + end.Length) - startIndex).ReplaceFirstOccurrence(start, "");
            else
                return text.Substring(startIndex + start.Length, endIndex - (startIndex + start.Length));        
        }


        public enum BetweenOptions
        {
            KeepStartEnd,
            KeepStart,
            KeepEnd,
            KeepNone,
        }


        public static string ReplaceFirstOccurrence(this string String, string find, string replace)
        {
            int pos = String.IndexOf(find);
            if (pos < 0)
            {
                return String;
            }
            return String.Substring(0, pos) + replace + String.Substring(pos + find.Length);
        }

        public static string ReplaceLastOccurrence(this string String, string find, string replace)
        {
            int pos = String.LastIndexOf(find);

            if (pos == -1)
                return String;

            return String.Remove(pos, find.Length).Insert(pos, replace);
        }


        public static string? FirstCharToLowerCase(this string? String)
        {
            if (!string.IsNullOrEmpty(String) && char.IsUpper(String[0]))
                return String.Length == 1 ? char.ToLower(String[0]).ToString() : char.ToLower(String[0]) + String[1..];

            return String;
        }

        public static string? FirstCharToUpperCase(this string? String)
        {
            if (!string.IsNullOrEmpty(String) && char.IsLower(String[0]))
                return String.Length == 1 ? char.ToUpper(String[0]).ToString() : char.ToUpper(String[0]) + String[1..];

            return String;
        }


        public static IEnumerable<int> AllIndicesOf(this string text, string pattern)
        {
            int M = pattern.Length;
            int N = text.Length;

            int[] lps = LongestPrefixSuffix(pattern);
            int i = 0, j = 0;

            while (i < N)
            {
                if (pattern[j] == text[i])
                {
                    j++;
                    i++;
                }
                if (j == M)
                {
                    yield return i - j;
                    j = lps[j - 1];
                }

                else if (i < N && pattern[j] != text[i])
                {
                    if (j != 0)
                    {
                        j = lps[j - 1];
                    }
                    else
                    {
                        i++;
                    }
                }
            }
        }

        private static int[] LongestPrefixSuffix(string pattern)
        {
            int[] lps = new int[pattern.Length];
            int length = 0;
            int i = 1;

            while (i < pattern.Length)
            {
                if (pattern[i] == pattern[length])
                {
                    length++;
                    lps[i] = length;
                    i++;
                }
                else
                {
                    if (length != 0)
                    {
                        length = lps[length - 1];
                    }
                    else
                    {
                        lps[i] = length;
                        i++;
                    }
                }
            }
            return lps;
        }


        public static string ConvertToSnakeCase(this string text)
        {
            return Regex.Replace(text, @"(\p{Ll})(\p{Lu})", "$1_$2").ToUpperInvariant();
        }

        public static string ConvertToCamelCase(this string text)
        {
            var words = text.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            var leadWord = words[0].ToLower();
            var tailWords = words.Skip(1)
                .Select(word => char.ToUpper(word[0]) + word.Substring(1))
                .ToArray();

            return $"{leadWord}{string.Join(string.Empty, tailWords)}";
        }




        /// <summary>
        /// Formats a string to be used wtih mailto: URI schema.
        /// https://www.w3docs.com/snippets/html/how-to-create-mailto-links.html
        /// </summary>
        /// <param name="String"></param>
        /// <returns></returns>
        public static string FormatForMailTo(this string String)
        {
            return String.Replace(" ", "%20").Replace("\n", "%0D%0A").Replace(Environment.NewLine, "%0D%0A").Replace("\"", "");
        }


        /// <summary>
        /// Determines if a string is a valid email.
        /// </summary>
        /// <param name="Email"></param>
        /// <returns></returns>
        public static bool IsValidEmail(this string Email)
        {
            if (Email == null)
            {
                return false;
            }

            if (new EmailAddressAttribute().IsValid(Email))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determines if a string is a valid URL.
        /// </summary>
        /// <param name="URL"></param>
        /// <returns></returns>
        public static bool IsValidURL(this string URL)
        {
            if (string.IsNullOrEmpty(URL))
            {
                return false;
            }
            if (!URL.StartsWith(@"http://") && !URL.StartsWith(@"https://"))
            {
                return false;
            }
            return Uri.IsWellFormedUriString(URL, UriKind.Absolute);
        }


        /// <summary>
        /// Converts bytes to a human readable string for showing File Sizes
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static string toByteString(this ulong bytes)
        {
            ulong Kilobyte = 1024;
            ulong Megabyte = 1024 * Kilobyte;
            ulong Gigabyte = 1024 * Megabyte;

            string suffix;
            double readable;

            if (bytes >= Gigabyte)
            {
                suffix = "GB";
                readable = (double)bytes / Gigabyte;
            }
            else if (bytes >= Megabyte)
            {
                suffix = "MB";
                readable = (double)bytes / Megabyte;
            }
            else if (bytes >= Kilobyte)
            {
                suffix = "KB";
                readable = (double)bytes / Kilobyte;
            }
            else
            {
                return bytes.ToString("0 B"); // Bytes
            }

            // Format to 1 decimal place, but show as integer if .0
            return readable.ToString(readable % 1 == 0 ? "0" : "0.0") + " " + suffix;
        }



        public static IComparer<string> WithNaturalSort(this StringComparer comparer)
        {
            return new NaturalStringComparer();
        }
        

        internal class NaturalStringComparer : IComparer<string>
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string psz1, string psz2);

            public int Compare(string x, string y)
            {
                return StrCmpLogicalW(x ?? "", y ?? "");
            }
        }

    }
}
