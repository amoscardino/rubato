using Microsoft.AspNetCore.Mvc;

namespace Rubato.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();
}
