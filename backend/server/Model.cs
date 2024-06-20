using System;
using System.Collections.Generic;

namespace backend.server
{
    public class CrawlerRequest
    {
        public string Website { get; set; }
    }

    public class ArticleData
    {
        public DateTime ExecuteTime { get; set; }
        public List<Article> Articles { get; set; }
    }

    public class Article
    {
        public string Website { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public int TotalLikes { get; set; }
        public DateTime Date { get; set; }
    }
}
