FROM mcr.microsoft.com/dotnet/sdk:10.0.102-alpine3.23@sha256:d17d8d6511aee3dd54b4d9e8e03d867b1c3df28fb518ee9dd69e50305b7af4ee AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/Altinn.Notifications/*.csproj ./src/Altinn.Notifications/
COPY src/Altinn.Notifications.Core/*.csproj ./src/Altinn.Notifications.Core/
COPY src/Altinn.Notifications.Integrations/*.csproj ./src/Altinn.Notifications.Integrations/
COPY src/Altinn.Notifications.Persistence/*.csproj ./src/Altinn.Notifications.Persistence/
RUN dotnet restore ./src/Altinn.Notifications/Altinn.Notifications.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet build ./src/DbTools/DbTools.csproj -c Release -o /app_tools
RUN dotnet publish -c Release -o out ./src/Altinn.Notifications/Altinn.Notifications.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-alpine3.23@sha256:8e21337e482e353c958681789872b3451e966e07c259b9a6f9a8b7902749a785 AS final
WORKDIR /app
EXPOSE 5090

# Installing package for time zone functionality
RUN apk add --no-cache tzdata

COPY --from=build /app/out .
COPY --from=build /app/src/Altinn.Notifications.Persistence/Migration ./Migration
COPY src/Altinn.Notifications/Views ./Views

RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
USER dotnet
RUN mkdir /tmp/logtelemetry

ENTRYPOINT [ "dotnet", "Altinn.Notifications.dll" ]
