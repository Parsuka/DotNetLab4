using System.Globalization;

namespace Lab4Library.Common
{
    public static class SizeFormatter
    {
        private static readonly string[] Units = { "Б", "КБ", "МБ", "ГБ", "ТБ" };
        private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("uk-UA");

        public static string Format(long bytes)
        {
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < Units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            string value = unit == 0 ? size.ToString("0", Culture) : size.ToString("0.##", Culture);
            return value + " " + Units[unit];
        }

        public static string FormatWithBytes(long bytes)
        {
            return Format(bytes) + " (" + bytes.ToString("N0", Culture) + " байт)";
        }
    }
}
