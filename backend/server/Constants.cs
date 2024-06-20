using System.Collections.Generic;

namespace backend.server
{
    public enum Website
    {
        VnExpress,
        TuoiTre
    }
    public static class Constants
    {
        public static readonly Dictionary<string, Website> WebsiteMap = new Dictionary<string, Website>
        {
            { "https://vnexpress.net", Website.VnExpress },
            { "https://tuoitre.vn", Website.TuoiTre }
        };

        public static readonly string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

        public static readonly string RawJsonPath = "out/raw.json";
        public static readonly string TopArticlesJsonPath = "out/top_articles.json";
        public static readonly string TopArticlesVnExpressJsonPath = "out/top_articles_vnexpress.json";
        public static readonly string TopArticlesTuoiTreJsonPath = "out/top_articles_tuoitre.json";
    }
}
