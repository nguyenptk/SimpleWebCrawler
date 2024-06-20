using Newtonsoft.Json;
using PuppeteerSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace backend.server
{
    public class TuoiTreHandler : BaseHandler, ICrawlerHandler
    {
        private string _website;
        private HashSet<string> _processedArticles = new HashSet<string>(); // Avoid duplicated href in a div or page

        public async Task StartCrawlerAsync(string website)
        {
            this._website = website;

            var html = await HttpClient.GetStringAsync(website);

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            // Extract menu links from the main page using class 'menu-nav'
            var menuNodes = htmlDocument.DocumentNode.SelectNodes("//ul[@class='menu-nav']//a[@href]");
            if (menuNodes == null)
            {
                Console.WriteLine("No menu links found.");
                return;
            }

            var menuLinks = menuNodes.Select(node => node.Attributes["href"].Value)
                                    .Select(link => link.StartsWith("http") ? link : new Uri(new Uri(website), link).ToString())
                                    .Distinct()
                                    .ToList();

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                ExecutablePath = Environment.GetEnvironmentVariable("PUPPETEER_EXECUTABLE_PATH"),
                Args = new[] {
                    "--disable-gpu",
                    "--disable-dev-shm-usage",
                    "--disable-setuid-sandbox",
                    "--no-sandbox"
                }
            };

            using var browser = await Puppeteer.LaunchAsync(launchOptions);

            for (int i = 0; i < menuLinks.Count; i += BatchMenu)
            {
                var tasks = new List<Task>();
                var batch = menuLinks.Skip(i).Take(BatchMenu);

                foreach (var menuLink in batch)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            await ProcessMenuLink(browser, menuLink);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing menu link '{menuLink}': {ex.Message}");
                        }
                    }));
                }

                await Task.WhenAll(tasks);
            }

            Console.WriteLine("TuoiTre's crawler finished.");

            Console.WriteLine("Recalculate and save top 10 articles.");
            RecalculateAndSaveTopArticles(_website);

            return;
        }

        private async Task ProcessMenuLink(Browser browser, string menuLink)
        {
            using var page = await browser.NewPageAsync();
            var html = await FetchHtmlContentWithPuppeteer(page, menuLink, "");

            if (string.IsNullOrEmpty(html))
            {
                Console.WriteLine($"Failed to fetch HTML content for menu link: {menuLink}");
                return;
            }

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            // Process articles in the main menu link
            await ProcessArticlesInLink(browser, menuLink);

            // Extract sub-menu links and process articles
            var subMenuNodes = htmlDocument.DocumentNode.SelectNodes("//ul[contains(@class, 'sub-category')]//a[@href]");
            if (subMenuNodes != null)
            {
                var subMenuLinks = subMenuNodes.Select(node => node.Attributes["href"].Value)
                                                .Select(link => link.StartsWith("http") ? link : new Uri(new Uri(menuLink), link).ToString())
                                                .Distinct()
                                                .ToList();

                for (int i = 0; i < subMenuLinks.Count; i += BatchMenu)
                {
                    var subTasks = new List<Task>();
                    var subBatch = subMenuLinks.Skip(i).Take(BatchMenu);

                    foreach (var subMenuLink in subBatch)
                    {
                        subTasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessArticlesInLink(browser, subMenuLink);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error processing sub-menu link '{subMenuLink}': {ex.Message}");
                            }
                        }));
                    }

                    await Task.WhenAll(subTasks);
                }
            }
        }

        private async Task ProcessArticlesInLink(Browser browser, string link)
        {
            var semaphore = new SemaphoreSlim(BatchArticle);

            using var page = await browser.NewPageAsync();
            var html = await FetchHtmlContentWithPuppeteer(page, link, "");

            if (string.IsNullOrEmpty(html))
            {
                Console.WriteLine($"Failed to fetch HTML content for article link: {link}");
                return;
            }

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);

            // Select articles within the box-category-item, box-sub-item, and box-category-content classes
            var articleNodes = htmlDocument.DocumentNode.SelectNodes("//div[contains(@class, 'box-category-item') or contains(@class, 'box-sub-item') or contains(@class, 'box-category-content')]");

            if (articleNodes == null)
            {
                Console.WriteLine($"No articles found for link: {link}");
                return;
            }

            var articleNodeList = articleNodes.ToList();
            for (int i = 0; i < articleNodeList.Count; i += BatchArticle)
            {
                var tasks = new List<Task>();
                var batch = articleNodeList.Skip(i).Take(BatchArticle);

                foreach (var articleNode in batch)
                {
                    var articleUrl = articleNode.SelectSingleNode(".//a[contains(@class, 'box-category-link-title')]")?.Attributes["href"].Value;
                    var articleTitle = articleNode.SelectSingleNode(".//a[contains(@class, 'box-category-link-title')]")?.InnerText.Trim();
                    var commentNode = articleNode.SelectSingleNode(".//div[contains(@class, 'ico-data-type type-data-comment box-category-comment')]");

                    // Check if articleUrl and articleTitle are not null and the article has not been processed yet
                    if (!string.IsNullOrEmpty(articleUrl) && !string.IsNullOrEmpty(articleTitle) && commentNode != null && !_processedArticles.Contains(articleUrl))
                    {
                        _processedArticles.Add(articleUrl);
                        Console.WriteLine($"Link: {link}, Queueing article for processing: {articleTitle}");

                        await semaphore.WaitAsync();
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                using var articlePage = await browser.NewPageAsync();
                                var article = await ProcessArticle(articlePage, _website+articleUrl, articleTitle);
                                if (article != null && IsArticleFromLastMonday(article.Date))
                                {
                                    AppendToRawJsonFile(article);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error processing article '{articleTitle}': {ex.Message}");
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }));
                    }
                    else
                    {
                        Console.WriteLine($"Link: {link}, Skipping article without comments or already processed: {articleTitle ?? "Unknown title"}");
                    }
                }

                await Task.WhenAll(tasks);
            }
        }

        private async Task<string> FetchHtmlContentWithPuppeteer(Page page, string url, string title)
        {
            if (!IsValidUrl(url))
            {
                Console.WriteLine($"Invalid URL '{url}', skipping fetch.");
                return null;
            }

            const int retryCount = 3;
            const int delay = 2000; // 2 seconds delay between retries
            const int timeout = 15000; // Set the timeout to 15 seconds

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                    await page.GoToAsync(url, new NavigationOptions
                    {
                        WaitUntil = new[] { WaitUntilNavigation.Networkidle0 },
                        Timeout = timeout
                    });

                    return await page.GetContentAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching content from url: {url}: {ex.Message}");
                    if (i == retryCount - 1)
                    {
                        return null;
                    }
                    await Task.Delay(delay); // Wait before retrying
                }
            }
            return null;
        }

        private async Task<Article> ProcessArticle(Page page, string articleUrl, string articleTitle)
        {
            int retryCount = 3;
            int delay = 2000; // 2 seconds delay between retries

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    // Use PuppeteerSharp to fetch the full HTML content of the article
                    string articleHtml = await FetchHtmlContentWithPuppeteer(page, articleUrl, articleTitle);
                    if (articleHtml == null)
                    {
                        Console.WriteLine($"Skipping article due to repeated timeouts: {articleTitle}");
                        return null;
                    }

                    var articleDocument = new HtmlDocument();
                    articleDocument.LoadHtml(articleHtml);

                    // Update the dateNode to match the new format
                    var dateNode = articleDocument.DocumentNode.SelectSingleNode("//div[@data-role='publishdate']");
                    if (dateNode == null)
                    {
                        Console.WriteLine("Article date not found.");
                        return null;
                    }

                    var dateString = dateNode.InnerText.Trim();
                    DateTime articleDate = DateTime.MinValue;

                    // Parse the date with the new format
                    string format = "dd/MM/yyyy HH:mm 'GMT+7'";
                    if (!DateTime.TryParseExact(dateString, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out articleDate))
                    {
                        Console.WriteLine($"Failed to parse date: {dateString}");
                        return null;
                    }

                    if (!IsArticleFromLastMonday(articleDate))
                    {
                        Console.WriteLine($"Skipping article: {articleTitle} (Published on: {articleDate})");
                        return null;
                    }

                    // Update the likeNodes to match the new format
                    var likeNodes = articleDocument.DocumentNode.SelectNodes("//div[contains(@class, 'totalreact')]//span[contains(@class, 'total')]");
                    if (likeNodes == null)
                    {
                        Console.WriteLine($"No likes found in the article: {articleTitle}");
                        return null;
                    }

                    int totalLikes = 0;
                    foreach (var likeNode in likeNodes)
                    {
                        if (int.TryParse(likeNode.InnerText.Trim(), out int likes))
                        {
                            totalLikes += likes;
                        }
                    }

                    return new Article
                    {
                        Website = _website,
                        Title = articleTitle,
                        Url = articleUrl,
                        TotalLikes = totalLikes,
                        Date = articleDate
                    };
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing article '{articleTitle}': {ex.Message}");
                    if (i == retryCount - 1)
                    {
                        return null;
                    }
                    await Task.Delay(delay); // Wait before retrying
                }
            }
            return null;
        }
    }
}
