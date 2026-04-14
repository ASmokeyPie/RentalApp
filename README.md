---
title: "RentalApp readme"
parent: RentalApp
grand_parent: C# practice
nav_order: 5
mermaid: true
---

# RentalApp

The purpose of this app is to act as a starting point for further development. It provides some
basic features including:

* Database integration and migrations
* Role-based security
* Local authentication
* Example navigation

This version of the app uses PostgreSQL for data storage and Entity Framework Core for object-relational mapping
and migrations.

To fully understand how it works, you should follow an appropriate set of tutorials such as 
[this one](https://edinburgh-napier.github.io/SET09102/tutorials/csharp/) which covers all of the main
concepts and techniques used here. However, if you want to jump straight in and work out any problems
as you go along, that will also work. The code uses structured comments for use with the 
[Doxygen](https://www.doxygen.nl/) documentation generator tool. 

You can use any development environment with this project including

* [Rider](https://www.jetbrains.com/rider/)
* [Visual Studio](https://visualstudio.microsoft.com/)
* [Visual Studio Code](https://code.visualstudio.com/)

The instructions assume you will be using VSCode since that is a lowest-common-denominator choice.

## Compatibility

This app is built using the following tool versions.

| Name                                                                                      | Version     |
|-------------------------------------------------------------------------------------------|-------------|
| [.NET](https://dotnet.microsoft.com/en-us/)                                               | 8.0 / 9.0   |
| [PostgreSQL Docker image](https://hub.docker.com/_/postgres)                              | 16          |


## Getting started

### Prerequisites

Before using this app, ensure you have:

1. **.NET SDK 8.0** or later installed
2. **Docker** installed and running
3. **PostgreSQL container** running (see [dev-environment tutorial](https://edinburgh-napier.github.io/SET09102/tutorials/csharp/dev-environment/))

### Configuration

1. Copy `RentalApp.Database/appsettings.json.template` to `RentalApp.Database/appsettings.json`
2. Update the connection string with your PostgreSQL credentials:
   ```json
   {
     "ConnectionStrings": {
       "DevelopmentConnection": "Host=localhost;Username=student_user;Password=password123;Database=rentalapp"
     }
   }
   ```

### Initial Setup

1. Navigate to the Migrations project and create the initial migration:
   ```bash
   cd RentalApp.Migrations
   dotnet ef migrations add InitialCreate
   ```

2. Apply the migration to create the database:
   ```bash
   dotnet ef database update
   ```

3. Build and run the application:
   ```bash
   cd ../RentalApp
   dotnet build
   dotnet run
   ```

### Tutorial

For a comprehensive guide on using this app and understanding its architecture, see the
[MAUI + MVVM + Database Tutorial](https://edinburgh-napier.github.io/SET09102/tutorials/csharp/maui-mvvm-database/).
