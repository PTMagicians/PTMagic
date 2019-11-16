using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Monitor.Pages
{
  public class ErrorModel : PageModel
  {
    public string RequestId { get; set; }
    public IExceptionHandlerFeature Exception = null;

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public void OnGet()
    {
      Exception = HttpContext.Features.Get<IExceptionHandlerFeature>();
      
      var exceptionFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();

      RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;

      string errorString = exceptionFeature.Error.ToString();
      if (!errorString.Contains("is being used")) {
        Logger.WriteException(exceptionFeature.Error, "An error occurred whilst requesting " + exceptionFeature.Path);
      }
    }
  }
}
