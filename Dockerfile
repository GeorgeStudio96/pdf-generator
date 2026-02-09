# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project file, package props and restore dependencies
COPY pdf-service.csproj Directory.Packages.props ./
RUN dotnet restore

# Copy source code
COPY . .

# Build application
RUN dotnet publish -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Copy built application from build stage
COPY --from=build /app/publish .

# Copy logo if exists
COPY logo.png ./logo.png

# Expose port
EXPOSE 5038

# Run application
ENTRYPOINT ["dotnet", "pdf-service.dll"]
