# ServDyne POS - Installation & Setup Guide

## A. Building the installer (your machine, once per release)

1. **Install Inno Setup** (free): https://jrsoftware.org/isdl.php - get "innosetup-6.x.x.exe", install with defaults.
2. **Publish the app** - from `D:\Personal\ORM\RMS\Afsara`:
   ```powershell
   dotnet publish src\Afsara.WinForms -p:PublishProfile=ServDynePOS-win-x64
   ```
   This creates `installer\publish\ServDynePOS.exe` (~190 MB, includes the .NET runtime - the client does NOT need to install .NET).
3. **Compile the installer**: right-click `installer\ServDynePOS.iss` → **Compile**.
   Output: `installer\output\ServDynePOS_Setup_1.0.0.exe` (~60-80 MB compressed).

## B. Client machine - prerequisites

- **Windows 10 or 11 (64-bit)**.
- **SQL Server**. The app needs a database; pick one:
  - **Single terminal (one PC):** install **SQL Server 2022 Express** (free) - https://www.microsoft.com/sql-server/sql-server-downloads → "Express". During install choose **Basic**; it finishes with an instance named `.\SQLEXPRESS`.
  - **Multiple terminals:** one PC acts as the server with SQL Server Express; other PCs point to it over the network. In SQL Server Configuration Manager enable **TCP/IP** and allow port 1433 in Windows Firewall.
- **Windows user running the POS** needs permission to create the database. If setup runs as the same Windows account used daily (common), the default `Trusted_Connection=True` works. Otherwise add SQL login and use `User Id=...;Password=...` in appsettings.json.
- **Thermal printer**: install its Windows driver first; pick it in Settings → KOT/Invoice printer.

## C. Client machine - install steps

1. Copy `ServDynePOS_Setup_1.0.0.exe` to the client PC (USB or share).
2. Run it → Next → Finish. It installs to `C:\Program Files\ServDyne POS` and creates a desktop shortcut.
3. **Edit the connection string** if needed: open `C:\Program Files\ServDyne POS\appsettings.json` in Notepad (Run as administrator → open file) and set:
   - Local SQL Express: `"Server=.\SQLEXPRESS;Database=AfsaraPOS;Trusted_Connection=True;TrustServerCertificate=True;"`
   - Remote server:     `"Server=SERVER-PC\SQLEXPRESS;Database=AfsaraPOS;Trusted_Connection=True;TrustServerCertificate=True;"`
   - SQL auth:          `"Server=.\SQLEXPRESS;Database=AfsaraPOS;User Id=pos;Password=***;TrustServerCertificate=True;"`
4. Launch **ServDyne POS** from the desktop. First run takes ~30-60 s (creates the `AfsaraPOS` database and seeds a sample menu).
5. Sign in: **admin / admin123** → click your name in the header → change the password.
6. Open **Settings** (gear): restaurant name, address, phone, NTN, taxes, printers → **Save** → **Test Print**.
7. Open **Setup**: enter real categories, menu items, tables, waiters, users.

## D. Upgrading a client

1. Publish + compile a new installer with a higher `MyAppVersion`.
2. Run the new setup on the client PC. It overwrites the exe but **keeps** `appsettings.json` and the database (data is never touched).
3. On first run the app applies any new database migrations automatically.

## E. Common problems

| Symptom | Fix |
|---|---|
| "Database Error" on launch | SQL Server not running (check "SQL Server (SQLEXPRESS)" service) or wrong server name in appsettings.json |
| First run very slow | Normal - database is being created |
| Printer not printing | Install the Windows driver, then select it in Settings |
| SmartScreen warning | Unsigned exe - click "More info → Run anyway" (a code-signing certificate removes this) |

## Notes

- The app is Windows-only; .NET 8 runtime is bundled inside the exe.
- Data lives in the `AfsaraPOS` SQL Server database - back it up with SQL Server Management Studio or `sqlcmd`.
- User data is preserved on uninstall? No - uninstall removes program files only; the database stays intact in SQL Server.
