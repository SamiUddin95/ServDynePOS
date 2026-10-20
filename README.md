# ServDyne POS

*Smart Dining. Seamless Operations.*

Restaurant Point of Sale for Windows (.NET 8 WinForms + EF Core + SQL Server), built with Clean Architecture. Product: **ServDyne POS** by **ServDyne**.

## Features

- **Order types**: Dine In (table selection), Take Away, Delivery (customer, address, delivery charges)
- **Kitchen Order Tickets (KOT)**: first KOT, additional KOT (only newly added quantities), cancellation KOT for voided items/orders, grouped by kitchen section
- **Billing**: guest bill, cash/card/online settlement, tax % per payment method, service charges, % or flat discounts, change calculation
- **Thermal printing** (80mm) of KOTs, invoices and day-end sales summary, with optional print preview and separate kitchen/counter printers
- **Business day (Opening balance & Day Close)**: open the day with the opening cash float; all orders are posted to the open day's business date (even past midnight); Day Close (Manager/Admin) is blocked while orders are running, counts cash by denomination, shows expected vs counted cash with short/over variance (reason required), and prints a **Z-Report**. An **X-Report** (status) can be printed anytime; closed days can be reprinted from Reports > Day Closings
- **Running orders** and table status (free/occupied, amount, elapsed time)
- **Setup**: menu items, categories, tables, waiters, users (Admin / Manager / Cashier roles)
- **Reports**: sales summary, payment and order-type breakdown, item-wise sales, invoice reprint, export to **Excel (.xlsx)** and **PDF**

## Solution Structure

| Project | Responsibility |
|---|---|
| `src/Afsara.Domain` | Entities, enums, order calculation rules |
| `src/Afsara.Application` | Use-case services (orders, catalog, auth, reports, settings), print DTOs |
| `src/Afsara.Infrastructure` | EF Core `AppDbContext`, configurations, migrations, seeding, password hashing |
| `src/Afsara.Printing` | GDI+ thermal receipt renderer and KOT / invoice / summary layouts |
| `src/Afsara.WinForms` | UI: login, POS screen, dialogs, setup, settings, reports |
| `tests/Afsara.Tests` | xUnit tests for order math and KOT delta logic |

## Requirements

- Windows, .NET 8 SDK
- SQL Server (Express/LocalDB/Developer)

## Getting Started

1. Set the connection string in `src/Afsara.WinForms/appsettings.json` (default: `Server=.;Database=AfsaraPOS;Trusted_Connection=True;...`).
   For LocalDB use `Server=(localdb)\\MSSQLLocalDB;...`.
2. Run:
   ```powershell
   dotnet run --project src/Afsara.WinForms
   ```
   The database is created, migrated, and seeded with a sample menu on first start.
3. Sign in with **admin / admin123**. Change the password by clicking your name in the header.
4. Open **Settings** to set the restaurant details, taxes, and KOT/invoice printers. Use **Test Print** to check the output.

## Keyboard Shortcuts (POS)

| Key | Action |
|---|---|
| F1 | New order / clear screen |
| F2 | Search items (Enter adds exact code or single match) |
| F4 | Hold (save as running order) |
| F5 | Send KOT |
| F6 | Running orders |
| F7 | Select table |
| F8 | Print guest bill |
| F9 | Pay / settle |
| + / - / Del | Change quantity / remove selected cart line |

## Development

```powershell
dotnet build Afsara.POS.sln
dotnet test tests/Afsara.Tests
dotnet ef migrations add <Name> -p src/Afsara.Infrastructure -s src/Afsara.Infrastructure
```

## Shipping a Release (Setup exe)

```powershell
dotnet publish src/Afsara.WinForms -p:PublishProfile=ServDynePOS-win-x64
# then compile installer\ServDynePOS.iss with Inno Setup 6 -> installer\output\ServDynePOS_Setup_<version>.exe
```

Full step-by-step for you and the client: `installer/INSTALLATION.md`.
