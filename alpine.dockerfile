FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine3.23
LABEL maintainer="Matthew Kelly (Badgerati)"
ENV POWERSHELL_TELEMETRY_OPTOUT=1
# a larger gen0 GC budget, as PowerShell allocates heavily per request (see docs/Hosting/Performance.md)
ENV DOTNET_GCgen0size=0x4000000
RUN mkdir -p /usr/local/share/powershell/Modules/Pode
COPY ./pkg/ /usr/local/share/powershell/Modules/Pode