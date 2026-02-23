# Use the official .NET SDK image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/sdk:10.0.102-alpine3.23@sha256:d17d8d6511aee3dd54b4d9e8e03d867b1c3df28fb518ee9dd69e50305b7af4ee AS build

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
FROM mcr.microsoft.com/dotnet/aspnet:10.0.2-alpine3.23@sha256:8e21337e482e353c958681789872b3451e966e07c259b9a6f9a8b7902749a785 AS final
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
