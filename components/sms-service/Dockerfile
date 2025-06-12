#Use the official .NET SDK image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/sdk:9.0.301-alpine3.21@sha256:cec8f5d4537ff29112274379401142fa73d97fcc9f174dc1c623c29dcaef24c1 AS build

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
FROM mcr.microsoft.com/dotnet/aspnet:9.0.6-alpine3.21@sha256:ea72850bd81ba5c95ba88641a4fa315471bef9e3d1cd7e26c2594faff56e3a36 AS final
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
