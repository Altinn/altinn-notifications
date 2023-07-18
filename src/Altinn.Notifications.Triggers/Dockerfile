FROM mcr.microsoft.com/dotnet/sdk:7.0.306-alpine3.18 AS build
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY src/Altinn.Notifications.Triggers/*.csproj .src/Altinn.Notifications.Triggers/
RUN dotnet restore ./src/Altinn.Notifications.Triggers/

# Copy everything else and build
COPY src/Altinn.Notifications.Triggers/ ./src/Altinn.Notifications.Triggers/
RUN dotnet publish -c Release --no-restore -o out ./src/Altinn.Notifications.Triggers/

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:7.0.9-alpine3.18 AS final
WORKDIR /app
EXPOSE 5092

COPY --from=build /app/out .

RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
USER dotnet
ENTRYPOINT [ "dotnet", "Altinn.Notifications.Triggers.dll" ]
