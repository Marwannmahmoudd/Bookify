using Bookify.Web.Services;
using Microsoft.AspNetCore.Identity.UI.Services;


namespace Bookify.Web.Tasks
{
    public class HangfireTasks
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;


        private readonly IEmailBodyBuilder _emailBodyBuilder;
        private readonly IEmailSender _emailSender;

        public HangfireTasks(ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment,

            IEmailBodyBuilder emailBodyBuilder,
            IEmailSender emailSender)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;

            _emailBodyBuilder = emailBodyBuilder;
            _emailSender = emailSender;
        }
        public async Task PrepareExpirationAlert()
        {
            var subscribers = _context.Subscribers
                .Include(s => s.Subscribtions)
                .Where(s => !s.IsBlackListed && s.Subscribtions.OrderByDescending(x => x.EndDate).First().EndDate == DateTime.Today.AddDays(5))
                .ToList();

            foreach (var subscriber in subscribers)
            {
                var endDate = subscriber.Subscribtions.Last().EndDate.ToString("d MMM, yyyy");

                //Send email and WhatsApp Message
                var placeholders = new Dictionary<string, string>()
                {
                    { "imageUrl", "https://res.cloudinary.com/devcreed/image/upload/v1671062674/calendar_zfohjc.png" },
                    { "header", $"Hello {subscriber.FirstName}," },
                    { "body", $"your subscription will be expired by {endDate} 🙁" }
                };

                var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);

                await _emailSender.SendEmailAsync(
                    subscriber.Email,
                    "Bookify Subscription Expiration", body);


            }
        }

        public async Task RentalsExpirationAlert()
        {
            var tomorrow = DateTime.Today.AddDays(1);

            var rentals = _context.Rentals
                    .Include(r => r.Subscriber)
                    .Include(r => r.RentalCopies)
                    .ThenInclude(c => c.BookCopy)
                    .ThenInclude(bc => bc!.Book)
                    .Where(r => r.RentalCopies.Any(r => r.EndDate.Date == tomorrow))
                    .ToList();

            foreach (var rental in rentals)
            {
                var expiredCopies = rental.RentalCopies.Where(c => c.EndDate.Date == tomorrow && !c.ReturnDate.HasValue).ToList();

                var message = $"your rental for the below book(s) will be expired by tomorrow {tomorrow.ToString("dd MMM, yyyy")} 💔:";
                message += "<ul>";

                foreach (var copy in expiredCopies)
                {
                    message += $"<li>{copy.BookCopy!.Book!.Title}</li>";
                }

                message += "</ul>";

                var placeholders = new Dictionary<string, string>()
                {
                    { "imageUrl", "https://res.cloudinary.com/devcreed/image/upload/v1671062674/calendar_zfohjc.png" },
                    { "header", $"Hello {rental.Subscriber!.FirstName}," },
                    { "body", message }
                };

                var body = _emailBodyBuilder.GetEmailBody(EmailTemplates.Notification, placeholders);

                await _emailSender.SendEmailAsync(
                    rental.Subscriber!.Email,
                    "Bookify Rental Expiration 🔔", body);
            }
        }
    }
}