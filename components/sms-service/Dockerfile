#Use the official .NET SDK image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/sdk:10.0.202-alpine3.23@sha256:732cd42c6f659814c9804ad7b05c7f761e83ef8379c5b2fdc3af673353caff73 AS build

# Set the working directory in the container
WORKDIR /app

# Copy csproj and restore as distinct layers
# Paths mirror the repo structure so cross-component project references resolve correctly
COPY components/sms-service/src/Altinn.Notifications.Sms/*.csproj ./components/sms-service/src/Altinn.Notifications.Sms/
COPY components/sms-service/src/Altinn.Notifications.Sms.Core/*.csproj ./components/sms-service/src/Altinn.Notifications.Sms.Core/
COPY components/sms-service/src/Altinn.Notifications.Sms.Integrations/*.csproj ./components/sms-service/src/Altinn.Notifications.Sms.Integrations/
COPY components/shared/src/Altinn.Notifications.Shared/*.csproj ./components/shared/src/Altinn.Notifications.Shared/

RUN dotnet restore ./components/sms-service/src/Altinn.Notifications.Sms/Altinn.Notifications.Sms.csproj

# Copy the remaining source code and build the application
COPY components/sms-service/src ./components/sms-service/src
COPY components/shared/src ./components/shared/src
RUN dotnet publish -c Release -o out \
    ./components/sms-service/src/Altinn.Notifications.Sms/Altinn.Notifications.Sms.csproj


# Use the official .NET runtime image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.6-alpine3.23@sha256:2a80ec9b84fed7ede84708e3e366a56b9790bbf7978e875a31bc01ae80689534 AS final
EXPOSE 5092
WORKDIR /app
COPY --from=build /app/out ./

# setup the user and group
# the user will have no password, using shell /bin/false and using the group dotnet
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
# update permissions of files if necessary before becoming dotnet user
USER dotnet
RUN mkdir /tmp/logtelemetry

# Run the application
ENTRYPOINT [ "dotnet", "Altinn.Notifications.Sms.dll" ]
