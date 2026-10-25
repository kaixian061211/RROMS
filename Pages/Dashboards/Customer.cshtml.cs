using Microsoft.AspNetCore.Authorization;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Pages.Dashboards;

[Authorize(Roles = PaymentRoles.Customer)]
public class CustomerModel : DashboardModel
{
    public CustomerModel(IWebHostEnvironment environment) : base(environment, PaymentRoles.Customer) { }
}
