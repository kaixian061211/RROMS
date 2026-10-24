using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RROMS.Web.Models.Payments;
using RROMS.Web.Services.Payments;

namespace RROMS.Web.Pages.Payments;

[Authorize(Roles = PaymentRoles.AdminOrStaff)]
public class IndexModel : PageModel
{
    private readonly IPaymentQueries _queries;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IPaymentQueries queries,
        ILogger<IndexModel> logger)
    {
        _queries = queries;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public PaymentFilter Filter { get; set; } = new();

    public List<PaymentDetails> Rows { get; private set; } = new();

    public decimal PaidTotal =>
        Rows
            .Where(row => row.Status == PaymentStatus.Paid)
            .Sum(row => row.Amount);

    public async Task<IActionResult> OnGetAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            Rows = await _queries.SearchAsync(Filter);
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not load the payment transaction list");

            Response.StatusCode =
                StatusCodes.Status500InternalServerError;

            ModelState.AddModelError(
                string.Empty,
                "Payments could not be loaded. Please try again later.");

            return Page();
        }
    }
}