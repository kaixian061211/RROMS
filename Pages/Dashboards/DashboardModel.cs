using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Dashboards;

namespace RROMS.Web.Pages.Dashboards;

/// <summary>What the three role dashboards share: the role's cards from DashboardCards.</summary>
public abstract class DashboardModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly string _role;

    protected DashboardModel(IWebHostEnvironment environment, string role)
    {
        _environment = environment;
        _role = role;
    }

    public IReadOnlyList<DashboardCard> Cards { get; private set; } = Array.Empty<DashboardCard>();

    /// <summary>Development also shows features not built yet, and who owns each one.</summary>
    public bool IsDevelopment => _environment.IsDevelopment();

    public void OnGet() => Cards = DashboardCards.For(_role, IsDevelopment);
}
