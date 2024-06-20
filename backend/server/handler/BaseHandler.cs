using Newtonsoft.Json;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace backend.server
{
    public abstract class BaseHandler
    {
        protected HttpClient HttpClient { get; } = new HttpClient();
        protected object LockObject = new object();
        protected int BatchMenu
        {
            get
            {
                int batchMenu = int.Parse(Environment.GetEnvironmentVariable("BATCH_MENU") ?? "1");
                int result = batchMenu / Controller.ProcessingWebsites.Count;
                return result < 1 ? 1 : result;
            }
        }

        protected int BatchArticle
        {
            get
            {
                int batchArticle = int.Parse(Environment.GetEnvironmentVariable("BATCH_ARTICLE") ?? "8");
                int result = batchArticle / Controller.ProcessingWebsites.Count;
                return result < 1 ? 1 : result;
            }
        }

        protected DateTime ExecutionStartTime { get; set; } = DateTime.Now;

        protected bool IsValidUrl(string url)
        {
            var pattern = @"^(http|https|ftp)\://[a-zA-Z0-9\-\.]+\.[a-zA-Z]{2,3}(:[a-zA-Z0-9]*)?/?([a-zA-Z0-9\-\._\?\,\'/\\\+&%\$#\=~])*[^\.\,\)\(\s]$";
            var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return regex.IsMatch(url);
        }

        protected bool IsArticleFromLastMonday(DateTime date)
        {
            var today = DateTime.Today;
            var daysSinceMonday = (today.DayOfWeek - DayOfWeek.Monday + 7) % 7;
            var lastMonday = today.AddDays(-daysSinceMonday);
            return date >= lastMonday;
        }

        protected void AppendToRawJsonFile(Article article)
        {
            lock (LockObject)
            {
                var articleData = new ArticleData
                {
                    ExecuteTime = ExecutionStartTime,
                    Articles = new List<Article>()
                };

                if (File.Exists(Constants.RawJsonPath))
                {
                    var existingJson = File.ReadAllText(Constants.RawJsonPath);
                    articleData = JsonConvert.DeserializeObject<ArticleData>(existingJson) ?? new ArticleData
                    {
                        ExecuteTime = ExecutionStartTime,
                        Articles = new List<Article>()
                    };
                }

                // Check if an article with the same URL already exists
                var existingArticle = articleData.Articles.FirstOrDefault(a => a.Url == article.Url);
                if (existingArticle != null)
                {
                    // If it exists, remove it
                    articleData.Articles.Remove(existingArticle);
                }

                // Add the new article (this will either add a new article or replace an existing one)
                articleData.Articles.Add(article);

                var json = JsonConvert.SerializeObject(articleData, Formatting.Indented);
                File.WriteAllText(Constants.RawJsonPath, json);
            }
        }

        protected void RecalculateAndSaveTopArticles(string website)
        {
            var articleData = new ArticleData
            {
                ExecuteTime = ExecutionStartTime,
                Articles = new List<Article>()
            };

            if (File.Exists(Constants.RawJsonPath))
            {
                var existingJson = File.ReadAllText(Constants.RawJsonPath);
                articleData = JsonConvert.DeserializeObject<ArticleData>(existingJson) ?? new ArticleData
                {
                    ExecuteTime = ExecutionStartTime,
                    Articles = new List<Article>()
                };
            }

            // Get the date one week ago
            var oneWeekAgo = DateTime.Now.AddDays(-7);

            // Filter the top 10 articles to those from the last week
            articleData.Articles = articleData.Articles
                .Where(a => a.Date >= oneWeekAgo && a.Website == website)
                .OrderByDescending(a => a.TotalLikes)
                .Take(10)
                .ToList();

            var json = JsonConvert.SerializeObject(articleData, Formatting.Indented);
            string topArticlesJsonPath = LoadHandler.GetTopArticlesJsonPath(website);
            File.WriteAllText(topArticlesJsonPath, json);
        }
    }
}
