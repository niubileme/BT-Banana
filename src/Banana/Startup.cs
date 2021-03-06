﻿using Banana.Filters;
using Banana.Services;
using CSRedis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Banana
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IVideoService, VideoService>();
            services.AddSingleton<IRedisService, RedisService>();
            services.AddSingleton<IMagnetSearchService, MagnetSearchService>();
            services.AddSingleton<IVideoRankingService, VideoRankingService>();

            services.AddResponseCompression();

            services.AddMemoryCache();
            services.AddSession();

            services.AddMvc(options =>
            {
                options.Filters.Add<GlobalExceptionFilter>();
            });

            services.AddOptions();

            #region Redis
            var RedisClient = new CSRedisClient(Configuration["Redis:CSRedisConnection"]);
            services.AddSingleton(_ => RedisClient);
            #endregion

            #region MongoDB
            var MongoConnectionString = Configuration["MongoDB:connectionString"];
            var mClient = new MongoClient(MongoConnectionString);
            services.AddSingleton(_ => mClient);
            #endregion

            #region ElasticSearch
            var EsUrls = Configuration["ElasticSearch:Url"].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            var EsNodes = new List<Uri>();
            EsUrls.ForEach(url =>
            {
                EsNodes.Add(new Uri(url));
            });
            var EsPool = new Elasticsearch.Net.StaticConnectionPool(EsNodes);
            var EsSettings = new Nest.ConnectionSettings(EsPool);
            var EsClient = new Nest.ElasticClient(EsSettings);
            services.AddSingleton(_ => EsClient);
            #endregion

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            app.UseStaticFiles();
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/error");
            }
            app.UseResponseCompression();
            app.UseSession();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
