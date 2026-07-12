# Kototsubo

Kototsubo is a personal collection manager built with ASP.NET Core MVC. It manages books and other media, supports ISBN lookup through openBD, and imports Kindle collection data from CSV or JSON.

## Features

- Collection CRUD with history records
- Search, sorting, filtering, and pagination
- ISBN lookup with openBD
- Kindle CSV/JSON preview and bulk import
- ASP.NET Core Identity authentication
- SQL Server and Entity Framework Core migrations

## Requirements

- .NET 10 SDK
- SQL Server or SQL Server Express

## Setup

Configure the database connection with user secrets or an environment variable:

```powershell
dotnet user-secrets init --project Kototsubo
dotnet user-secrets set "ConnectionStrings:SiteConnection" "Server=.\\SQLEXPRESS;Database=KototsuboDB;Integrated Security=True;TrustServerCertificate=True;" --project Kototsubo
dotnet run --project Kototsubo
```

Alternatively, set `ConnectionStrings__SiteConnection` in the environment.

The application applies Entity Framework Core migrations and creates the required roles at startup. It does not create a default user or a fixed password.

Public registration is disabled by default. To create the first account, enable registration only while the application is available on a trusted local network, register the account, then remove the setting and restart:

```powershell
$env:Security__AllowPublicRegistration = "true"
dotnet run --project Kototsubo
Remove-Item Env:Security__AllowPublicRegistration
```

Do not leave public registration enabled on an Internet-facing deployment. Data Protection keys, production settings, uploaded import data, and publish profiles are runtime secrets and are excluded from Git.

## Verification

```powershell
dotnet build Kototsubo.slnx
dotnet test Tests/Tests.csproj
```

Before pushing, verify that `git status` contains only the intended product changes. This repository uses `main` as its single development and release branch:

```powershell
git add <changed-files>
git commit -m "<message>"
git push origin main
```

Local agent harnesses, handoffs, plans, and private documents are ignored so they cannot be included by a routine push.

