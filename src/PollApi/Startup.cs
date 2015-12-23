using Microsoft.AspNet.Builder;
using Microsoft.AspNet.Hosting;
using Microsoft.AspNet.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace PollApi
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            // Set up configuration sources.
            var builder = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json");

            if (env.IsDevelopment())
            {
                // This will push telemetry data through Application Insights pipeline faster, allowing you to view results immediately.
                builder.AddApplicationInsightsSettings(developerMode: true);
            }

            builder.AddEnvironmentVariables();
            this.Configuration = builder.Build().ReloadOnChanged("appsettings.json");
        }

        public IConfigurationRoot Configuration { get; set; }

        // This method gets called by the runtime. Use this method to add services to the container
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddApplicationInsightsTelemetry(this.Configuration);
            services.AddCaching();
            services.AddCors();
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(env.IsDevelopment() ? LogLevel.Information : LogLevel.Warning);
            loggerFactory.AddDebug();

            app.UseOverrideHeaders(new OverrideHeaderMiddlewareOptions
            {
                ForwardedOptions = ForwardedHeaders.All
            });

            app.UseApplicationInsightsRequestTelemetry();
            app.UseDeveloperExceptionPage();
            app.UseApplicationInsightsExceptionTelemetry();

            app.UseDefaultFiles().UseStaticFiles();

            app.UseCors(builder => builder.AllowAnyOrigin().WithMethods("GET"));
            app.UseMvc();
        }

        // Entry point for the application.
        public static void Main(string[] args) => WebApplication.Run<Startup>(args);
    }
}
