FROM mcr.microsoft.com/dotnet/sdk:10.0.300-alpine3.23@sha256:5c559aa5d99337e400d39ab4fa1f6979d126c29b20939d53658ed38300571e74 AS build
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
RUN dotnet publish -c Release -o out ./components/api/src/Altinn.Notifications/Altinn.Notifications.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.8-alpine3.23@sha256:1e37a8236c558ae31bd6bc8144e38e6036b73cf1b0616fe56d79e60babb9d93b AS final
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
