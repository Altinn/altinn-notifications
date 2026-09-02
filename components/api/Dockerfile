FROM mcr.microsoft.com/dotnet/sdk:10.0.301-alpine3.23@sha256:940f919ae84dd92ccd4aab7686fa5b777870b006c9360351039e16bcaad73d89 AS build

WORKDIR /app

# CPM / build configuration
COPY Directory.Packages.props ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./

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

RUN dotnet publish -c Release -o out ./components/api/src/Altinn.Notifications/Altinn.Notifications.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.11-alpine3.23@sha256:4d5339ac9814f1a033a09e664bcf159e9fb386c89b6e7917b3dca7254e656027 AS final

WORKDIR /app

EXPOSE 5090

# Installing package for time zone functionality
RUN apk add --no-cache tzdata

COPY --from=build /app/out .
COPY --from=build /app/components/api/src/Altinn.Notifications.Persistence/Migration ./Migration
COPY components/api/src/Altinn.Notifications/Views ./Views

RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
RUN chmod 777 /app

USER dotnet

RUN mkdir /tmp/logtelemetry
RUN chmod 777 /tmp/logtelemetry

ENTRYPOINT [ "dotnet", "Altinn.Notifications.dll" ]