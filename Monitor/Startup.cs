using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Core.Main;
using Core.Helper;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Monitor
{
  public class Startup
  {
    PTMagicConfiguration systemConfiguration = null;

    public Startup()
    {
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
      string monitorBasePath = Directory.GetCurrentDirectory();
      if (!System.IO.File.Exists(monitorBasePath + Path.DirectorySeparatorChar + "appsettings.json"))
      {
        monitorBasePath += Path.DirectorySeparatorChar + "Monitor";
      }

      IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(monitorBasePath)
                .AddJsonFile("appsettings.json", false)
                .Build();

      string ptMagicBasePath = config.GetValue<string>("PTMagicBasePath");

      if (!ptMagicBasePath.EndsWith(Path.DirectorySeparatorChar))
      {
        ptMagicBasePath += Path.DirectorySeparatorChar;
      }

      systemConfiguration = new PTMagicConfiguration(ptMagicBasePath);

      services.AddMvc(option => option.EnableEndpointRouting = false);
      services.AddAntiforgery(o => o.HeaderName = "XSRF-TOKEN");
      services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
      services.AddDistributedMemoryCache();
      services.AddSession(options =>
      {
        options.IdleTimeout = TimeSpan.FromSeconds(1800);
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "PTMagicMonitor" + systemConfiguration.GeneralSettings.Monitor.Port.ToString();
      });

      services.Configure<KestrelServerOptions>(options =>
      {
        options.AllowSynchronousIO = true;
      });

      // Remove the old tmp folder if it exists
      string oldTmpFolder = monitorBasePath + System.IO.Path.DirectorySeparatorChar + "wwwroot" + System.IO.Path.DirectorySeparatorChar + "assets" + System.IO.Path.DirectorySeparatorChar + "tmp" + System.IO.Path.DirectorySeparatorChar;
      if (System.IO.Directory.Exists(oldTmpFolder))
      {
        System.IO.Directory.Delete(oldTmpFolder, true);
      }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
      // Register global exception handler
      if (env.EnvironmentName == "Development")
      {
        app.UseBrowserLink();
        app.UseDeveloperExceptionPage();
      }
      else
      {
        app.UseExceptionHandler("/Error");
      }

      // Configure request pipeline
      app.UseStaticFiles();
      app.UseSession();
      app.UseMvcWithDefaultRoute();      

      // Open the browser
      if (systemConfiguration.GeneralSettings.Monitor.OpenBrowserOnStart) OpenBrowser("http://localhost:" + systemConfiguration.GeneralSettings.Monitor.Port.ToString());
    }

    public static void OpenBrowser(string url)
    {
      if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
      {
        Process.Start(new ProcessStartInfo("cmd", $"/c start {url}")); // Works ok on windows
      }
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
      {
        Process.Start("xdg-open", url);  // Works ok on linux
      }
      else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
      {
        Process.Start("open", url); // Not tested
      }
    }
  }
}
