#Use the official .NET SDK image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/sdk:10.0.101-alpine3.23@sha256:9698eeadcdb786aafd10802e9196dd918c2aed7af9a7297a33ae272123d96bc9 AS build

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
FROM mcr.microsoft.com/dotnet/aspnet:10.0.1-alpine3.23@sha256:a126f0550b963264c5493fd9ec5dc887020c7ea7ebe1da09bc10c9f26d16c253 AS final
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
