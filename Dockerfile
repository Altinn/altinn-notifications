FROM mcr.microsoft.com/dotnet/sdk:9.0.307-alpine3.22@sha256:512f8347b0d2f9848f099a8c31be07286955ceea337cadb1114057ed0b15862f AS build
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
FROM mcr.microsoft.com/dotnet/aspnet:10.0.0-alpine3.22@sha256:049f2d7d7acfcbf09e1d15eb4faccec6453b0a98f0cb54d53bcbdc3ed91e96c8 AS final
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
