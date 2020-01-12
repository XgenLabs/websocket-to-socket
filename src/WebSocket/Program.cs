using System;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using WebSocket.Hubs;

namespace WebSocket
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = CreateLogger();

            try
            {
                Log.Information("Starting web host");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .ConfigureLogging((hostingContext, logging) =>
                    {
                        logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
                        logging.AddDebug();
                        logging.AddSerilog(dispose: true);
                    })
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseKestrel(options =>
                        {
                            // Hub bound to TCP end point
                            options.ListenLocalhost(5000);

                            // options.Listen(IPAddress.Any, 5000, builder => builder.UseHub<Proxy>());
                        }).UseStartup<Startup>();
                    });

        private static Logger CreateLogger()
        {
            return new LoggerConfiguration()
                .MinimumLevel.Debug()
                .Enrich.FromLogContext()
                .WriteTo.ColoredConsole()
                .WriteTo.File("logs/log.txt",
                    rollingInterval: RollingInterval.Day,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1))
                .CreateLogger();
        }
    }
}
