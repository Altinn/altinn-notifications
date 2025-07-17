# Use the official .NET SDK image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/sdk:9.0.302-alpine3.22@sha256:329088c6945f917af6492ebe58b74c9720e1cf7e8e2bcbf62e04ab3232e18e7c AS build

# Set the working directory in the container
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY /src/Altinn.Notifications.Email/*.csproj ./src/Altinn.Notifications.Email/
COPY /src/Altinn.Notifications.Email.Core/*.csproj ./src/Altinn.Notifications.Email.Core/
COPY /src/Altinn.Notifications.Email.Integrations/*.csproj ./src/Altinn.Notifications.Email.Integrations/

RUN dotnet restore ./src/Altinn.Notifications.Email/Altinn.Notifications.Email.csproj

# Copy the remaining source code and build the application
COPY /src ./src
RUN dotnet publish -c Release -o out ./src/Altinn.Notifications.Email/Altinn.Notifications.Email.csproj


# Use the official .NET runtime image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0.7-alpine3.22@sha256:89a7a398c5acaa773642cfabd6456f33e29687c1529abfaf068929ff9991cb66 AS final
EXPOSE 5091
WORKDIR /app
COPY --from=build /app/out ./

# setup the user and group
# the user will have no password, using shell /bin/false and using the group dotnet
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
# update permissions of files if neccessary before becoming dotnet user
USER dotnet
RUN mkdir /tmp/logtelemetry

# Run the application
ENTRYPOINT [ "dotnet", "Altinn.Notifications.Email.dll" ]
