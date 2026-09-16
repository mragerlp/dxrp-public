# Base image for the DXRP Cloud Agent environment.
#
# DXRP is an s&box game (C#). The s&box engine/editor itself is a proprietary
# Windows/Steam application and cannot run in a headless Linux Cloud Agent, so
# this image targets the parts of the repository that are buildable/runnable on
# Linux: the .NET 10 "DXRP Server Launcher" (dxrp-server.cs) and general C#
# tooling. dxrp-server.cs declares `#:property TargetFramework=net10.0`, so the
# .NET 10 SDK is the required stable toolchain.
FROM mcr.microsoft.com/dotnet/sdk:10.0

# git and curl are used by the launcher (repository clone/fetch) and by common
# development workflows. ca-certificates keeps HTTPS calls (e.g. the DXRP API)
# working.
RUN apt-get update \
    && apt-get install -y --no-install-recommends git curl ca-certificates \
    && rm -rf /var/lib/apt/lists/*

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1
