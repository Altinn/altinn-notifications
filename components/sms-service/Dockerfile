#Use the official .NET SDK image with Alpine Linux as a base image
FROM mcr.microsoft.com/dotnet/sdk:9.0.307-alpine3.22@sha256:512f8347b0d2f9848f099a8c31be07286955ceea337cadb1114057ed0b15862f AS build

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
FROM mcr.microsoft.com/dotnet/aspnet:9.0.11-alpine3.22@sha256:be36809e32840cf9fcbf1a3366657c903e460d3c621d6593295a5e5d02268a0d AS final
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
