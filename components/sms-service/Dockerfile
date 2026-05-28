#Use the official .NET SDK image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/sdk:10.0.300-alpine3.23@sha256:5c559aa5d99337e400d39ab4fa1f6979d126c29b20939d53658ed38300571e74 AS build

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
FROM mcr.microsoft.com/dotnet/aspnet:10.0.8-alpine3.23@sha256:1b3a34768687d583ebdf16ca8dd9a21ec93de9cdf81bb424e3c1a706e2a453d7 AS final
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
