using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebSocket.Hubs;
using WebSocket.Routing;

namespace WebSocket
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddConnections();
            services.AddCors(options => options.AddPolicy("CorsPolicy",
            builder =>
            {
                builder.AllowAnyMethod().AllowAnyHeader()
                       .WithOrigins("http://localhost:4200")
                       .AllowCredentials();
            }));
            services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.HandshakeTimeout = TimeSpan.FromMinutes(1);
                // Faster pings for testing
                options.KeepAliveInterval = TimeSpan.FromSeconds(5);
            });
            services.AddLogging();

            services.AddTransient<ServiceFactory>(provider => provider.GetService);
            services.AddTransient<ComunicationManager>();
            services.AddTransient<ResilientCommunicationManager>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();
            app.UseRouting();
            app.UseCors("CorsPolicy");

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHub<Proxy>("/chat");
            });
        }
    }
}
