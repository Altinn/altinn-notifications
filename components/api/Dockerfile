FROM mcr.microsoft.com/dotnet/sdk:10.0.102-alpine3.23@sha256:d17d8d6511aee3dd54b4d9e8e03d867b1c3df28fb518ee9dd69e50305b7af4ee AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
# Paths mirror the repo structure so cross-component project references resolve correctly
COPY components/api/src/Altinn.Notifications/*.csproj ./components/api/src/Altinn.Notifications/
COPY components/api/src/Altinn.Notifications.Core/*.csproj ./components/api/src/Altinn.Notifications.Core/
COPY components/api/src/Altinn.Notifications.Integrations/*.csproj ./components/api/src/Altinn.Notifications.Integrations/
COPY components/api/src/Altinn.Notifications.Persistence/*.csproj ./components/api/src/Altinn.Notifications.Persistence/
COPY components/shared/src/Altinn.Notifications.Shared/*.csproj ./components/shared/src/Altinn.Notifications.Shared/
RUN dotnet restore ./components/api/src/Altinn.Notifications/Altinn.Notifications.csproj

# Copy everything else and build
COPY components/api/src ./components/api/src
COPY components/shared/src ./components/shared/src
RUN dotnet build ./components/api/src/DbTools/DbTools.csproj -c Release -o /app_tools
RUN dotnet publish -c Release -o out ./components/api/src/Altinn.Notifications/Altinn.Notifications.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-alpine3.23@sha256:8e21337e482e353c958681789872b3451e966e07c259b9a6f9a8b7902749a785 AS final
WORKDIR /app
EXPOSE 5090

# Installing package for time zone functionality
RUN apk add --no-cache tzdata

COPY --from=build /app/out .
COPY --from=build /app/components/api/src/Altinn.Notifications.Persistence/Migration ./Migration
COPY components/api/src/Altinn.Notifications/Views ./Views

RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT [ "dotnet", "Altinn.Notifications.dll" ]
