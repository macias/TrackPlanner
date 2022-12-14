using TrackPlanner.Data.Stored;
using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrackPlanner.CommonBackend;
using TrackPlanner.Data;
using TrackPlanner.RestService.Controllers;

namespace TrackPlanner.RestService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IHost host = CreateHostBuilder(args)
                .UseDefaultServiceProvider((context, options) =>
                {
                    options.ValidateOnBuild = false; // 2/2 step for controllers as services
                })
                .Build();
            //host.Services.GetService<DummyControl>();
            var ctrl = host.Services.GetRequiredService<PlannerController>();
            //RunTest(ctrl);
            host.Run();
        }

        private static void RunTest(PlannerController ctrl)
        {
            if (false)
            {
                var schedule = new ScheduleJourney();
                schedule.Days.Add(new ScheduleDay());
                var summary = schedule.GetSummary();
                ;
            }
            if (false)
            {
                if (ctrl.TryLoadSchedule("sztum.trproj", out var schedule))
                {
                    //schedule.SplitDay(1, 5);
                    var summary = schedule.GetSummary();
                    Console.WriteLine(summary.Days[1].Checkpoints.Count);
                }
            }
            {
                if (ctrl.TryLoadSchedule("sztum_test.trproj", out var schedule))
                {
                    ctrl.SaveFullSchedule(new SaveRequest(){ Path = "xxx.trproj", Schedule = schedule});
                }
            }

        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            var custom_config_path =  ConfigHelper.InitializeConfigFile("restservice_settings.json", RestServiceConfig.SectionName, new RestServiceConfig());
            
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
                        .AddJsonFile(custom_config_path, optional: false)
                        .AddEnvironmentVariables()
                        .AddCommandLine(args);

/*                    
                    System.IO.File.Copy(sourceFileName:System.IO.Path.Combine(bin_directory, "clientsettings.json"),
                        destFileName:Path.Combine(env.ContentRootPath, "wwwroot", "copy_webuisettings.json"), 
                        overwrite:true);*/
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
        }
    }
}
