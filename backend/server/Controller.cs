using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace backend.server
{
    [ApiController]
    [Route("webcrawler/v1")]
    public class Controller : ControllerBase
    {
        private readonly ICrawlerHandlerFactory _crawlerHandlerFactory;
        private readonly LoadHandler _load;
        public static ConcurrentDictionary<string, bool> ProcessingWebsites = new ConcurrentDictionary<string, bool>();

        public Controller(ICrawlerHandlerFactory crawlerHandlerFactory, LoadHandler load)
        {
            _crawlerHandlerFactory = crawlerHandlerFactory;
            _load = load;
        }

        [HttpPost("execute")]
        public async Task<IActionResult> Execute([FromBody] CrawlerRequest request)
        {
            if (string.IsNullOrEmpty(request.Website))
            {
                return BadRequest(new { message = "Website is required" });
            }

            if (!Constants.WebsiteMap.ContainsKey(request.Website)) {
                return BadRequest(new { message = "Invalid website" });
            }

            if (ProcessingWebsites.TryAdd(request.Website, true))
            {
                try
                {
                    var handler = _crawlerHandlerFactory.Create(request.Website);
                    if (handler != null)
                    {
                        await handler.StartCrawlerAsync(request.Website);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Unhandled exception: {ex.Message}");
                    ProcessingWebsites.TryRemove(request.Website, out _);
                    return StatusCode(500, new { message = "An error occurred while processing your request" });
                }
                finally
                {
                    ProcessingWebsites.TryRemove(request.Website, out _);
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
