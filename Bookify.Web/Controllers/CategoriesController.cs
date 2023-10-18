using Bookify.Web.Core.Models;
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
        public IActionResult Create()
        {  
            return View("Form");
        }
        [HttpPost]
        public IActionResult Create(CategoryFormViewModel model)
        {
            if(ModelState.IsValid)
            {
                var category = new Category
                {
                    Name = model.Name
                };
                context.Categories.Add(category);
                context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            return View("Form",model);
        }
        public IActionResult Edit(int id)
        {
            var category = context.Categories.Find(id);
            if(category == null)
            {
                return NotFound();
            }
            var viewModel = new CategoryFormViewModel { 
                Id=category.Id,
                Name = category.Name };

            return View("Form",viewModel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(CategoryFormViewModel model)
        {
            var category = context.Categories.Find(model.Id);
            if (category == null)
            {
                return NotFound();
            }
            if (ModelState.IsValid)
            {
                category.Name = model.Name;
                category.LastUpdatedOn = DateTime.Now;
                context.SaveChanges();
                return RedirectToAction(nameof (Index));
            }
           
           
        

            return View("Form", model);
        }
    }
}
