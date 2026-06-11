FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/ChatApp.Core/ChatApp.Core.csproj src/ChatApp.Core/
COPY src/ChatApp.Infrastructure/ChatApp.Infrastructure.csproj src/ChatApp.Infrastructure/
COPY src/ChatApp.Api/ChatApp.Api.csproj src/ChatApp.Api/
RUN dotnet restore src/ChatApp.Api/ChatApp.Api.csproj

COPY src/ src/
RUN dotnet publish src/ChatApp.Api/ChatApp.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ChatApp.Api.dll"]
