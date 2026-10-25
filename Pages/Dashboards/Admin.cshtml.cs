using Microsoft.AspNetCore.Authorization;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Pages.Dashboards;

[Authorize(Roles = PaymentRoles.Admin)]
public class AdminModel : DashboardModel
{
    public AdminModel(IWebHostEnvironment environment) : base(environment, PaymentRoles.Admin) { }
}
