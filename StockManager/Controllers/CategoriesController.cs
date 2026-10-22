using Microsoft.AspNetCore.Mvc;

namespace StockManager.Controllers;

public class CategoriesController : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}