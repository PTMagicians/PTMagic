using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Core.Main;
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

      try
      {
        systemConfiguration = new PTMagicConfiguration(ptMagicBasePath);
      }
      catch (Exception ex)
      {
        throw ex;
      }

      services.AddMvc();
      services.AddAntiforgery(o => o.HeaderName = "XSRF-TOKEN");
      services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
      services.AddDistributedMemoryCache();
      services.AddSession(options =>
      {
        options.IdleTimeout = TimeSpan.FromSeconds(900);
        options.Cookie.HttpOnly = true;
        options.Cookie.Name = "PTMagicMonitor" + systemConfiguration.GeneralSettings.Monitor.Port.ToString();
      });
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IHostingEnvironment env)
    {
      // Register global exception handler
      if (env.IsDevelopment())
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
      app.UseMvc();

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
