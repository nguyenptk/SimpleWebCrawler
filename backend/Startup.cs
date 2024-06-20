using backend.server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace WebCrawler
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddScoped<ICrawlerHandlerFactory, CrawlerHandlerFactory>();
            services.AddScoped<VnExpressHandler>();
            services.AddScoped<TuoiTreHandler>();
            services.AddScoped<LoadHandler>();
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    builder =>
                    {
                        builder
                        .AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                    });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseHttpsRedirection();

            app.UseCors("AllowAllOrigins");

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
