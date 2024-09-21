FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS base
USER $APP_UID
ARG TARGETARCH
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/Bulwark.Auth/Bulwark.Auth.csproj", "src/Bulwark.Auth/"]
COPY ["src/Bulwark.Auth.Core/Bulwark.Auth.Core.csproj", "src/Bulwark.Auth.Core/"]
COPY ["src/Bulwark.Auth.Repositories/Bulwark.Auth.Repositories.csproj", "src/Bulwark.Auth.Repositories/"]
RUN dotnet restore "src/Bulwark.Auth/Bulwark.Auth.csproj"
COPY . .
WORKDIR "/src/src/Bulwark.Auth"
RUN dotnet build -m:1 "Bulwark.Auth.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Bulwark.Auth.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Bulwark.Auth.dll"]