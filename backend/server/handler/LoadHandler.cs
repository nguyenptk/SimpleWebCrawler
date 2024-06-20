using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace backend.server
{
    public class LoadHandler
    {
        public ArticleData LoadData(string website)
        {
            var articleData = new ArticleData
            {
                Articles = new List<Article>()
            };

            string topArticlesJsonPath = GetTopArticlesJsonPath(website);
            if (File.Exists(topArticlesJsonPath))
            {
                var existingJson = File.ReadAllText(topArticlesJsonPath);
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
                    .Where(a => a.Date >= oneWeekAgo && a.Website == website)
                    .OrderByDescending(a => a.TotalLikes)
                    .Take(10)
                    .ToList();
            }

            return articleData;
        }

        public static string GetTopArticlesJsonPath(string website)
        {
            string topArticlesJsonPath = Constants.TopArticlesJsonPath;
            switch (Constants.WebsiteMap[website])
            {
                case Website.VnExpress:
                    topArticlesJsonPath = Constants.TopArticlesVnExpressJsonPath;
                    break;
                case Website.TuoiTre:
                    topArticlesJsonPath = Constants.TopArticlesTuoiTreJsonPath;
                    break;
            }
            return topArticlesJsonPath;
        }
    }
}
