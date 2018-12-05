﻿using Banana.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Banana.Services
{
    public class VideoRankingService : IVideoRankingService
    {
        private IRedisService _redisService;
        public VideoRankingService(IRedisService redisService)
        {
            _redisService = redisService;
        }

        /// <summary>
        /// 日排行key
        /// </summary>
        /// <param name="classify"></param>
        /// <returns></returns>
        private string GetDayRankingKey(string classify, DateTime? date = null)
        {
            var type = VideoCommonService.GetVideoType(classify) ?? "";
            return $"{VideoCommonService.DayRankingKey}{(date.HasValue ? date.Value.ToString("yyyyMMdd") : DateTime.Today.ToString("yyyyMMdd"))}{type}";
        }

        /// <summary>
        /// 本周排行key
        /// </summary>
        private string GetCurrentWeekRankingKey(string classify, out DateTime start, out DateTime end)
        {
            var type = VideoCommonService.GetVideoType(classify) ?? "";
            start = Utility.GetWeekUpOfDate(DateTime.Now, DayOfWeek.Monday, 0).Date;
            end = Utility.GetWeekUpOfDate(DateTime.Now, DayOfWeek.Sunday, 1).Date;
            return $"{VideoCommonService.WeekRankingKey}{start.ToString("yyyyMMdd")}{end.ToString("yyyyMMdd")}{type}";
        }

        public bool AccessStatistics(string id, string classify)
        {
            //日排行
            _redisService.SortedSetIncrement(GetDayRankingKey(classify), id, 1, 60 * 24 * 15);//15天
            //总排行
            _redisService.SortedSetIncrement(VideoCommonService.TotalRankingKey, id, 1);
            return true;
        }

        public List<KeyValuePair<string, double>> GetDayRanking(string classify, int pageindex, int pagesize)
        {
            return _redisService.SortedSetRangeByRankWithScores(GetDayRankingKey(classify), pageindex, pagesize);
        }

        public List<KeyValuePair<string, double>> GetWeekRanking(string classify, int pageindex, int pagesize)
        {
            lock (classify)
            {
                //本周排行key
                var currentWeekRankingKey = GetCurrentWeekRankingKey(classify, out DateTime start, out DateTime end);
                if (!_redisService.IsExist(currentWeekRankingKey))
                {
                    //获取本周的日key
                    var dayKeys = new List<string>();
                    while (DateTime.Compare(start, end) <= 0)
                    {
                        dayKeys.Add(GetDayRankingKey(classify, start));
                        start = start.AddDays(1);
                    }
                    //合并日key 计算到周key
                    _redisService.SortedSetCombineAndStore(currentWeekRankingKey, dayKeys, 60 * 24 * 15);//15天
                }
                return _redisService.SortedSetRangeByRankWithScores(currentWeekRankingKey, pageindex, pagesize);
            }
        }
    }
}
