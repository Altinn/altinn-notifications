# Use the official .NET SDK image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/sdk:10.0.301-alpine3.23@sha256:940f919ae84dd92ccd4aab7686fa5b777870b006c9360351039e16bcaad73d89 AS build

# Set the working directory in the container
WORKDIR /app

# CPM / build configuration
COPY Directory.Packages.props ./
COPY Directory.Build.props ./

# Copy csproj and restore as distinct layers
# Paths mirror the repo structure so cross-component project references resolve correctly
COPY components/email-service/src/Altinn.Notifications.Email/*.csproj ./components/email-service/src/Altinn.Notifications.Email/
COPY components/email-service/src/Altinn.Notifications.Email.Core/*.csproj ./components/email-service/src/Altinn.Notifications.Email.Core/
COPY components/email-service/src/Altinn.Notifications.Email.Integrations/*.csproj ./components/email-service/src/Altinn.Notifications.Email.Integrations/
COPY components/shared/src/Altinn.Notifications.Shared/*.csproj ./components/shared/src/Altinn.Notifications.Shared/

RUN dotnet restore ./components/email-service/src/Altinn.Notifications.Email/Altinn.Notifications.Email.csproj

# Copy the remaining source code and build the application
COPY components/email-service/src ./components/email-service/src
COPY components/shared/src ./components/shared/src
RUN dotnet publish -c Release -o out \
    ./components/email-service/src/Altinn.Notifications.Email/Altinn.Notifications.Email.csproj

# Use the official .NET runtime image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/aspnet:10.0.9-alpine3.23@sha256:57bd717ac18ff6c8a39cc0ee4a76c1f15adc46df50434c73eff0c3f1df4c88f0 AS final
EXPOSE 5091
WORKDIR /app
COPY --from=build /app/out ./

# setup the user and group
# the user will have no password, using shell /bin/false and using the group dotnet
RUN addgroup -g 3000 dotnet && adduser -u 1000 -G dotnet -D -s /bin/false dotnet
# update permissions of files if necessary before becoming dotnet user
USER dotnet
RUN mkdir /tmp/logtelemetry

# Run the application
ENTRYPOINT [ "dotnet", "Altinn.Notifications.Email.dll" ]
