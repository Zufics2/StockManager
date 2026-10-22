using Microsoft.AspNetCore.Mvc;

namespace StockManager.Controllers;

public class ProductsController : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }
}