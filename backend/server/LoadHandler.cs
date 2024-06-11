using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace backend.server
{
    public class LoadHandler
    {
        public LoadHandler()
        {
        }

        public ArticleData LoadData(string homepageUrl)
        {
            var articleData = new ArticleData
            {
                Articles = new List<Article>()
            };

            if (File.Exists(Constants.TopArticlesJsonPath))
            {
                var existingJson = File.ReadAllText(Constants.TopArticlesJsonPath);
                articleData = JsonConvert.DeserializeObject<ArticleData>(existingJson) ?? new ArticleData
                {
                    Articles = new List<Article>()
                };
            }
            else if (File.Exists(Constants.RawJsonPath))
            {
                var existingJson = File.ReadAllText(Constants.RawJsonPath);
                articleData = JsonConvert.DeserializeObject<ArticleData>(existingJson) ?? new ArticleData
                {
                    Articles = new List<Article>()
                };

                // Get the date one week ago
                var oneWeekAgo = DateTime.Now.AddDays(-7);

                // Filter the top 10 articles to those from the last week
                articleData.Articles = articleData.Articles
                    .Where(a => a.Date >= oneWeekAgo)
                    .OrderByDescending(a => a.TotalLikes)
                    .Take(10)
                    .ToList();
            }

            // Convert the DateTime to UTC
            foreach (var article in articleData.Articles)
            {
                article.Date = article.Date.AddHours(-7);
            }

            return articleData;
        }
    }
}