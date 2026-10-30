# Customer history and preferences: 4 small edits to shared files

The new files are self-contained. These shared files need a few lines each (kept out of the zip so they
don't overwrite your teammates' changes).

## 1. Program.cs: register the two services

Add `using RROMS.Web.Services.CustomerHistoryAndPreferences;` at the top, then next to the other modules:

```csharp
// ---- Customer history and preferences module ----
builder.Services.AddScoped<ICustomerHistoryService, CustomerHistoryService>();
builder.Services.AddScoped<ICustomerPreferenceService, CustomerPreferenceService>();
```

## 2. Pages/Shared/_Layout.cshtml: navigation

Inside `@if (isCustomer)` (the block with Reservations and My orders), after "My orders":

```cshtml
<li class="nav-item">
    <a class="nav-link @Active(Under("/History"))" aria-current="@Current(Under("/History"))" asp-page="/History/Index">History</a>
</li>
<li class="nav-item">
    <a class="nav-link @Active(currentPage == "/Preferences/Index")" aria-current="@Current(currentPage == "/Preferences/Index")" asp-page="/Preferences/Index">My preferences</a>
</li>
```

Inside `@if (isStaff)`, after "Customer feedback":

```cshtml
<li class="nav-item">
    <a class="nav-link @Active(Under("/Preferences/Manage"))" aria-current="@Current(Under("/Preferences/Manage"))" asp-page="/Preferences/Manage/Index">Customer preferences</a>
</li>
```

## 3. Models/Dashboards/DashboardCards.cs: turn on the two cards

```csharp
new("History", "Your past reservations, orders and payments.", 15, "Vishalinii", Customer, "/History"),
...
new("Customer preferences", "See what customers like.", 18, "Vishalinii", Staff, "/Preferences/Manage"),
```

## 4. Tests/RROMS.Tests/DashboardCardTests.cs: expected cards

The two cards are now built, so story 15 joins the customer list and story 18 joins the staff list
outside Development. Staff becomes `"6, 9, 11, 17, 18"` for both `true` and `false`; add `15` to the
customer `false` list.

## 5. Create the table

```powershell
dotnet ef migrations add AddCustomerPreference
dotnet ef database update
```

The `CustomerPreference` table comes from `Models/CustomerHistoryAndPreferences/CustomerPreferenceConfiguration.cs`.
