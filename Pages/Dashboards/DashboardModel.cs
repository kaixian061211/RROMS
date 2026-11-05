using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Dashboards;

namespace RROMS.Web.Pages.Dashboards;

/// <summary>What the three role dashboards share: the role's cards from DashboardCards.</summary>
public abstract class DashboardModel : PageModel
{
    private readonly string _role;

    protected DashboardModel(string role) => _role = role;

    public IReadOnlyList<DashboardCard> Cards { get; private set; } = Array.Empty<DashboardCard>();

    public virtual void OnGet() => Cards = DashboardCards.For(_role);
}
