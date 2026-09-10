FROM mcr.microsoft.com/dotnet/sdk:10.0.401-alpine3.23@sha256:84c5d09dfeb4275f6540084e69331f140223eb968b5ef48023923b0b2eda156b AS build

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
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-alpine3.23@sha256:68eeb5922e4737a0f4657e3a631e566c38186f910c7155fb7c377414ce58a64d AS final

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