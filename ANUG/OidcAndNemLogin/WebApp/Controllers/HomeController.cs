using ITfoxtec.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using WebApp.Models;

namespace WebApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> logger;
        private readonly AppSettings appSettings;
        private readonly IdentitySettings identitySettings;
        private readonly IHttpClientFactory httpClientFactory;

        public HomeController(ILogger<HomeController> logger, AppSettings appSettings, IdentitySettings identitySettings, IHttpClientFactory httpClientFactory)
        {
            this.logger = logger;
            this.appSettings = appSettings;
            this.identitySettings = identitySettings;
            this.httpClientFactory = httpClientFactory;
        }

        public IActionResult Index()
        {
            return View();
        }


        [Authorize]
        public IActionResult Secure()
        {
            return View();
        }

        [Authorize]
        public async Task<IActionResult> CallApi1()
        {
            var client = httpClientFactory.CreateClient();
            var accessToken = await HttpContext.GetTokenAsync(OpenIdConnectParameterNames.AccessToken);
            client.SetAuthorizationHeaderBearer(accessToken);

            using var response = await client.GetAsync(appSettings.Api1Url);
            if (response.IsSuccessStatusCode)
            {
                ViewBag.Result = JToken.Parse(await response.Content.ReadAsStringAsync());
            }
            else
            {
                throw new Exception($"Unable to call API. API URL='{appSettings.Api1Url}', StatusCode='{response.StatusCode}'.");
            }

            ViewBag.Title = "Call API1";
            return View("CallApi");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var exceptionHandlerPathFeature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var exception = exceptionHandlerPathFeature?.Error;

            var errorViewModel = new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier };
            errorViewModel.TechnicalErrors = exception != null ? new List<string>(exception.ToString().Split('\n')) : null;

            return View(errorViewModel);
        }
    }
}
