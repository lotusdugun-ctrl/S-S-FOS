// Ported from src/game/daily.ts.
using System;
using System.Globalization;

namespace Sisifos.Game
{
    public static class Daily
    {
        /// <summary>today's date, local, as YYYY-MM-DD (the date's own Year/Month/Day are used)</summary>
        public static string todayKey(DateTime? date = null)
        {
            var d = date ?? DateTime.Now;
            string m = d.Month.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
            string day = d.Day.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0');
            return d.Year.ToString(CultureInfo.InvariantCulture) + "-" + m + "-" + day;
        }

        /// <summary>
        /// The saying of the day: the same for everyone on a given date, and a
        /// different one every day for a thousand days before any repeats (389 shares
        /// no factor with 1000, so stepping by it visits every page once).
        /// </summary>
        public static int dailyIndex(DateTime? date = null)
        {
            var d = date ?? DateTime.Now;
            var utc = new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc);
            double days = Math.Floor((utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds / 86400000);
            double n = Quotes.QUOTE_COUNT;
            return (int)((((days * 389 + 17) % n) + n) % n);
        }
    }
}
