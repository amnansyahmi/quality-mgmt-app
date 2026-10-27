using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmaComplaints.Models;

namespace SmaComplaints.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Index","Complaints");
    }

    public IActionResult Privacy()
    {
        return RedirectToAction("Index","Complaints");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
