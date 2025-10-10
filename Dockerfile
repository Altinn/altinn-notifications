FROM mcr.microsoft.com/dotnet/sdk:9.0.305-alpine3.22@sha256:306ad935d9543becc91b59b61c20e6fecba6faed34e975fbcb378caa185e8b85 AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:9.0.9-alpine3.22@sha256:d2bef2c7ecb618e02c5e2f29b448be0aca1f82993800066c3622e7cabfca9ead AS final
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
