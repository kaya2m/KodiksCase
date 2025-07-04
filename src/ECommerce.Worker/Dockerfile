FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["src/ECommerce.Worker/ECommerce.Worker.csproj", "src/ECommerce.Worker/"]
COPY ["src/ECommerce.Application/ECommerce.Application.csproj", "src/ECommerce.Application/"]
COPY ["src/ECommerce.Core/ECommerce.Core.csproj", "src/ECommerce.Core/"]
COPY ["src/ECommerce.Infrastructure/ECommerce.Infrastructure.csproj", "src/ECommerce.Infrastructure/"]
COPY ["src/ECommerce.Shared/ECommerce.Shared.csproj", "src/ECommerce.Shared/"]

RUN dotnet restore "src/ECommerce.Worker/ECommerce.Worker.csproj"

COPY . .

WORKDIR "/src/src/ECommerce.Worker"
RUN dotnet build "ECommerce.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "ECommerce.Worker.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "ECommerce.Worker.dll"]