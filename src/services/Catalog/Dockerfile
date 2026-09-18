FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/services/Catalog/Catalog.csproj", "src/services/Catalog/"]
COPY ["src/shared/Contracts/Contracts.csproj", "src/shared/Contracts/"]
COPY ["src/shared/Messaging/Messaging.csproj", "src/shared/Messaging/"]
RUN dotnet restore "src/services/Catalog/Catalog.csproj"
COPY . .
WORKDIR "/src/src/services/Catalog"
RUN dotnet build "./Catalog.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Catalog.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Catalog.dll"]
