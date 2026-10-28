# Demo-only container. Runs the app in Development with its synthetic in-memory data
# (DemoMode). It never connects to SQL Server or SMTP, and its data resets on restart.
# It needs a host that keeps ONE long-running instance (for example Render); see DEPLOY-DEMO.md.
# For real use, deploy with DemoMode=false and SQL Server (see SmaComplaints/README.md).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copy the project files first so Docker can reuse the restore layer.
COPY SmaComplaints/SmaComplaints.csproj SmaComplaints/NuGet.Config SmaComplaints/packages.lock.json SmaComplaints/
RUN dotnet restore SmaComplaints/SmaComplaints.csproj
COPY SmaComplaints/ SmaComplaints/
RUN dotnet publish SmaComplaints/SmaComplaints.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

# Debian-based runtime image (not chiseled): the app needs tzdata for Asia/Kuala_Lumpur,
# and libfontconfig1 for PDF page rendering (SkiaSharp).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends tzdata libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish ./

# DemoMode is only permitted in Development (see Program.cs), so the demo runs as Development.
# The host's PORT variable (Render sets it) overrides the default below; Program.cs binds to it.
ENV ASPNETCORE_ENVIRONMENT=Development \
    DemoMode=true \
    PORT=80 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DataProtection__KeysPath=/tmp/dp-keys \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_NOLOGO=1

ENTRYPOINT ["dotnet", "SmaComplaints.dll"]
