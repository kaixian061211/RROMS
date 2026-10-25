namespace RROMS.Web.Models.Dashboards;

/// <summary>One feature on a role's dashboard. PagePath is null until the feature is built.</summary>
public record DashboardCard(string Title, string Description, int Story, string Owner, string Role, string? PagePath)
{
    public bool IsBuilt => PagePath is not null;
}
