# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY pdf-service.csproj .
RUN dotnet restore

# Copy source code
COPY . .

# Build application
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy built application from build stage
COPY --from=build /app/publish .

# Copy logo if exists
COPY logo.png ./logo.png

# Expose port
EXPOSE 5038

# Run application
ENTRYPOINT ["dotnet", "pdf-service.dll"]
