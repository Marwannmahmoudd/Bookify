using Bookify.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
using System.Text.Encodings.Web;

namespace Bookify.Web.Controllers
{
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMapper _mapper;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IEmailSender _emailSender;
        private readonly IEmailBodyBuilder _emailBodyBuilder;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public UsersController(UserManager<ApplicationUser> userManager, IMapper mapper, IWebHostEnvironment webHostEnvironment, RoleManager<IdentityRole> roleManager, IEmailSender emailSender, IEmailBodyBuilder emailBodyBuilder)
        {
            _userManager = userManager;
            _mapper = mapper;
            this._roleManager = roleManager;
            _emailSender = emailSender;
            _emailBodyBuilder = emailBodyBuilder;
            _webHostEnvironment = webHostEnvironment;
        }
        public async Task<IActionResult> Index()
        {

            var users = await _userManager.Users.ToListAsync();
            var viewModel = _mapper.Map<IEnumerable<UserViewModel>>(users);
            return View(viewModel);
        }
        [HttpGet]
        [AjaxOnly]
        public async Task<IActionResult> Create()
        {
            var viewModel = new UserFormViewModel
            {
                Roles = await _roleManager.Roles
                                .Select(r => new SelectListItem
                                {
                                    Text = r.Name,
                                    Value = r.Name
                                })
                                .ToListAsync()
            };

            return PartialView("_Form", viewModel);
        }
        [HttpPost]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            ApplicationUser user = new()
            {
                FullName = model.FullName,
                UserName = model.UserName,
                Email = model.Email,
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value
            };
            var result = await _userManager.CreateAsync(user, model.Password);
            if (result.Succeeded)
            {
                await _userManager.AddToRolesAsync(user, model.SelectedRoles);

                var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ConfirmEmail",
                    pageHandler: null,
                    values: new { area = "Identity", userId = user.Id, code },
                    protocol: Request.Scheme);
                var placeholders = new Dictionary<string, string>()
                {
                    { "imageUrl", "https://res.cloudinary.com/dagpvgkuc/image/upload/v1711847027/icon-positive-vote-1_vktxt1_ar0aqr.png" },
                    { "header", $"Hey {user.FullName}, thanks for joining us!" },
                    { "body",   "please confirm your email" },
                    { "url", $"{HtmlEncoder.Default.Encode(callbackUrl!)}" },
                    { "linkTitle", "Active Account!"}
                };
                var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Email, placeholders);
                await _emailSender.SendEmailAsync(user.Email, "Confirm your email", body);

                var viewmodel = _mapper.Map<UserViewModel>(user);
                return PartialView("_UserRow", viewmodel);
            }
            return BadRequest(string.Join(',', result.Errors.Select(e => e.Description)));
        }
        [HttpGet]
        [AjaxOnly]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();
            try
            {
                var viewmodel = _mapper.Map<UserFormViewModel>(user);
                viewmodel.SelectedRoles = await _userManager.GetRolesAsync(user);
                viewmodel.Roles = await _roleManager.Roles
                         .Select(r => new SelectListItem
                         {
                             Text = r.Name,
                             Value = r.Name
                         }).ToListAsync();
                return PartialView("_Form", viewmodel);
            }
            catch (Exception ex)
            {

                throw new Exception(ex.Message);

            }
        }
        [HttpPost]

        public async Task<IActionResult> Edit(UserFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user is null) return NotFound();
            user = _mapper.Map(model, user);
            user.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            user.LastUpdatedOn = DateTime.Now;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                var currentRoles = await _userManager.GetRolesAsync(user);

                var rolesUpdated = !currentRoles.SequenceEqual(model.SelectedRoles);

                if (rolesUpdated)
                {
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                }
                await _userManager.UpdateSecurityStampAsync(user);
                var viewModel = _mapper.Map<UserViewModel>(user);
                return PartialView("_UserRow", viewModel);
            }
            return BadRequest(string.Join(',', result.Errors.Select(e => e.Description)));
        }
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();
            user.IsDeleted = !user.IsDeleted;
            user.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            user.LastUpdatedOn = DateTime.Now;
            if (user.IsDeleted)
                await _userManager.UpdateSecurityStampAsync(user);
            await _userManager.UpdateAsync(user);
            return Ok(user.LastUpdatedOn.ToString());
        }
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();
            var viewmodel = new ResetPasswordFormViewModel { Id = user.Id };
            return
                PartialView("_ResetPasswordForm", viewmodel);
        }
        [HttpPost]

        public async Task<IActionResult> ResetPassword(ResetPasswordFormViewModel model)
        {
            if (!ModelState.IsValid)
                return BadRequest();
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user is null)
                return NotFound();
            var currentPasswordHash = user.PasswordHash;
            await _userManager.RemovePasswordAsync(user);
            var result = await _userManager.AddPasswordAsync(user, model.Password);
            if (result.Succeeded)
            {
                user.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
                user.LastUpdatedOn = DateTime.Now;
                await _userManager.UpdateAsync(user);
                var viewModel = _mapper.Map<UserViewModel>(user);
                return PartialView("_UserRow", viewModel);
            }
            user.PasswordHash = currentPasswordHash;
            await _userManager.UpdateAsync(user);
            return BadRequest(string.Join(',', result.Errors.Select(e => e.Description)));
        }
        [HttpPost]

        public async Task<IActionResult> Unlock(string? id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null) return NotFound();
            var isLocked = await _userManager.IsLockedOutAsync(user);

            if (isLocked)
                await _userManager.SetLockoutEndDateAsync(user, null);
            return Ok();
        }
        public async Task<IActionResult> AllowUserName(UserFormViewModel model)
        {
            var user = await _userManager.FindByNameAsync(model.UserName);
            var isAllowed = user is null || user.Id.Equals(model.Id);
            return Json(isAllowed);
        }
        public async Task<IActionResult> AllowEmail(UserFormViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            var isAllowed = user is null || user.Id.Equals(model.Id);
            return Json(isAllowed);
        }
    }
}
