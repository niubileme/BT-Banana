﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Banana.Models;
using Microsoft.Extensions.Caching.Memory;
using Banana.Services;
using Microsoft.Extensions.Options;
using Banana.Core;
using Banana.Helper;
using System.Text.RegularExpressions;
using Banana.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Banana.Controllers
{
    public class HomeController : Controller
    {
        private IConfiguration _configInfos;
        private IMemoryCache _memoryCache;
        //private readonly IRedisService _redisService;
        private readonly IMagnetSearchService _magnetSearchService;
        private IVideoService _videoService;

        public HomeController(IConfiguration config, IMemoryCache memoryCache, IMagnetSearchService magnetSearchService, IVideoService videoService)
        {
            _configInfos = config;
            _memoryCache = memoryCache;
            //_redisService = redisService;
            _magnetSearchService = magnetSearchService;
            _videoService = videoService;
        }


        /// <summary>
        /// 首页
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var result = new List<string>();
            var cacheKey = "BaiDuHotKey";
            if (!_memoryCache.TryGetValue(cacheKey, out result))
            {
                result = await GetBaiDuHot(20);
                if (result != null && result.Count > 0)
                    _memoryCache.Set(cacheKey, result, new DateTimeOffset(DateTime.Now.AddMinutes(20)));
            }
            ViewData["BaiDuHot"] = result;
            var videoUpdateList = new List<Video>();
            var videoUpdateListKey = "VideoUpdateListKey";
            if (!_memoryCache.TryGetValue(videoUpdateListKey, out videoUpdateList))
            {
                videoUpdateList = _videoService.GetUpdateVideoList(1, 10, out long totalCount);
                if (videoUpdateList != null && videoUpdateList.Count > 0)
                    _memoryCache.Set(videoUpdateListKey, videoUpdateList, new DateTimeOffset(DateTime.Now.AddMinutes(20)));
            }
            ViewData["VideoUpdateList"] = videoUpdateList;
            return View();
        }

        ///// <summary>
        ///// 搜索
        ///// </summary>
        //[Route("/s")]
        //public IActionResult Search()
        //{
        //    return View();
        //}

        //[HttpPost]
        //public async Task<IActionResult> SearchPost([FromForm]string key)
        //{
        //    if (string.IsNullOrEmpty(key))
        //        return Json(new { error = -1, msg = "搜索条件为空" });
        //    var result = await SearchService.QQSearch(key);
        //    return Json(new { error = 0, data = result.SearchResult });
        //}

        /// <summary>
        /// 电影库
        /// </summary>
        [Route("/movies")]
        public IActionResult Movies()
        {
            return View();
        }

        private async Task<List<string>> GetBaiDuHot(int num)
        {
            List<string> list = new List<string>();
            try
            {
                string url = "http://top.baidu.com/buzz?b=26&c=1&fr=topcategory_c1";
                var html = await HttpHelper.Get(url, "gb2312");
                MatchCollection r = Regex.Matches(html, "<tr[\\s\\S]*?<td[\\s\\S]*?keyword\">[\\s\\S]*?>(.+?)<");
                int i = 0;
                foreach (Match item in r)
                {
                    if (i == num)
                    {
                        break;
                    }
                    list.Add(item.Groups[1].Value.Trim());
                    i++;
                }
            }
            catch (Exception ex)
            {
                //Log.Error("百度电影风云榜", ex);
            }
            return list;
        }



        [Route("/about")]
        public IActionResult About()
        {
            return View();
        }

        [Route("/error")]
        public IActionResult Error()
        {
            return View();
        }

        /// <summary>
        /// 磁力链接 搜索
        /// </summary>
        [Route("/s/magnet/{key}/{index?}")]
        public IActionResult MagnetSearch(string key, string index)
        {
            key = key.Trim();
            if (string.IsNullOrEmpty(key))
                return RedirectToAction("index");
            int.TryParse(index, out int currentIndex);
            currentIndex = currentIndex < 1 ? 1 : currentIndex > 100 ? 100 : currentIndex; //最多100页
            var pageSize = 10;
            var result = new MagnetLinkSearchResultViewModel();
            //只缓存前20页数据  1分钟
            if (currentIndex > 20)
            {
                result = _magnetSearchService.MagnetLinkSearch(key, currentIndex, pageSize);
            }
            else
            {
                var cacheKey = $"s_m_{key}_{currentIndex}";
                if (!_memoryCache.TryGetValue(cacheKey, out result))
                {
                    result = _magnetSearchService.MagnetLinkSearch(key, currentIndex, pageSize);
                    _memoryCache.Set(cacheKey, result, new DateTimeOffset(DateTime.Now.AddMinutes(5)));
                }
            }
            return View(result);
        }

        /// <summary>
        /// 磁力链接 详情页
        /// </summary>
        [Route("/d/magnet/{hash}")]
        public IActionResult MagnetDetail(string hash)
        {
            if (string.IsNullOrEmpty(hash) || hash.Length != 40)
                return new RedirectResult("/error");
            var model = new MagnetLink();
            var cacheKey = $"d_m_{hash}";
            if (!_memoryCache.TryGetValue(cacheKey, out model))
            {
                model = _magnetSearchService.MagnetLinkInfo(hash);
                if (model == null)
                    return new RedirectResult("/error");
                _memoryCache.Set(cacheKey, model, new TimeSpan(0, 30, 0));
            }
            return View(model);
        }


        #region 视频解析

        /// <summary>
        /// 验证url 目前只支持腾讯视频
        /// </summary>
        private bool VerifyUrl(string url)
        {
            var result = true;
            if (string.IsNullOrEmpty(url))
                return false;
            if (!Regex.IsMatch(url, "^[https|http].+?v.qq.com"))
                return false;
            return result;
        }

        /// <summary>
        /// 视频解析
        /// </summary>
        [Route("/analyse/{url?}")]
        public IActionResult Analyse(string url)
        {
            var gkey = HttpContext.Session.GetString("gkey");
            if (string.IsNullOrEmpty(gkey))
            {
                gkey = Guid.NewGuid().ToString();
                HttpContext.Session.SetString("gkey", gkey);
            }
            ViewData["gkey"] = gkey;
            ViewData["url"] = VerifyUrl(url) ? url : "";
            return View();
        }

        [HttpPost]
        [Route("/analyse/getkey")]
        public IActionResult AnalyseKey([FromForm]string url, [FromForm]string gkey)
        {
            url = FormatHelper.UrlDecode(url);
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(gkey))
            {
                return Json(new { errorCode = -1, msg = "参数不完整，缺少相关参数" });
            }
            if (!VerifyUrl(url))
                return Json(new { errorCode = -1, msg = "视频地址不正确或不支持" });
            var now = DateTime.Now;
            var time = now.ToString("yyyy-MM-dd HH:mm:ss");
            var timestamp = FormatHelper.ConvertToTimeStamp(now).ToString();
            var sign = CreateSign(url, gkey, timestamp);
            var requestKey = EncodeRequestKey(FormatHelper.UrlEncode(url), gkey, timestamp, sign);
            return Json(new { errorCode = 0, requestkey = requestKey });
        }

        [Route("/analyse/frame")]
        public IActionResult AnalyseFrame([FromQuery]string rk)
        {
            if (string.IsNullOrEmpty(rk))
            {
                return NotFound();
            }
            ViewData["rk"] = rk;
            return View();
        }

        [Route("/analyse/core")]
        public IActionResult AnalyseCore([FromQuery]string rk)
        {
            var ckplayerJson = new CKPlayerJsonViewModel() { autoplay = true };
            var url = string.Empty;
            var gkey = string.Empty;
            var timestamp = string.Empty;
            var sign = string.Empty;
            if (!DecodeRequestKey(rk, out url, out gkey, out timestamp, out sign))
            {
                return Json(new { errorCode = -9998, msg = "参数错误，请求非法！" });
            }
            //url解码 验证签名需要
            url = FormatHelper.UrlDecode(url);
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(gkey) | string.IsNullOrEmpty(timestamp) | string.IsNullOrEmpty(sign))
            {
                return Json(new { errorCode = -9999, msg = "参数错误，请求非法！" });
            }
            long.TryParse(timestamp, out long timestampl);
            var time = FormatHelper.ConvertToDateTime(timestampl);
            //不得超过2分钟
            if (DateTime.Compare(time.AddMinutes(2), DateTime.Now) < 0)
            {
                return Json(new { errorCode = -10000, msg = "请求已过期" });
            }
            //gkey需要与会话一致
            var server_gkey = HttpContext.Session.GetString("gkey");
            if (gkey != server_gkey)
            {
                return Json(new { errorCode = -10001, msg = "会话超时，请求非法！" });
            }
            //校验签名
            if (!ValidateSign(url, gkey, timestamp, sign))
            {
                return Json(new { errorCode = -10002, msg = "签名错误，请求非法！" });
            }

            var cacheKey = $"analyse_{url}";
            var response = new VideoAnalyseResponse();
            if (!_memoryCache.TryGetValue(cacheKey, out response))
            {
                response = AnalyseService.Analyse(_configInfos["ConfigInfos:AnalyseServiceAddress"], url);
                if (response != null && response.ErrCode == 0 && response.Data.Count > 0)
                    _memoryCache.Set(cacheKey, response, new DateTimeOffset(DateTime.Now.AddHours(1)));
            }
            //处理数据
            var result = new List<string>();
            var name = string.Empty;
            if (response == null || response.ErrCode != 0)
            {
                //result.Add("http://movie.ks.js.cn/flv/other/1_0.mp4");
                //解析失败   
                //应该返回错误 todo  这里返回null
            }
            else
            {
                //可能会有多个视频  这里只取第一个
                var video = response.Data.FirstOrDefault();
                if (video != null)
                {
                    var ckvideo = new CKVideo()
                    {
                        type = "mp4",
                        weight = 0,
                        definition = video.Definition
                    };

                    foreach (var item in video.Part)
                    {
                        if (!string.IsNullOrEmpty(item.Url))
                            ckvideo.video.Add(new CKVideoInfo()
                            {
                                file = item.Url,
                                duration = item.Duration
                            });
                    }
                    ckplayerJson.video.Add(ckvideo);
                }
            }

            return Json(ckplayerJson);
        }


        private string CreateSign(string url, string gkey, string timestamp)
        {
            var MD5Key = "btbanana.com";
            return FormatHelper.Md5Hash($"md5key={MD5Key}&url={url}&gkey={gkey}&timestamp={timestamp}&md5key={MD5Key}");
        }

        private bool ValidateSign(string url, string gkey, string timestamp, string sign)
        {
            var MD5Key = "btbanana.com";
            var sourceSign = FormatHelper.Md5Hash($"md5key={MD5Key}&url={url}&gkey={gkey}&timestamp={timestamp}&md5key={MD5Key}");
            return sourceSign == sign;
        }

        private string EncodeRequestKey(string url, string gkey, string timestamp, string sign)
        {
            var request = $"&u={url}&g={gkey}&t={timestamp}&s={sign}";
            return FormatHelper.EncodeBase64(request);
        }

        private bool DecodeRequestKey(string request, out string url, out string gkey, out string timestamp, out string sign)
        {
            url = string.Empty;
            gkey = string.Empty;
            timestamp = string.Empty;
            sign = string.Empty;

            var source = FormatHelper.DecodeBase64(request);
            if (string.IsNullOrEmpty(source))
                return false;
            try
            {
                url = Regex.Match(source, "&u=(.+?)&").Groups[1].Value;
                gkey = Regex.Match(source, "&g=(.+?)&").Groups[1].Value;
                timestamp = Regex.Match(source, "&t=(.+?)&").Groups[1].Value;
                sign = Regex.Match(source, "&s=(.+)").Groups[1].Value;
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }
        #endregion
    }
}
