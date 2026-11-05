using Microsoft.AspNetCore.Authorization;
using RROMS.Web.Models.Dashboards;
using RROMS.Web.Models.Payments;

namespace RROMS.Web.Pages.Dashboards;

[Authorize(Roles = PaymentRoles.Admin)]
public class AdminModel : DashboardModel
{
    public AdminModel() : base(PaymentRoles.Admin) { }

    /// <summary>The staff pages an Admin also uses: reservations, the order queue and counter payments.</summary>
    public IReadOnlyList<DashboardCard> Operations { get; private set; } = Array.Empty<DashboardCard>();

    public override void OnGet()
    {
        base.OnGet();
        Operations = DashboardCards.DailyOperations;
    }
}
