FROM mcr.microsoft.com/dotnet/sdk:7.0.203-alpine3.17 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/Altinn.Notifications/*.csproj ./src/Altinn.Notifications/
COPY src/Altinn.Notifications.Core/*.csproj ./src/Altinn.Notifications.Core/
COPY src/Altinn.Notifications.Integrations/*.csproj ./src/Altinn.Notifications.Integrations/
COPY src/Altinn.Notifications.Persistence/*.csproj ./src/Altinn.Notifications.Persistence/
RUN dotnet restore ./src/Altinn.Notifications/Altinn.Notifications.csproj

# Copy everything else and build
COPY src ./src
RUN dotnet publish -c Release -o out ./src/Altinn.Notifications/Altinn.Notifications.csproj

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0.5-alpine3.17 AS final
WORKDIR /app
COPY --from=build /app/out .
COPY src/Altinn.Notifications.Persistence/Migration ./Migration
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
USER dotnet
ENTRYPOINT [ "dotnet", "Altinn.Notifications.dll" ]
