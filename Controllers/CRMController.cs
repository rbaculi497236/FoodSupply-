using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodSupply.Controllers
{
    [Authorize]
    public class CRMController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}