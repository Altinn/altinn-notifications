#Use the official .NET SDK image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/sdk:9.0.202-alpine3.21@sha256:2303ad5956875eb82d3c6195e43f0e8e1378a6252869f2d4d200e067130ff5b5 AS build

# Set the working directory in the container
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY /src/Altinn.Notifications.Sms/*.csproj ./src/Altinn.Notifications.Sms/
COPY /src/Altinn.Notifications.Sms.Core/*.csproj ./src/Altinn.Notifications.Sms.Core/
COPY /src/Altinn.Notifications.Sms.Integrations/*.csproj ./src/Altinn.Notifications.Sms.Integrations/

RUN dotnet restore ./src/Altinn.Notifications.Sms/Altinn.Notifications.Sms.csproj

# Copy the remaining source code and build the application
COPY /src ./src
RUN dotnet publish -c Release -o out ./src/Altinn.Notifications.Sms/Altinn.Notifications.Sms.csproj


# Use the official .NET runtime image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0.3-alpine3.21@sha256:374a0ebc32ae59692470070a8bbcdef1186250d446836bf6ec8ac08a5c623667 AS final
EXPOSE 5092
WORKDIR /app
COPY --from=build /app/out ./

# setup the user and group
# the user will have no password, using shell /bin/false and using the group dotnet
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
# update permissions of files if neccessary before becoming dotnet user
USER dotnet
RUN mkdir /tmp/logtelemetry

# Run the application
ENTRYPOINT [ "dotnet", "Altinn.Notifications.Sms.dll" ]
