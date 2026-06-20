# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the entire solution directory
COPY . .

# Restore dependencies for the WebApi project
RUN dotnet restore "src/host/Nexora.WebApi/Nexora.WebApi.csproj"

# Build and publish release files
RUN dotnet publish "src/host/Nexora.WebApi/Nexora.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose port 8080 (Render's default mapped port)
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Nexora.WebApi.dll"]
