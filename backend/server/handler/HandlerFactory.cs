using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace backend.server
{
    public interface ICrawlerHandler
    {
        Task StartCrawlerAsync(string website);
    }

    public interface ICrawlerHandlerFactory
    {
        ICrawlerHandler Create(string website);
    }

    public class CrawlerHandlerFactory : ICrawlerHandlerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public CrawlerHandlerFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ICrawlerHandler Create(string website)
        {
            foreach (var site in Constants.WebsiteMap)
            {
                if (website.Contains(site.Key))
                {
                    switch (site.Value)
                    {
                        case Website.VnExpress:
                            return _serviceProvider.GetRequiredService<VnExpressHandler>();
                        case Website.TuoiTre:
                            return _serviceProvider.GetRequiredService<TuoiTreHandler>();
                    }
                }
            }
            return null;
        }
    }
}
