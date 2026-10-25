using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RROMS.Tests;

/// <summary>
/// Runs a step just before a DbContext saves, after the service has done its checks. Tests use it to make
/// something else happen in between, such as a second booking arriving at the same moment.
/// </summary>
public class BeforeSaving : SaveChangesInterceptor
{
    private readonly Func<Task> _step;

    public BeforeSaving(Func<Task> step) => _step = step;

    public bool Ran { get; private set; }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (!Ran)
        {
            Ran = true;
            await _step();
        }
        return result;
    }
}
