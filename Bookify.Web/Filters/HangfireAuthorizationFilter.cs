using Hangfire.Annotations;
using Hangfire.Dashboard;

namespace Bookify.Web.Filters
{
	public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
	{
		private string _policyName;

		public HangfireAuthorizationFilter(string policyName)
		{
			_policyName = policyName;
		}

		public bool Authorize([NotNull] DashboardContext context)
		{
			var httpcontext  = context.GetHttpContext();
			var authService = httpcontext.RequestServices.GetRequiredService<IAuthorizationService>();
			var isAuthorized = authService.AuthorizeAsync(httpcontext.User,_policyName)
				.ConfigureAwait(false)
				.GetAwaiter()
				.GetResult().Succeeded;
			return isAuthorized;
		}
	}
}
