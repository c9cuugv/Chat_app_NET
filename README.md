# Chat Application (.NET 8)

A real-time chat application built with modern .NET technologies, following Clean Architecture principles.

## Technologies Used

### Backend
- **Framework**: [.NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) (ASP.NET Core Web API)
- **Language**: C# 12
- **Real-time Communication**: SignalR
- **Database**: PostgreSQL (accessed via Entity Framework Core)
- **Caching & Backplane**: Redis
- **Authentication**: JWT (JSON Web Tokens)
- **Logging**: Serilog

### Architecture
- **Clean Architecture**: Separated into API, Core (Domain), and Infrastructure layers.
- **Microservices-ready**: Designed with containerization in mind.

### DevOps & Tools
- **Containerization**: Docker & Docker Compose
- **Documentation**: Swagger / OpenAPI

## Project Structure

- **`src/ChatApp.Api`**: The entry point of the application. Contains Controllers, SignalR Hubs, and Dependency Injection setup.
- **`src/ChatApp.Core`**: The domain layer. Contains Entities, Interfaces, and DTOs. Dependencies flow *inward* to this project.
- **`src/ChatApp.Infrastructure`**: Implementation details. Contains EF Core DbContext, Repositories, and Redis configuration.

## Getting Started

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) installed.
- [Docker Desktop](https://www.docker.com/products/docker-desktop) installed and running.

### 1. Start Infrastructure
Use Docker Compose to spin up the required databases (PostgreSQL and Redis).

```bash
docker-compose up -d
```

This will run:
- **PostgreSQL** on port `5432` (User: `admin`, DB: `chatapp`)
- **Redis** on port `6379`

### 2. Configuration
Check `appsettings.json` in `ChatApp.Api` to ensure connection strings match your Docker setup.

### 3. Run the Application
You can run the application using the .NET CLI:

```bash
dotnet run --project src/ChatApp.Api
```

Or open `ChatApp.slnx` in Visual Studio / JetBrains Rider and run the `ChatApp.Api` project.

## API Documentation
Once running, you can access the Swagger UI to interact with the API endpoints:
- **URL**: `http://localhost:5000/swagger` (or the port specified in your launch profile)
