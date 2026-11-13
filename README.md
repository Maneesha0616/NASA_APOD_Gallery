# NASA APOD Gallery

A web application built using **ASP.NET Core MVC, C#, and SQL Server** that integrates with NASA's Astronomy Picture of the Day (APOD) API to retrieve, store, and display astronomy pictures in a responsive gallery.

## Project Overview

The NASA APOD Gallery allows users to explore astronomy pictures provided by NASA. Users can fetch pictures for a selected date range, store the retrieved information in a SQL Server database, and browse previously saved pictures through a gallery interface.

The application uses ADO.NET (SqlClient) to perform database operations, including inserting and retrieving records.

## Features

- **NASA APOD API Integration** – Fetch astronomy pictures and metadata directly from NASA.
- **Date Range Selection** – Retrieve pictures based on selected start and end dates.
- **SQL Server Database** – Store NASA picture information in a database.
- **Duplicate Prevention** – Avoid inserting existing APOD records.
- **ADO.NET Database Operations** – Use SqlConnection, SqlCommand, SqlParameter, and SQL queries.
- **Saved Picture Gallery** – Display previously stored astronomy pictures.
- **Search Functionality** – Search pictures by title or description.
- **Sorting** – Sort saved pictures by newest or oldest date.
- **Responsive Interface** – Browse the gallery through a modern, user-friendly cosmic interface.

##  Technologies Used

| Technology | Purpose |
|---|---|
| C# | Backend programming language |
| ASP.NET Core MVC | Web application framework |
| HTML5 | Web page structure |
| CSS3 | Styling and layout |
| JavaScript | Frontend interactions |
| SQL Server | Database management |
| ADO.NET (SqlClient) | Database connectivity and operations |
| NASA APOD API | Astronomy picture data source |

##  NASA APOD API

This application uses NASA's Astronomy Picture of the Day API.

**API Endpoint:**

`https://api.nasa.gov/planetary/apod`

**API Documentation:** https://api.nasa.gov/

An API key is required to access the service. You can generate a free API key through NASA's API website.

## Prerequisites

Before running the application, make sure you have installed:

- .NET SDK (v8.0 / v10.0)
- Visual Studio, Visual Studio Code, or JetBrains Rider
- SQL Server or SQL Server Express
- SQL Server Management Studio (SSMS)

## Installation and Setup

### 1. Clone the Repository

Open a terminal and run:

```bash
git clone https://github.com/Maneesha0616/NASA_APOD_Gallery.git
```

Navigate to the project folder:

```bash
cd NASA_APOD_Gallery
```

### 2. Configure the NASA API Key

1. Visit [https://api.nasa.gov/](https://api.nasa.gov/).
2. Generate your free NASA API key.
3. Configure the API key in `appsettings.json` under `NasaApi:ApiKey` (or use the built-in `DEMO_KEY`).

### 3. Configure SQL Server Database

1. Open SQL Server Management Studio (SSMS).
2. Connect to your SQL Server instance (e.g. `.\SQLEXPRESS`).
3. Execute the provided script located at `Database/CreateDatabase.sql`.
4. Update the connection string in `appsettings.json` if your SQL Server instance name differs.

### 4. Restore Dependencies

Run the following command:

```bash
dotnet restore
```

### 5. Run the Application

Run the application using:

```bash
dotnet run
```

Open the local URL displayed in the terminal (default: `http://localhost:5044`) to access the NASA APOD Gallery.

## Database Information

The application stores NASA APOD information in SQL Server.

The `dbo.ApodEntries` database table includes the following columns:

| Column | Data Type | Description |
|---|---|---|
| Id | INT IDENTITY | Primary key |
| Date | DATE | Date of the astronomy picture (Unique) |
| Title | NVARCHAR(500) | Title of the picture |
| Explanation | NVARCHAR(MAX) | Scientific description of the picture |
| Url | NVARCHAR(2048) | Direct image URL |
| HdUrl | NVARCHAR(2048) | High-definition image URL |
| MediaType | NVARCHAR(50) | Type of media (image) |
| ServiceVersion | NVARCHAR(50) | NASA API service version |
| Copyright | NVARCHAR(255) | Image copyright / credit |
| SavedAt | DATETIME2 | Date and time the record was saved |

Database operations are implemented using pure **ADO.NET (`Microsoft.Data.SqlClient`)** with parameterized queries.

## Project Submission

This repository contains:

- ASP.NET Core MVC application source code.
- SQL Server database creation script (`Database/CreateDatabase.sql`).
- Application configuration instructions.
- Instructions for setting up the NASA API key.



