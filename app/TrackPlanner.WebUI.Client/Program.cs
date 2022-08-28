using Blazor.DownloadFileFast.Interfaces;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Blazored.Modal;
using Force.DeepCloner;
using TrackPlanner.Data;
using TrackPlanner.Settings;

namespace TrackPlanner.WebUI.Client
{
    public class Program
    {
        public static EnvironmentConfiguration Configuration { get; private set; } = new EnvironmentConfiguration();
        public static UserPlannerPreferences InitUserPlannerPrefs { get; set; } = default!;

        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            
            builder.Services.AddBlazoredModal();

            var http = new HttpClient {BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)};

            using (var response = await http.GetAsync(Constants.ConfigFilename))
            {
                await using (var stream = await response.Content.ReadAsStreamAsync())
                {
                    using (var reader = new StreamReader(stream, leaveOpen: true))
                    {
                        var content = await reader.ReadToEndAsync().ConfigureAwait(false);
                        Console.WriteLine(content);
                    }

                    stream.Position = 0;
                    builder.Configuration.AddJsonStream(stream);
                }
            }

            builder.Configuration.Bind(EnvironmentConfiguration.SectionName, Configuration);
            Configuration.Check();

            InitUserPlannerPrefs = Configuration.PlannerPreferences.DeepClone();

            builder.Services.AddScoped(sp => http);
            builder.Services.AddScoped(typeof(RestClient));
            builder.Services.AddBlazorDownloadFile();

            await builder.Build().RunAsync();
        }

    }
}
