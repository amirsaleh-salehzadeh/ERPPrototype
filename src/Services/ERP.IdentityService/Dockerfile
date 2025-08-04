# Use the official .NET 10 runtime as a parent image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Services/ERP.IdentityService/ERP.IdentityService.csproj", "src/Services/ERP.IdentityService/"]
COPY ["src/Contracts/ERP.Contracts/ERP.Contracts.csproj", "src/Contracts/ERP.Contracts/"]
RUN dotnet restore "src/Services/ERP.IdentityService/ERP.IdentityService.csproj"
COPY . .
WORKDIR "/src/src/Services/ERP.IdentityService"
RUN dotnet build "ERP.IdentityService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ERP.IdentityService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ERP.IdentityService.dll"]
