using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Monitor.Pages
{
  public class ErrorModel : PageModel
  {
    public string RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public void OnGet()
    {
      var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

      RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

      Logger.WriteException(exceptionFeature.Error, "An error occurred whilst requesting " + exceptionFeature.Path);
    }
  }
}
