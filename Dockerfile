FROM mcr.microsoft.com/dotnet/sdk:10.0.101-alpine3.23@sha256:2267f81b6463e39df275f2affa1859471bda67dd6bea884f7d3b7f4eb28c3fd9 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0.1-alpine3.23@sha256:a126f0550b963264c5493fd9ec5dc887020c7ea7ebe1da09bc10c9f26d16c253 AS final
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
