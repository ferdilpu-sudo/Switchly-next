# Multi-stage Dockerfile (native linux/arm64 for Oracle Cloud Ampere A1 and linux/amd64)
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY Directory.Packages.props ./
COPY Switchly.LicenseApi/Switchly.LicenseApi.csproj ./Switchly.LicenseApi/
RUN dotnet restore ./Switchly.LicenseApi/Switchly.LicenseApi.csproj

COPY Switchly.LicenseApi/ ./Switchly.LicenseApi/
RUN dotnet publish ./Switchly.LicenseApi/Switchly.LicenseApi.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Run as non-root user
RUN adduser -D -H -u 10001 appuser
USER appuser

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Switchly.LicenseApi.dll"]
