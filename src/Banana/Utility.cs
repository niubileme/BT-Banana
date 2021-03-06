﻿using System;

namespace Banana
{
    public class Utility
    {

        /// <summary>
        /// 获取指定日期的指定周的星期几的日期。周日-周六
        /// </summary>
        /// <param name="dt">指定日期</param>
        /// <param name="weekday">星期几</param>
        /// <param name="Number">-2上上周，-1上周，0本周，1下周，2下下周</param>
        /// <returns></returns>
        public static DateTime GetWeekUpOfDate(DateTime dt, DayOfWeek weekday, int Number = 0)
        {
            int wd1 = (int)weekday;
            int wd2 = (int)dt.DayOfWeek;
            return wd2 == wd1 ? dt.AddDays(7 * Number) : dt.AddDays(7 * Number - wd2 + wd1);
        }


        public static string GetImage(string str)
        {
            return string.IsNullOrWhiteSpace(str) ? "/images/no.jpg" : ("data:image/jpg;base64," + str);
        }

        public static string GetString(string str)
        {
            return string.IsNullOrWhiteSpace(str) ? "..." : str;
        }
    }
}
