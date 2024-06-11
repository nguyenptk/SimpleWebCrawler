using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace backend.server
{
    [ApiController]
    [Route("webcrawler/v1")]
    public class Controller : ControllerBase
    {
        private readonly CrawlerHandler _crawler;
        private readonly LoadHandler _load;
        private readonly List<string> whitelist = new List<string> { "https://vnexpress.net/" };
        private static readonly ConcurrentDictionary<string, bool> _processingWebsites = new ConcurrentDictionary<string, bool>();

        public Controller(CrawlerHandler crawler, LoadHandler load)
        {
            _crawler = crawler;
            _load = load;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> Execute([FromBody] CrawlerRequest request)
        {
            if (string.IsNullOrEmpty(request.Website))
            {
                return BadRequest(new { message = "Website is required" });
            }

            if (!whitelist.Contains(request.Website)) {
                return BadRequest(new { message = "Invalid website" });
            }

            if (_processingWebsites.TryAdd(request.Website, true))
            {
                try
                {
                    await _crawler.StartCrawlerAsync(request.Website);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unhandled exception: {ex.Message}");
                    _processingWebsites.TryRemove(request.Website, out _);
                    return StatusCode(500, new { message = "An error occurred while processing your request" });
                }
                finally
                {
                    _processingWebsites.TryRemove(request.Website, out _);
                }
            }
            else
            {
                return BadRequest(new { message = $"Website {request.Website} is already being processed" });
            }

            return Ok(new { message = "Execute the crawler successfully" });
        }

        [HttpPost("load")]
        public IActionResult Load([FromBody] CrawlerRequest request)
        {
            var data = _load.LoadData(request.Website);

            return Ok(new { message = data });
        }
    }
}
