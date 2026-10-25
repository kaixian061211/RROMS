using Microsoft.AspNetCore.Authorization;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Pages.Dashboards;

[Authorize(Roles = PaymentRoles.Staff)]
public class StaffModel : DashboardModel
{
    public StaffModel(IWebHostEnvironment environment) : base(environment, PaymentRoles.Staff) { }
}
