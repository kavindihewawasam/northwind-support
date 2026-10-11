# syntax=docker/dockerfile:1

# ---- build: the .NET SDK compiles and publishes ----------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore layer. Only the files that decide *what* gets restored are copied first (SDK pin, shared
# build settings, package versions and the project files), so this layer is reused until one of them
# changes, and editing a .cs file does not trigger a new restore.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY apps/api/src/SupportDesk.Domain/SupportDesk.Domain.csproj apps/api/src/SupportDesk.Domain/
COPY apps/api/src/SupportDesk.Application/SupportDesk.Application.csproj apps/api/src/SupportDesk.Application/
COPY apps/api/src/SupportDesk.Infrastructure/SupportDesk.Infrastructure.csproj apps/api/src/SupportDesk.Infrastructure/
COPY apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj apps/api/src/SupportDesk.Presentation/
RUN dotnet restore apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj

# Now the source. A change here only rebuilds from this point on.
COPY .editorconfig ./
COPY apps/api/src apps/api/src
RUN dotnet publish apps/api/src/SupportDesk.Presentation/SupportDesk.Presentation.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- run: only the ASP.NET runtime and the published output ----------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Plain HTTP on 8080 (no certificates in the image) as the image's built-in non-root user.
# Nothing else is baked in: the connection string, signing key and environment name arrive as
# environment variables at run time (see docker-compose.yml and .env.example).
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "SupportDesk.Presentation.dll"]