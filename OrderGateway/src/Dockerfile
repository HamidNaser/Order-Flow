# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/aspnet:8.0-bookworm-slim AS base
WORKDIR /app
EXPOSE 8080


# New Relic installation:
# https://docs.newrelic.com/install/dotnet/?deployment=linux&docker=yesDocker

# Install the agent
RUN --mount=type=secret,id=corpca,required=false \
  apt-get update && apt-get install -y wget ca-certificates gnupg \
  && if [ -f /run/secrets/corpca ]; then cp /run/secrets/corpca /usr/local/share/ca-certificates/corp-root-ca.crt && update-ca-certificates; fi \
  && echo 'deb http://apt.newrelic.com/debian/ newrelic non-free' | tee /etc/apt/sources.list.d/newrelic.list \
  && wget https://download.newrelic.com/548C16BF.gpg \
  && apt-key add 548C16BF.gpg \
  && apt-get update \
  && apt-get install -y newrelic-dotnet-agent \
  && rm -rf /var/lib/apt/lists/*

# Enable the agent
# Following env vars are required, but will provided by the ECS instance host:
#  NEW_RELIC_LICENSE_KEY=YOUR_LICENSE_KEY
#  NEW_RELIC_APP_NAME=YOUR_APP_NAME
ENV CORECLR_ENABLE_PROFILING=1 \
  CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
  CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
  CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so


FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0-bookworm-slim AS build
ARG TARGETARCH
WORKDIR /src
COPY . .
RUN --mount=type=secret,id=corpca,required=false \
  if [ -f /run/secrets/corpca ]; then cp /run/secrets/corpca /usr/local/share/ca-certificates/corp-root-ca.crt && update-ca-certificates; fi
RUN --mount=type=secret,id=nugetconfig,required=false \
  mkdir -p /root/.nuget/NuGet \
  && if [ -f /run/secrets/nugetconfig ]; then \
       cp /run/secrets/nugetconfig /root/.nuget/NuGet/NuGet.Config; \
       sed -i '/Microsoft Visual Studio Offline Packages/d' /root/.nuget/NuGet/NuGet.Config; \
       sed -i '/<fallbackPackageFolders>/,/<\/fallbackPackageFolders>/d' /root/.nuget/NuGet/NuGet.Config; \
     fi
RUN dotnet test OrderGateway.UnitTests/OrderGateway.UnitTests.csproj
RUN dotnet publish OrderGateway.Api/OrderGateway.Api.csproj -a $TARGETARCH -c Release -o /app /p:UseAppHost=false

FROM base AS final
COPY --from=build /app .
USER $APP_UID
ENTRYPOINT ["dotnet", "OrderGateway.Api.dll"]
