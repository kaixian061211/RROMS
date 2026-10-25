# RROMS design guide

How RROMS pages look and read, so every module feels like one app. Follow it for new pages and when you
touch old ones.

- **See it live:** run the app in Development and open **/Dev/StyleGuide** (the "Style guide" link in the
  dark test-login bar). Every piece below is there with real RROMS content. Its markup is in
  `Pages/Dev/StyleGuide.cshtml`, so copy from it.
- **The files:**
  - `wwwroot/css/rroms.css`: colours, type and components. It re-themes Bootstrap 5.3.3 and adds the
    `rr-*` classes.
  - `wwwroot/fonts/`: the fonts, stored in the app (SIL Open Font License).
  - `wwwroot/js/stepper.js`: makes the − / + buttons of a stepper work.
- `_Layout.cshtml` already loads all of it. Your page only needs Bootstrap classes and `rr-*` classes.

The idea: **a kopitiam**. Marble tabletops, white enamel, painted shop signs, and the paper docket
that goes to the kitchen.


## Principles

1. **One action colour.** Pandan green is for things people can do: buttons, links, the option they
   chose. Status colours describe a state and are never pressed.
2. **Records are dockets.** Anything that changes hands (a booking, an order, a receipt) is shown as a
   paper docket with its reference number in monospace.
3. **Two densities.**
   - Customers get one comfortable column and big tap targets, with the main action at the bottom of
     the screen on a phone.
   - Staff get the full width and the actions on each row.
4. **Summary before detail.** Lead with what needs attention (what's pending, what's ready), then the rest.
5. **Real content only.** Real dishes, prices in RM, real statuses. No placeholder text or invented
   figures.
6. **One bold thing.** The docket is the signature. Everything around it stays plain: no gradients, no
   extra shadows, no decoration.


## Starting a page

```cshtml
@page
@model HistoryModel
@{
    ViewData["Title"] = "My payments";   @* the browser tab shows "My payments · RROMS" *@
}

<div class="rr-narrow">   @* customer pages only: one column, at most 44rem wide. Staff pages leave it out. *@
    <header class="rr-page-header">
        <div>
            <h1>My payments</h1>
            <p class="rr-lede">One line of context, only if it helps.</p>
        </div>
        <div class="rr-page-actions">
            <a asp-page="..." class="btn btn-primary">The page's main action</a>
        </div>
    </header>

    @* alerts, then the content *@
</div>
```

- Use a plain `<h1>`, `<h2>` and `<h3>`. They are already sized and set in the display font, so don't
  add `h3 mb-3` or `display-4`.
- A record page can put its reference above the title:
  `<span class="rr-ref d-block mb-1">RES-000123</span><h1>Edit reservation</h1>`.
- Page-only CSS goes in a `<Page>.cshtml.css` file next to the page (CSS isolation). Don't use inline
  `style=""`.
- **`rroms.css` is shared:** ask the group before changing it.


## Colour

Use the tokens (`var(--rr-pandan)`) or the Bootstrap classes, which already use them (`btn-primary`,
`text-bg-warning`, `alert-danger`). Never type a hex value in a page.

| Name | Token | Hex | Use for |
|---|---|---|---|
| Marble | `--rr-marble` | `#F1F3F1` | Page background |
| Enamel | `--rr-enamel` | `#FFFFFF` | Cards, dockets, the header |
| Kopi-O | `--rr-kopi` | `#1D1A17` | Text and headings |
| Muted | `--rr-muted` | `#5D625C` | Secondary text |
| Line | `--rr-line` | `#D8DCD7` | Borders and rules |
| Pandan | `--rr-pandan` | `#0B6B4F` | Actions, links, chosen options, confirmed / ready / paid |
| Kaya | `--rr-kaya` | `#E9B23C` | Needs attention (pending). A fill behind dark text, never text on white |
| Teh | `--rr-teh` | `#A86B3C` | Being worked on (preparing) |
| Sambal | `--rr-sambal` | `#C2381C` | Cancel, errors, refusals |

Bootstrap's `primary` and `success` are both pandan. `warning` is kaya, `danger` is sambal and `info`
is teh.


## Type

| Role | Font | Where |
|---|---|---|
| Display | Zilla Slab 600/700 | `h1`–`h3`, table numbers, docket titles, the wordmark |
| Body | Plus Jakarta Sans 400–700 | Everything else |
| Monospace | Cascadia Mono / Consolas | References (`RES-000123`) and money on dockets |

- `rr-label`: a small uppercase label ("SEATING", "TABLE"). Only for real labels.
- `rr-ref`: a reference number.
- `rr-money`: an amount, so the digits line up and never wrap.
- `rr-lede`: one line of context under a heading.


## Status pills

Show a status as `<span class="rr-status rr-status--good">Confirmed</span>`, not a Bootstrap badge. The
modifier is what the status **means**, so each module maps its own statuses onto the same six. Put the
mapping in your module's formatter, like `ReservationFormatter.StatusBadge`.

| Modifier | Means | Orders | Reservations | Payments (suggested) |
|---|---|---|---|---|
| `--attention` | Waiting for someone | Pending | Pending | Waiting at counter |
| `--progress` | Being worked on | Preparing | | Paying online |
| `--good` | Done well, ready | Ready | Confirmed | Paid |
| `--done` | Finished | Served, Completed | Completed | |
| `--off` | No longer happening | Cancelled | Cancelled | Refunded |
| `--bad` | Went wrong | | | Failed |


## Buttons

- **One `btn-primary` per view:** the thing most people came to do. Everything else is
  `btn-outline-secondary`.
- **Cancelling:**
  - `btn-outline-danger` for the Cancel button.
  - `btn-danger` only on the page that asks "Cancel this…?".
- **Row actions:** `btn-sm` for the buttons on a row or a docket.
- **Labels say what happens:** "Book table", "Start preparing", "Save changes". Never "Submit" or "OK".


## Components

Copy the markup from `/Dev/StyleGuide`. What each one is for:

| Component | Classes | Use for |
|---|---|---|
| Page header | `rr-page-header`, `rr-page-actions` | The top of every page |
| Docket | `rr-docket` (+ `__head`, `__title`, `__lines`, `__line`, `__qty`, `__amount`, `__total`, `__meta`, `__actions`) | A record that changes hands: order, booking, receipt. Add `rr-docket--narrow` when it stands alone on a page |
| Steps | `rr-steps` with `is-done` / `is-current` on the `<li>` | Where something is in a fixed journey. It doesn't update by itself |
| Order rail | `rr-board`, `rr-board__column`, `__title`, `__rail`, `__dockets`, `__empty`; `rr-board--single` | Staff boards of dockets by status |
| Slip | `rr-slip`, `__body`, `__title`, `__side` | One record in a customer's list |
| Date block | `rr-dateblock` (a `<time>`) | The day a booking is for |
| Staff list | `rr-list`, `__item`, `__main`, `__actions`; `is-attention` | Rows staff work through; `is-attention` adds the kaya edge |
| Group title | `rr-group-title` + `rr-count` | A heading with how many are under it |
| Table number | `rr-tableno`, `__number` | A table number set large |
| Choice tiles | `rr-tiles` (`--wide`), `rr-tile`, `rr-tile-group` | A few options people compare: times, tables, payment methods |
| Stepper | `rr-stepper` + `data-stepper`, `__button`, `__input` | Small counts: party size, quantities |
| Dish rows | `rr-dishes`, `rr-dish`, `__name`, `__price`; `is-chosen` | What can be ordered |
| Panel | `rr-panel` | A white surface for a form |
| Action bar | `rr-actionbar` | A form's buttons; it stays at the bottom of a phone screen |
| Empty state | `rr-empty`, `rr-empty__title` | A list with nothing in it |
| Feature card | `rr-feature`, `__title`, `__meta`; `rr-feature--soon` | Dashboard cards, and choices that are buttons |
| Filters | Bootstrap `nav nav-pills` links | Status filters: one tap, kept in the address. No "Show" button |

A docket, for example:

```html
<article class="rr-docket rr-docket--narrow" aria-label="Order ORD-000123">
    <div class="rr-docket__head">
        <div>
            <span class="rr-docket__title">Table 4</span>
            <span class="rr-ref">ORD-000123</span>
        </div>
        <span class="rr-status rr-status--attention">Pending</span>
    </div>
    <ol class="rr-docket__lines">
        <li class="rr-docket__line"><span class="rr-docket__qty">2 ×</span> <span>Nasi Lemak</span> <span class="rr-docket__amount">RM 25.80</span></li>
    </ol>
    <div class="rr-docket__total"><span>Total</span><span class="rr-money">RM 25.80</span></div>
    <div class="rr-docket__actions">
        <a class="btn btn-primary" href="...">Go to payment</a>
    </div>
</article>
```

The first thing in `rr-docket__actions` takes the width, and anything after it (a Cancel) stays small.


## Forms

- Put the fields in an `rr-panel` and the buttons in an `rr-actionbar` straight inside the `<form>`.
  The bar reaches out to the page edges on a phone, so don't put it inside a `.row`.
- Every field has a visible label. Optional fields say "(optional)"; required ones say nothing.
- **Choice tiles are real radio buttons:**
  `<input type="radio" class="btn-check" id="x" name="Input.TableId" value="3"> <label class="rr-tile" for="x">3</label>`.
  The form sends them like any other field. Add `required` and `data-msg-required="Please choose a table."`
  to ask for a choice.
- **A stepper's number box is a normal input:** the buttons only change it. Add
  `<script src="~/js/stepper.js" asp-append-version="true"></script>` to the page's `Scripts` section.
- **Validation messages:** `<span asp-validation-for="..." class="text-danger"></span>` under the field.
  A field that fails ASP.NET validation turns red by itself, tiles and steppers included.
- A date field is a `type="date"` input; it's already kept to a sensible width.


## Words

- Write from the customer's or staff member's side: "My orders", "Order queue", not "Order entities".
- Sentence case everywhere: "Manage reservations", not "Manage Reservations".
- After an action, say what happened: "RES-000123 is now confirmed."
- Errors say what went wrong and what to do next: "That table is already booked for this time slot.
  Please choose another time or table."
- An empty list says what will appear there and how to add the first one.
- Formats come from the formatter classes, never written in the page:
  - Money: `RM 85.50` (`PaymentFormatter.Money`).
  - Date and time: `26 Oct 2026, 7:30 PM`.
  - References: `ORD-000123`.


## Layout and screens

- **Page margins:** at least 16px on a phone.
- **Font size:** 15px on a phone, 16px from 768px.
- **Navigation:** collapses into the menu button below 1200px.
- **Order rail columns:** four from 1200px, two from 576px, one on a phone.
- **Tap targets:** at least 44px (`--rr-touch`).
- **Keyboard focus:** shows as a green outline; don't remove it.
- **Before a PR, check your pages at 375px wide:**
  - Nothing scrolls sideways.
  - Buttons don't overflow.
  - The main action is easy to reach.
- **Motion:** `prefers-reduced-motion` turns transitions off, so keep any motion small.


## Checklist for a new page

- [ ] Starts with `rr-page-header`; customer pages sit in `rr-narrow`.
- [ ] One `btn-primary`; the other buttons are outline.
- [ ] Statuses are `rr-status` pills, mapped in your formatter.
- [ ] Money, dates and references come from a formatter.
- [ ] The empty list uses `rr-empty`.
- [ ] No inline styles or hex colours; page CSS is in `<Page>.cshtml.css`.
- [ ] No sideways scrolling at 375px, and keyboard focus is visible.
