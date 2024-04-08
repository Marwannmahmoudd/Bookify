using Bookify.Web.Core.Consts;
using Bookify.Web.Core.Models;
using Bookify.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bookify.Web.Controllers
{
    public class SubscribersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IImageService _imageService;
		private readonly IDataProtector _dataProtector;

		public SubscribersController(ApplicationDbContext context,IMapper mapper , IImageService imageService ,IDataProtectionProvider dataProtector)
        {
            _context = context;
            this._mapper = mapper;
			_dataProtector = dataProtector.CreateProtector("MySecureKey");
			_imageService = imageService;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Search(SearchFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            var subscriber = _context.Subscribers.SingleOrDefault(
                s => s.Email.Equals(model.Value)
                || s.MobileNumber.Equals(model.Value)
                || s.NationalId.Equals(model.Value));
            var viewmodel = _mapper.Map<SubscriberSearchResultViewModel>(subscriber);
            if(subscriber is not null)
                viewmodel.Key = _dataProtector.Protect(subscriber.Id.ToString());
            return PartialView("_Result", viewmodel);
        }
        public IActionResult Details(string id)
        {
            var subscriberId = int.Parse( _dataProtector.Unprotect(id));
            var subscriber = _context.Subscribers
                .Include(s => s.Governorate)
                .Include(s => s.Area)
                .SingleOrDefault(s => s.Id == subscriberId);

            if (subscriber is null)
                return NotFound();

            var viewModel = _mapper.Map<SubscriberViewModel>(subscriber);
            viewModel.Key = id;
            return View(viewModel);
        }
        [HttpGet]
        public IActionResult Create()
        {
            var viewModel = PopulateViewModel();
            return View("Form", viewModel);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubscriberFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", PopulateViewModel(model));

            var subscriber = _mapper.Map<Subscriber>(model);

            var imageName = $"{Guid.NewGuid()}{Path.GetExtension(model.Image!.FileName)}";
            var imagePath = "/images/Subscribers";

            var (isUploaded, errorMessage) = await _imageService.UploadAsync(model.Image, imageName, imagePath, hasThumbnail: true);

            if (!isUploaded)
            {
                ModelState.AddModelError("Image", errorMessage!);
                return View("Form", PopulateViewModel(model));
            }

			subscriber.ImageUrl = $"{imagePath}/{imageName}";
			subscriber.ImageThumbnailUrl = $"{imagePath}/thumb/{imageName}";
			subscriber.CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;

            _context.Add(subscriber);
            _context.SaveChanges();

			//TODO: Send welcome email
			var subscriberId = _dataProtector.Protect(subscriber.Id.ToString());
			return RedirectToAction(nameof(Index), new { id = subscriberId });
        }
        [HttpGet]
     
        public IActionResult Edit(string id)
        {
            var subcriberId = int.Parse(_dataProtector.Unprotect(id)); 
            var subcriber = _context.Subscribers.Find(subcriberId);
            if (subcriber == null)
                return NotFound();
            var model = _mapper.Map<SubscriberFormViewModel>(subcriber);
            var viewmodel = PopulateViewModel(model);
            viewmodel.Key = id;
            return View("Form", viewmodel);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(SubscriberFormViewModel model)
        {
            if(!ModelState.IsValid)
                return View("Form",PopulateViewModel(model));
			var subscriberId = int.Parse(_dataProtector.Unprotect(model.Key));
			var subscriber = _context.Subscribers.Find(subscriberId);
            if (subscriber is null)
                return NotFound();
            if(model.Image is not null)
            {
                if (!string.IsNullOrEmpty(subscriber.ImageUrl))
                    _imageService.Delete(subscriber.ImageUrl, subscriber.ImageThumbnailUrl);


                var imageName = $"{Guid.NewGuid()}{Path.GetExtension(model.Image.FileName)}";
                var imagePath = "/images/Subscribers";

                var (isUploaded, errorMessage) = await _imageService.UploadAsync(model.Image, imageName, imagePath, hasThumbnail: true);
                if(!isUploaded)
                {
                    ModelState.AddModelError("image", errorMessage!);
                    return View("Form", PopulateViewModel(model));
                }
                model.ImageUrl = $"{imagePath}/{imageName}";
                model.ImageThumbnailUrl = $"{imagePath}/thumb/{imageName}";
            }

            else if (!string.IsNullOrEmpty(subscriber.ImageUrl))
            {
                model.ImageUrl = subscriber.ImageUrl;
                model.ImageThumbnailUrl = subscriber.ImageThumbnailUrl;
            }

            subscriber = _mapper.Map(model, subscriber);
            subscriber.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            subscriber.LastUpdatedOn = DateTime.Now;

            _context.SaveChanges();

            var subscriberId2 = _dataProtector.Protect(subscriber.Id.ToString());
            return RedirectToAction(nameof(Details), new { id = subscriberId2 });

		}
        public IActionResult GetAreas(int governorateId) 
        {
            var areas = _context.Areas
                   .Where(a => a.GovernorateId == governorateId && !a.IsDeleted)
                   .OrderBy(g => g.Name)
                   .ToList();

            return Ok(_mapper.Map<IEnumerable<SelectListItem>>(areas));
        }
        public IActionResult AllowNationalId(SubscriberFormViewModel model)
        {
            var subscriberId = 0;

            if (!string.IsNullOrEmpty(model.Key))
                subscriberId = int.Parse(_dataProtector.Unprotect(model.Key));
            var subscriber = _context.Subscribers.SingleOrDefault(b => b.NationalId == model.NationalId);
            var isAllowed = subscriber is null || subscriber.Id.Equals(subscriberId);
            return Json(isAllowed);
        }
        public IActionResult AllowMobileNumber(SubscriberFormViewModel model)
        {
            var subscriberId = 0;

            if (!string.IsNullOrEmpty(model.Key))
                subscriberId = int.Parse(_dataProtector.Unprotect(model.Key));
            var subscriber = _context.Subscribers.SingleOrDefault(b => b.MobileNumber == model.MobileNumber);
            var isAllowed = subscriber is null || subscriber.Id.Equals(subscriberId);
            return Json(isAllowed);
        }
        public IActionResult AllowEmail(SubscriberFormViewModel model)
        {
            var subscriberId = 0;

            if (!string.IsNullOrEmpty(model.Key))
                subscriberId = int.Parse(_dataProtector.Unprotect(model.Key));
            var subscriber = _context.Subscribers.SingleOrDefault(b => b.Email == model.Email);
            var isAllowed = subscriber is null || subscriber.Id.Equals(subscriberId);
            return Json(isAllowed);
        }
        private SubscriberFormViewModel PopulateViewModel(SubscriberFormViewModel model = null)
        {
            SubscriberFormViewModel viewmodel = model is null ? new SubscriberFormViewModel() : model;


            var governorates = _context.Governorates.Where(a => !a.IsDeleted).OrderBy(a => a.Name).ToList();
            viewmodel.Governorates = _mapper.Map<IEnumerable<SelectListItem>>(governorates);
             
            if(model?.GovernorateId > 0)
            {
                var areas = _context.Areas
                    .Where(a => a.GovernorateId == model.GovernorateId && !a.IsDeleted)
                    .OrderBy(a => a.Name).ToList();

                viewmodel.Areas = _mapper.Map<IEnumerable<SelectListItem>>(areas);
            }
            return viewmodel;
        } 
    }
}
