# Use the official .NET 10 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Services/Playground.WeatherService/Playground.WeatherService.csproj", "src/Services/Playground.WeatherService/"]
RUN dotnet restore "src/Services/Playground.WeatherService/Playground.WeatherService.csproj"
COPY . .
WORKDIR "/src/src/Services/Playground.WeatherService"
RUN dotnet build "Playground.WeatherService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Playground.WeatherService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Playground.WeatherService.dll"]
