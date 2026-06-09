# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY Day3.csproj .
RUN dotnet restore

# Copy everything else (excluding test/day folders via .dockerignore)
COPY . .

# Publish release build
RUN dotnet publish Day3.csproj -c Release -o /app/publish --no-restore

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published output from build stage
COPY --from=build /app/publish .

# ASP.NET Core listens on 8080 inside the container
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Day3.dll"]
