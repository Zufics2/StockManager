using Microsoft.AspNetCore.Mvc;

namespace StockManager.Controllers;

public class AuthController : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}