# Use the official .NET 8 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Services/ERP.FinanceService/ERP.FinanceService.csproj", "src/Services/ERP.FinanceService/"]
RUN dotnet restore "src/Services/ERP.FinanceService/ERP.FinanceService.csproj"
COPY . .
WORKDIR "/src/src/Services/ERP.FinanceService"
RUN dotnet build "ERP.FinanceService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ERP.FinanceService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ERP.FinanceService.dll"]
