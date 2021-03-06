﻿using Banana.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Banana.Core
{
    public class AnalyseService
    {
        public static VideoAnalyseResponse Analyse(string serviceAddress, string url)
        {
            var address = serviceAddress + "/api/core/analyse";
            var postData = $"url={url}";
            try
            {
                var data = Post(address, postData);
                return JsonConvert.DeserializeObject<VideoAnalyseResponse>(data);
            }
            catch (Exception ex)
            {
                return null;
            }
        }


        private static string Post(string url, string postData, string cookie = null, string encodingStr = "UTF-8")
        {
            var html = "";
            var encoding = Encoding.UTF8;
            try
            {
                encoding = Encoding.GetEncoding(encodingStr);
            }
            catch { }
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                if (!string.IsNullOrEmpty(cookie))
                    request.Headers[HttpRequestHeader.Cookie] = cookie;
                request.Timeout = 1000 * 10;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.101 Safari/537.36";
                byte[] byteArray = encoding.GetBytes(postData);
                request.ContentType = "application/x-www-form-urlencoded";
                request.ContentLength = byteArray.Length;
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(byteArray, 0, byteArray.Length);
                }
                var response = (HttpWebResponse)request.GetResponse();
                using (var sr = new StreamReader(response.GetResponseStream(), encoding))
                {
                    html = sr.ReadToEnd();
                }
                html = GetResponseBody(response, encoding);
            }
            catch (Exception ex)
            {
                //log
            }
            return html;
        }


        private static string GetResponseBody(HttpWebResponse response, Encoding encoding)
        {
            string responseBody = string.Empty;
            if (response.ContentEncoding.ToLower().Contains("gzip"))
            {
                using (GZipStream stream = new GZipStream(response.GetResponseStream(), CompressionMode.Decompress))
                {
                    using (StreamReader reader = new StreamReader(stream, encoding))
                    {
                        responseBody = reader.ReadToEnd();
                    }
                }
            }
            else if (response.ContentEncoding.ToLower().Contains("deflate"))
            {
                using (DeflateStream stream = new DeflateStream(
                    response.GetResponseStream(), CompressionMode.Decompress))
                {
                    using (StreamReader reader =
                        new StreamReader(stream, encoding))
                    {
                        responseBody = reader.ReadToEnd();
                    }
                }
            }
            else
            {
                using (Stream stream = response.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream, encoding))
                    {
                        responseBody = reader.ReadToEnd();
                    }
                }
            }
            return responseBody;
        }
    }
}
