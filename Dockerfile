# Base image (runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80

# Build image
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["CoffeeShopAPI/CoffeeShopAPI.csproj", "CoffeeShopAPI/"]
RUN dotnet restore "CoffeeShopAPI/CoffeeShopAPI.csproj"
COPY . .
WORKDIR "/src/CoffeeShopAPI"
RUN dotnet build "CoffeeShopAPI.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish image
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "CoffeeShopAPI.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "CoffeeShopAPI.dll"]
