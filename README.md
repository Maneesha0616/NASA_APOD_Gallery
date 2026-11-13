# NASA APOD Gallery - ASP.NET Core MVC Application

A full-stack ASP.NET Core MVC web application that consumes **NASA's Astronomy Picture of the Day (APOD) REST API**, stores the data in **Microsoft SQL Server** using low-level **ADO.NET (`Microsoft.Data.SqlClient`)** with raw parameterized queries, and showcases the images and video content in a modern, responsive cosmic gallery.

---

## Table of Contents

- [Features](#features)
- [Architecture & Tech Stack](#architecture--tech-stack)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
  - [1. Database Setup](#1-database-setup)
  - [2. NASA API Key Configuration](#2-nasa-api-key-configuration)
  - [3. Running the Application](#3-running-the-application)
- [Project Structure](#project-structure)
- [Implementation Details](#implementation-details)
  - [NASA APOD API Consumption & DTOs](#nasa-apod-api-consumption--dtos)
  - [ADO.NET Data Access Layer](#adonet-data-access-layer)
  - [Duplicate Prevention](#duplicate-prevention)
  - [Responsive Gallery View](#responsive-gallery-view)
- [Database Schema](#database-schema)
- [License](#license)

---

## Features

- **NASA REST API Integration**: Queries the NASA APOD endpoint (`https://api.nasa.gov/planetary/apod`) across custom date ranges and deserializes the JSON array response into C# DTOs.
- **Pure ADO.NET Persistence**: Uses `SqlConnection`, `SqlCommand`, `SqlParameter`, and `SqlDataReader` to perform all database transactions with parameterized SQL queries.
- **Duplicate Prevention**: Atomic SQL existence checks ensure existing entries for a given date are never duplicated in the database.
- **Responsive Astronomy Gallery**:
  - Displays all saved APOD records in a responsive grid layout.
  - Image blocks featuring card zoom animations, title, date badges, and media type pills.
  - Interactive preview modal for instant viewing of photos, video embeds, and scientific explanations without reloading.
  - Dedicated details page (`/Home/Details/{id}`) with direct links to high-definition (HD) full-resolution images.
  - Real-time search filter by title or explanation keywords.
  - Media filter for images vs. videos.
  - Sorting by newest or oldest date.
- **Quick Date Presets**: One-click presets for Last 7 Days, Last 14 Days, and Last 30 Days.
- **Automatic Database Initialization**: Checks and provisions the database, table, and performance indexes on startup if they do not yet exist.

---

## Architecture & Tech Stack

| Layer | Technology |
|---|---|
| **Framework** | .NET 10 / ASP.NET Core MVC |
| **Data Access** | Pure ADO.NET via `Microsoft.Data.SqlClient` (Raw SQL, Parameterized) |
| **Database** | Microsoft SQL Server / SQL Server Express / LocalDB |
| **External API** | NASA Open APIs (`planetary/apod`) |
| **Frontend** | Razor Views (`.cshtml`), Bootstrap 5, Bootstrap Icons, Custom CSS |
| **Serialization** | `System.Text.Json` |

---

## Prerequisites

Ensure the following tools are installed on your workstation:
1. **.NET SDK** (.NET 10, .NET 8, or .NET 7 SDK)
   - Verify with: `dotnet --version`
2. **Microsoft SQL Server**, **SQL Server Express**, or **LocalDB** (included with Visual Studio)
   - Default connection string targets `.\SQLEXPRESS`. Point `ConnectionStrings:DefaultConnection` at your own instance if it differs.
3. **IDE / Editor**: Visual Studio 2022 / 2025, Visual Studio Code, or JetBrains Rider.

---

## Getting Started

### 1. Database Setup

You have two simple options to set up the database:

#### Option A: Automatic Setup (Default)
When you run the application via `dotnet run`, the built-in `DatabaseInitializer` automatically checks if the `NasaApodDb` database and `ApodEntries` table exist on the configured SQL Server instance. If not, it creates them automatically.

#### Option B: Manual SQL Execution (SQL Server Management Studio or sqlcmd)
Execute the provided SQL script located at [`Database/CreateDatabase.sql`](Database/CreateDatabase.sql):

```sql
-- Creates database 'NasaApodDb' and table 'ApodEntries' with unique constraint on Date
sqlcmd -S "(localdb)\mssqllocaldb" -i "Database\CreateDatabase.sql"
```

Or open [`Database/CreateDatabase.sql`](Database/CreateDatabase.sql) in **SQL Server Management Studio (SSMS)** or **Azure Data Studio** and execute it.

---

### 2. NASA API Key Configuration

The repository ships with NASA's shared `DEMO_KEY`, so the application runs immediately without any configuration. `DEMO_KEY` is rate-limited (30 requests/hour, 50 requests/day per IP), which is enough for demos but shared with everyone else.

#### Getting a Free Personal API Key (Recommended):
1. Visit [https://api.nasa.gov](https://api.nasa.gov).
2. Fill out the "Generate API Key" form (First Name, Last Name, Email).
3. Copy your personal API key.

#### Setting the API Key:
**Never commit a personal API key.** Store it in the local developer secret store, which lives outside the project folder and is never added to Git:

```powershell
cd "c:\Users\User\Desktop\NASA_APOD_Gallery"
dotnet user-secrets set "NasaApi:ApiKey" "YOUR_NASA_API_KEY_HERE"
```

Verify it was saved (values are masked):
```powershell
dotnet user-secrets list
```

The project already carries a `UserSecretsId` in [NasaApodGallery.csproj](NasaApodGallery.csproj), so `dotnet run` in Development picks the secret up automatically. If you prefer an environment variable instead, it works without secrets:

```powershell
$env:NasaApi__ApiKey = "YOUR_NASA_API_KEY_HERE"
```

To change the endpoint or fall back to the shared demo key:

```powershell
dotnet user-secrets set "NasaApi:ApiKey" "DEMO_KEY"
```

`appsettings.json` holds only the placeholder and the base URL:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=NasaApodDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;"
  },
  "NasaApi": {
    "ApiKey": "DEMO_KEY",
    "BaseUrl": "https://api.nasa.gov/planetary/apod"
  }
}
```

---

### 3. Running the Application

1. Open your terminal in the project directory:
   ```powershell
   cd "c:\Users\User\Desktop\NASA_APOD_Gallery"
   ```

2. Restore packages and build:
   ```powershell
   dotnet build
   ```

3. Run the application:
   ```powershell
   dotnet run
   ```

4. Open your web browser and navigate to the displayed URL:
   ```
   http://localhost:5044   (or https://localhost:7120)
   ```

---

## Project Structure

```
├── Controllers/
│   └── HomeController.cs             # Index (Gallery), Fetch, QuickFetch, Details, ImageProxy, Privacy
├── DTOs/
│   └── NasaApodDto.cs                # JSON deserialization model matching NASA API properties
├── Models/
│   ├── ApodEntry.cs                  # Domain model mapping SQL database table
│   ├── GalleryViewModel.cs           # View model with filtering, search, and entries
│   ├── FetchRequestViewModel.cs      # Date range request model with validation
│   └── ErrorViewModel.cs             # Error view model
├── Data/
│   ├── IApodRepository.cs            # ADO.NET repository interface
│   ├── ApodRepository.cs             # ADO.NET implementation (SqlConnection, SqlCommand, SqlParameter)
│   └── DatabaseInitializer.cs       # Database & table auto-provisioning
├── Database/
│   └── CreateDatabase.sql            # Idempotent SQL script to create DB, table, and indexes
├── Services/
│   ├── INasaApodService.cs           # NASA API service interface
│   └── NasaApodService.cs            # HttpClient implementation with JSON array deserialization
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml              # Main responsive gallery & fetch control panel
│   │   ├── Details.cshtml            # Full-resolution APOD media details view
│   │   ├── Privacy.cshtml            # Architecture & about page
│   │   └── _ApodCard.cshtml          # Reusable responsive APOD card component
│   └── Shared/
│       ├── _Layout.cshtml            # Space-themed layout with navigation and flash alerts
│       └── _ValidationScriptsPartial.cshtml
├── wwwroot/
│   ├── css/site.css                  # Custom cosmic dark theme styling and card hover effects
│   └── js/site.js                    # Client-side dynamic modal preview & preset range logic
├── appsettings.json                  # Database connection string and NASA API configuration
├── Program.cs                        # DI container, HTTP client registration, pipeline config
└── NasaApodGallery.csproj            # Project configuration and package references
```

---

## Implementation Details

### NASA APOD API Consumption & DTOs
NASA's endpoint is consumed using `IHttpClientFactory` configured in `Program.cs`:
- Query: `https://api.nasa.gov/planetary/apod?api_key={key}&start_date={start}&end_date={end}&thumbs=true`
- The `thumbs=true` query parameter enables thumbnail generation for videos (YouTube/Vimeo).
- The JSON response array is deserialized into `List<NasaApodDto>` using `System.Text.Json.JsonSerializer`.

### ADO.NET Data Access Layer
All database interactions follow strict ADO.NET guidelines using `Microsoft.Data.SqlClient`:
- **No ORM**: Neither Entity Framework nor Dapper is used; all operations use raw SQL commands.
- **Parameterized Queries**: Every SQL command uses strongly-typed `SqlParameter` objects (e.g. `SqlDbType.Date`, `SqlDbType.NVarChar`) to guard against SQL injection.
- **Mapping**: `SqlDataReader` reads rows directly into `ApodEntry` instances.

### Duplicate Prevention
The whole fetched batch is written inside a single ADO.NET transaction, and each entry uses one
insert-and-guard statement:

```sql
INSERT INTO [dbo].[ApodEntries] (
    [Date], [Title], [Explanation], [Url], [HdUrl],
    [MediaType], [ServiceVersion], [Copyright], [ThumbnailUrl], [SavedAt]
)
SELECT @Date, @Title, @Explanation, @Url, @HdUrl,
       @MediaType, @ServiceVersion, @Copyright, @ThumbnailUrl, SYSUTCDATETIME()
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[ApodEntries] WITH (UPDLOCK, HOLDLOCK) WHERE [Date] = @Date
);
```

- `UPDLOCK, HOLDLOCK` makes the existence check and the insert one atomic critical section, so two
  concurrent fetches of the same date cannot both pass the check.
- Rows-affected from the single statement is the insert/skip signal (`1` = inserted, `0` = duplicate).
- If a violation still slips through, `SqlException` numbers 2601/2627 are caught per row and counted as a
  duplicate rather than failing the batch; only the offending statement rolls back.
- The database-level guarantee remains the unique index:
  `CONSTRAINT [UQ_ApodEntries_Date] UNIQUE NONCLUSTERED ([Date] ASC)`.

### Image Proxy
NASA's image servers apply hotlink protection, so gallery thumbnails are streamed through
`/Home/ImageProxy?url={origin}`. The endpoint reuses a named `IHttpClientFactory` client (`ApodImageProxy`)
instead of creating a socket per request, follows redirects manually so every hop is re-validated, serves
only `image/*` content types up to 25 MB, and refuses loopback, private, link-local, and reserved
addresses so it cannot be used to reach the host network.

### Responsive Gallery View
- Modern CSS grid with Bootstrap breakpoints (`col-12 col-sm-6 col-md-4 col-lg-3`).
- Cards display thumbnail images with uniform aspect ratios (`object-fit: cover`).
- Interactive preview modal allows inspection of explanation and full media without navigating away.
- Dedicated Details view (`/Home/Details/{id}`) provides full-screen layout, HD download links, and embedded video playback.

---

## Database Schema

```sql
CREATE TABLE [dbo].[ApodEntries] (
    [Id]             INT IDENTITY(1,1) NOT NULL,
    [Date]           DATE NOT NULL,
    [Title]          NVARCHAR(500) NOT NULL,
    [Explanation]    NVARCHAR(MAX) NULL,
    [Url]            NVARCHAR(2048) NOT NULL,
    [HdUrl]          NVARCHAR(2048) NULL,
    [MediaType]      NVARCHAR(50) NOT NULL,
    [ServiceVersion] NVARCHAR(50) NULL,
    [Copyright]      NVARCHAR(255) NULL,
    [ThumbnailUrl]   NVARCHAR(2048) NULL,
    [SavedAt]        DATETIME2 NOT NULL CONSTRAINT [DF_ApodEntries_SavedAt] DEFAULT (SYSUTCDATETIME()),

    CONSTRAINT [PK_ApodEntries] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [UQ_ApodEntries_Date] UNIQUE NONCLUSTERED ([Date] ASC)
);

CREATE NONCLUSTERED INDEX [IX_ApodEntries_Date]
ON [dbo].[ApodEntries] ([Date] DESC);
```

---

## Submission Checklist

- [x] ASP.NET MVC project structure (Controllers, Models, Views, DTOs, Services, Data).
- [x] Consumes NASA APOD endpoint with date range and JSON array deserialization.
- [x] DTO classes matching APOD response properties.
- [x] SQL Server database script (`Database/CreateDatabase.sql`).
- [x] Stores API response using ADO.NET (`SqlConnection`, `SqlCommand`, `SqlParameter`).
- [x] Avoids duplicate records on re-fetching.
- [x] Retrieves records using ADO.NET `SqlDataReader` and converts to domain models.
- [x] Responsive gallery layout on Home page showing image and title.
- [x] Git repository initialized with clear commit history.
- [x] Comprehensive documentation and setup instructions.

