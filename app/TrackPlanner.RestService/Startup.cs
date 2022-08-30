using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using TrackPlanner.Data.Serialization;
using TrackPlanner.RestService.Controllers;
using TrackPlanner.RestService.Workers;
using TrackPlanner.Settings;
using TrackPlanner.Shared;
using TrackPlanner.PathFinder;

namespace TrackPlanner.RestService
{
    public class Startup
    {
        private const string baseDirectory = "../..";

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services
                .AddControllers()
                .AddControllersAsServices() // 1/2 step for controllers as services
                //.AddJsonOptions(json => TextOptionsFactory.CustomizeJsonOptions(json.JsonSerializerOptions))
                .AddNewtonsoftJson(json => NewtonOptionsFactory.CustomizeJsonOptions(json.SerializerSettings))
                ;

            services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo {Title = "RestService", Version = "v1"}); });

            TrackPlanner.Mapping.Logger.Create(System.IO.Path.Combine(baseDirectory, "output/log.txt"), out TrackPlanner.Mapping.ILogger logger);

            services.AddSingleton<TrackPlanner.Mapping.ILogger>(sp => logger);

            var rest_config = new RestServiceConfig();
            Configuration.GetSection(RestServiceConfig.SectionName).Bind(rest_config);
            if (false)
                SetupCors(services, rest_config);
            else
            {

            }


            if (rest_config.DummyRouting)
            {
                services.AddSingleton<IWorker>(sp => new DummyWorker(sp.GetService<TrackPlanner.Mapping.ILogger>()));
            }
            else
            {
                services.AddSingleton<IWorker>(sp =>
                {
                    RouteManager.Create(sp.GetService<TrackPlanner.Mapping.ILogger>(), new Navigator(baseDirectory),
                        mapSubdirectory: rest_config.Maps, 
                        new SystemConfiguration(){ CompactPreservesRoads = false},
                        out RouteManager manager);
                    return new RealWorker(sp.GetService<TrackPlanner.Mapping.ILogger>(), manager);
                });
            }

            services.AddTransient<PlannerController>(sp => new PlannerController(sp.GetService<TrackPlanner.Mapping.ILogger>(), sp.GetService<IWorker>(),
                rest_config,
                baseDirectory));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "RestService v1"));
            }

            if (false)
            {
                app.UseCors(RestServiceConfig.CorsPolicyName);
            }
            else
            {
                // https://stackoverflow.com/questions/48285408/how-to-disable-cors-completely-in-webapi
                app.UseCors(x => x
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    //.WithOrigins("https://localhost:44351")); // Allow only this origin can also have multiple origins seperated with comma
                    .SetIsOriginAllowed(origin => true)); // Allow any origin
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }


        public static void SetupCors(IServiceCollection services, RestServiceConfig restServiceConfig)
        {
            if (restServiceConfig.CorsOrigins == null)
                return;

            // todo: CORS https://pastebin.com/uxQT8g4A

            CorsServiceCollectionExtensions.AddCors(services, o => o.AddPolicy(RestServiceConfig.CorsPolicyName, builder =>
            {
                builder
                    .WithOrigins(restServiceConfig.CorsOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }));
        }

    }
}
