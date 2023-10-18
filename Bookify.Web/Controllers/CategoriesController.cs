using Bookify.Web.Data;
using Microsoft.AspNetCore.Mvc;

namespace Bookify.Web.Controllers
{
    public class CategoriesController : Controller
    {
        private readonly ApplicationDbContext context;

        public CategoriesController(ApplicationDbContext _context)
        {
            context = _context;
        }
        public IActionResult Index()
        {
            //to do : use viewModel
            var categories = context.Categories.ToList();
            return View(categories);
        }
    }
}
