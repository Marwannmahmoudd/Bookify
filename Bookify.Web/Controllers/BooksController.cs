﻿using Bookify.Web.Filters;
using Bookify.Web.Services;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Linq;
using System.Linq.Dynamic.Core;

namespace Bookify.Web.Controllers
{
    [Authorize(Roles = AppRoles.Archive)]
    public class BooksController : Controller
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly Cloudinary _cloudinary;
        private List<string> _allowedExtensions = new() { ".jpg", ".jpeg", ".png" };
        private int _maxAllowedSize = 2097152;
        public IImageService _ImageService { get; }

        public BooksController(ApplicationDbContext context, IMapper mapper,
            IWebHostEnvironment webHostEnvironment, IOptions<CloudinarySetting> cloudinary , IImageService imageService)
        {
            _context = context;
            _mapper = mapper;
            _webHostEnvironment = webHostEnvironment;
            _ImageService = imageService;
            Account account = new()
            {
                Cloud = cloudinary.Value.Cloud,
                ApiKey = cloudinary.Value.ApiKey,
                ApiSecret = cloudinary.Value.ApiSecret
            };

            _cloudinary = new Cloudinary(account);
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult GetBooks()
        {
            var skip = int.Parse(Request.Form["start"]);
            var pageSize = int.Parse(Request.Form["length"]);

            var searchValue = Request.Form["search[value]"];

            var sortColumnIndex = Request.Form["order[0][column]"];
            var sortColumn = Request.Form[$"columns[{sortColumnIndex}][name]"];
            var sortColumnDirection = Request.Form["order[0][dir]"];

            IQueryable<Book> books = _context.Books
                .Include(b => b.Author)
                .Include(b => b.Categories)
                .ThenInclude(c => c.Category);

            if (!string.IsNullOrEmpty(searchValue)) 
                books = books.Where(b => b.Title.Contains(searchValue) || b.Author!.Name.Contains(searchValue));

            books = books.OrderBy($"{sortColumn} {sortColumnDirection}");

            var data = books.Skip(skip).Take(pageSize).ToList();

            var mappedData = _mapper.Map<IEnumerable<BookViewModel>>(data);

            var recordsTotal = books.Count();

            var jsonData = new { recordsFiltered = recordsTotal, recordsTotal, data = mappedData };

            return Ok(jsonData);
        }

        public IActionResult Details(int id)
        {
            var book = _context.Books
                .Include(b => b.Author)
                .Include(b => b.Copies)
                .Include(b => b.Categories)
                .ThenInclude(c => c.Category)
                .SingleOrDefault(b => b.Id == id);

            if (book is null)
                return NotFound();

            var viewModel = _mapper.Map<BookViewModel>(book);

            return View(viewModel);
        }

        public IActionResult Create()
        {
            return View("Form", PopulateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BookFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", PopulateViewModel(model));

            var book = _mapper.Map<Book>(model);

            if (model.Image is not null)
            {
                var imageName = $"{Guid.NewGuid()}{Path.GetExtension(model.Image.FileName)}";
                var (uploaded, errormessage) = await _ImageService.UploadAsync(model.Image, imageName, "/images/books", hasThumbnail: true);
                if (uploaded)
                {
                    book.ImageUrl = $"/images/books/{imageName}";
                    book.ImageThumbnailUrl = $"/images/books/thumb/{imageName}";
                }
                else
                {
                    ModelState.AddModelError(nameof(Image), errormessage!);
                    return View("Form", PopulateViewModel(model));
                }
               
                //using var straem = model.Image.OpenReadStream();

                //var imageParams = new ImageUploadParams
                //{
                //    File = new FileDescription(imageName, straem),
                //    UseFilename = true
                //};

                //var result = await _cloudinary.UploadAsync(imageParams);

                //book.ImageUrl = result.SecureUrl.ToString();
                //book.ImageThumbnailUrl = GetThumbnailUrl(book.ImageUrl);
                //book.ImagePublicId = result.PublicId;
            }
            book.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            foreach (var category in model.SelectedCategories)
                book.Categories.Add(new BookCategory { CategoryId = category });

            _context.Add(book);
            _context.SaveChanges();

            return RedirectToAction(nameof(Details), new { id = book.Id });
        }

        public IActionResult Edit(int id)
        {
            var book = _context.Books.Include(b => b.Categories).SingleOrDefault(b => b.Id == id);

            if (book is null)
                return NotFound();

            var model = _mapper.Map<BookFormViewModel>(book);
            var viewModel = PopulateViewModel(model);

            viewModel.SelectedCategories = book.Categories.Select(c => c.CategoryId).ToList();

            return View("Form", viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(BookFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", PopulateViewModel(model));

            var book = _context.Books.Include(b => b.Categories)
                .Include(b => b.Copies)
                .SingleOrDefault(b => b.Id == model.Id);

            if (book is null)
                return NotFound();

            //string imagePublicId = null;
            if (model.Image is not null)
            {
                _ImageService.Delete(book.ImageUrl, book.ImageThumbnailUrl);

                //await _cloudinary.DeleteResourcesAsync(book.ImagePublicId);

                var imageName = $"{Guid.NewGuid()}{Path.GetExtension(model.Image.FileName)}";

                var (uploaded, errormessage) = await _ImageService.UploadAsync(model.Image,imageName, "/images/books", hasThumbnail: true);
            if (uploaded)
            {
                model.ImageUrl = $"/images/books/{imageName}";
                model.ImageThumbnailUrl = $"/images/books/thumb/{imageName}";
            }
            else
            {
                ModelState.AddModelError(nameof(Image), errormessage!);
                return View("Form", PopulateViewModel(model));
            }
            
        }

            else if (!string.IsNullOrEmpty(book.ImageUrl))
            {
                model.ImageUrl = book.ImageUrl;
                model.ImageThumbnailUrl = book.ImageThumbnailUrl;
            }

            book = _mapper.Map(model, book);
            book.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            book.LastUpdatedOn = DateTime.Now;


            if (!model.IsAvailableForRental)           
                foreach (var item in book.Copies)
                    item.IsAvailableForRental = false;          
                          
                foreach (var category in model.SelectedCategories)
                book.Categories.Add(new BookCategory { CategoryId = category });

            _context.SaveChanges();

            return RedirectToAction(nameof(Details), new { id = book.Id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleStatus(int id)
        {
            var book = _context.Books.Find(id);

            if (book is null)
                return NotFound();

            book.IsDeleted = !book.IsDeleted;
            book.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            return Ok();
        }

        public IActionResult AllowItem(BookFormViewModel model)
        {
            var book = _context.Books.SingleOrDefault(b => b.Title == model.Title && b.AuthorId == model.AuthorId);
            var isAllowed = book is null || book.Id.Equals(model.Id);

            return Json(isAllowed);
        }

        private BookFormViewModel PopulateViewModel(BookFormViewModel? model = null)
        {
            BookFormViewModel viewModel = model is null ? new BookFormViewModel() : model;

            var authors = _context.Authors.Where(a => !a.IsDeleted).OrderBy(a => a.Name).ToList();
            var categories = _context.Categories.Where(a => !a.IsDeleted).OrderBy(a => a.Name).ToList();

            viewModel.Authors = _mapper.Map<IEnumerable<SelectListItem>>(authors);
            viewModel.Categories = _mapper.Map<IEnumerable<SelectListItem>>(categories);

            return viewModel;
        }

        private string GetThumbnailUrl(string url)
        {
            var separator = "image/upload/";
            var urlParts = url.Split(separator);

            var thumbnailUrl = $"{urlParts[0]}{separator}c_thumb,w_200,g_face/{urlParts[1]}";

            return thumbnailUrl;
        }
    }
}