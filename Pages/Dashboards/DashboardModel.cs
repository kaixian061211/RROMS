using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Dashboards;

namespace RROMS.Web.Pages.Dashboards;

/// <summary>What the three role dashboards share: the role's cards from DashboardCards, and the greeting.</summary>
public abstract class DashboardModel : PageModel
{
    private readonly string _role;

    protected DashboardModel(string role) => _role = role;

    public IReadOnlyList<DashboardCard> Cards { get; private set; } = Array.Empty<DashboardCard>();

    /// <summary>Today, for the dashboards that show today's bookings and service.</summary>
    public DateOnly Today { get; } = DateOnly.FromDateTime(DateTime.Now);

    /// <summary>"Good morning", "Good afternoon" or "Good evening", by the restaurant's clock.</summary>
    public string Greeting => DateTime.Now.Hour switch
    {
        < 12 => "Good morning",
        < 18 => "Good afternoon",
        _ => "Good evening"
    };

    public virtual Task OnGetAsync()
    {
        Cards = DashboardCards.For(_role);
        return Task.CompletedTask;
    }
}
