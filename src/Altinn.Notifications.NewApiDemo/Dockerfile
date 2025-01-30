FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 5151
#EXPOSE 8081
ENV ASPNETCORE_URLS=http://*:5151 
ENV ASPNETCORE_ENVIRONMENT=Development

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Development
WORKDIR /src
COPY ["Altinn.Notifications.NewApiDemo.csproj", "Altinn.Notifications.NewApiDemo/"]
RUN dotnet restore "Altinn.Notifications.NewApiDemo/Altinn.Notifications.NewApiDemo.csproj"

WORKDIR "/src/Altinn.Notifications.NewApiDemo"
COPY . .

RUN dotnet build "Altinn.Notifications.NewApiDemo.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Development
RUN dotnet publish "Altinn.Notifications.NewApiDemo.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
#WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Altinn.Notifications.NewApiDemo.dll"]
