using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Reflection;
using TrackPlanner.Settings;
using TrackPlanner.WebUI.Client;

namespace TrackPlanner.WebUI.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHostBuilder hostBuilder = CreateHostBuilder(args);
            IHost host = hostBuilder.Build();
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            /*return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });*/

            // https://stackoverflow.com/a/37365382/6734314
            // https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/?view=aspnetcore-5.0
            return Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var env = hostingContext.HostingEnvironment;

                    config.Sources.Clear();

                    config
                        .SetBasePath(env.ContentRootPath)
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                        .AddEnvironmentVariables()
                        .AddCommandLine(args);

                    string bin_directory = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!;
                    System.IO.File.Copy(sourceFileName:System.IO.Path.Combine(bin_directory, EnvironmentConfiguration.Filename),
                        destFileName:Path.Combine(env.ContentRootPath, "wwwroot", Constants.ConfigFilename), 
                        overwrite:true);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }




    }
}
