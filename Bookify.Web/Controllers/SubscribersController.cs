using Bookify.Web.Services;
using Hangfire;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bookify.Web.Controllers
{
    public class SubscribersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMapper _mapper;
        private readonly IImageService _imageService;
        private readonly IDataProtector _dataProtector;
        private readonly IEmailBodyBuilder _emailBodyBuilder;
        private readonly IEmailSender _emailSender;

        public SubscribersController(ApplicationDbContext context, IMapper mapper, IImageService imageService, IDataProtectionProvider dataProtector, IEmailBodyBuilder emailBodyBuilder,
            IEmailSender emailSender)
        {
            _context = context;
            this._mapper = mapper;
            _dataProtector = dataProtector.CreateProtector("MySecureKey");
            _imageService = imageService;
            _emailBodyBuilder = emailBodyBuilder;
            _emailSender = emailSender;
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
            if (subscriber is not null)
                viewmodel.Key = _dataProtector.Protect(subscriber.Id.ToString());
            return PartialView("_Result", viewmodel);
        }
        public IActionResult Details(string id)
        {
            var subscriberId = int.Parse(_dataProtector.Unprotect(id));
            var subscriber = _context.Subscribers
                .Include(s => s.Governorate)
                .Include(s => s.Area)
                  .Include(s => s.Subscribtions)
                  .Include(s => s.Rentals)
                  .ThenInclude(r => r.RentalCopies)
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
            Subscribtion subscribtion = new()
            {
                CreatedById = subscriber.CreatedById,
                CreatedOn = subscriber.CreatedOn,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddYears(1)
            };
            subscriber.Subscribtions.Add(subscribtion);
            _context.Add(subscriber);
            _context.SaveChanges();

            //TODO: Send welcome email
            var placeholders = new Dictionary<string, string>()
            {
                { "imageUrl", "https://res.cloudinary.com/dagpvgkuc/image/upload/v1712041807/icon-positive-vote-2_yxomny.png" },
                { "header", $"Welcome {model.FirstName}," },
                { "body", "thanks for joining Bookify 🤩" }
            };
            var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);
            BackgroundJob.Enqueue(() => _emailSender.SendEmailAsync(
                model.Email,
                "Welcome to Bookify", body));

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

        public async Task<IActionResult> Edit(SubscriberFormViewModel model)
        {
            if (!ModelState.IsValid)
                return View("Form", PopulateViewModel(model));
            var subscriberId = int.Parse(_dataProtector.Unprotect(model.Key));
            var subscriber = _context.Subscribers.Find(subscriberId);
            if (subscriber is null)
                return NotFound();
            if (model.Image is not null)
            {
                if (!string.IsNullOrEmpty(subscriber.ImageUrl))
                    _imageService.Delete(subscriber.ImageUrl, subscriber.ImageThumbnailUrl);


                var imageName = $"{Guid.NewGuid()}{Path.GetExtension(model.Image.FileName)}";
                var imagePath = "/images/Subscribers";

                var (isUploaded, errorMessage) = await _imageService.UploadAsync(model.Image, imageName, imagePath, hasThumbnail: true);
                if (!isUploaded)
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


            return RedirectToAction(nameof(Details), new { id = model.Key });

        }
        [HttpPost]

        public IActionResult RenewSubscription(string sKey)
        {
            var subscriberId = int.Parse(_dataProtector.Unprotect(sKey));
            var subscriber = _context.Subscribers.Include(s => s.Subscribtions).SingleOrDefault(s => s.Id == subscriberId);
            if (subscriber is null)
                return NotFound();
            if (subscriber.IsBlackListed)
                return BadRequest();

            var LastSubscribtion = subscriber.Subscribtions.Last();

            var startDate = LastSubscribtion.EndDate < DateTime.Today ? DateTime.Today :
                LastSubscribtion.EndDate.AddDays(1);
            Subscribtion newsusbscribtion = new()
            {
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value,
                CreatedOn = DateTime.Now,
                StartDate = startDate,
                EndDate = startDate.AddYears(1)

            };
            subscriber.Subscribtions.Add(newsusbscribtion);
            _context.SaveChanges();
            //Send email 
            var placeholders = new Dictionary<string, string>()
            {
                { "imageUrl", "https://res.cloudinary.com/dagpvgkuc/image/upload/v1712041807/icon-positive-vote-2_yxomny.png" },
                { "header", $"Hello {subscriber.FirstName}," },
                { "body", $"your subscription has been renewed through {newsusbscribtion.EndDate.ToString("d MMM, yyyy")} 🎉🎉" }
            };

            var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);
            BackgroundJob.Enqueue(() => _emailSender.SendEmailAsync(
                subscriber.Email,
                "Bookify Subscription Renewal", body));

            var viewmodel = _mapper.Map<SubscriptionViewModel>(newsusbscribtion);
            return PartialView("_SubscriptionRow", viewmodel);
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

            if (model?.GovernorateId > 0)
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
