using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.AsyncValidation.Extensions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Mvc.AsyncValidation;

class Program
{
    static void Main(string[] args)
    {
        CreateHostBuilder(args).Build().Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(configure =>
            {
                configure.UseStartup<Startup>();
            })
            .ConfigureServices(services =>
            {
                services.AddMvc(opt => opt.EnableEndpointRouting = false);
                services.AddAsyncValidation()
                    .Configure(opt =>
                    {
                        opt.MaxValidationDepth = null;
                    });
            });
    }
}

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add services to the container.
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();

        app.UseMvc(routes =>
        {
            routes.MapRoute(
                name: "default",
                template: "{controller=Home}/{action=Index}/{id?}");
        });
    }
}
