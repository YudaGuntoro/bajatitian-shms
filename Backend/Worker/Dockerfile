FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Worker/Worker.csproj", "Worker/"]
COPY ["SHMS.Storage/SHMS.Storage.csproj", "SHMS.Storage/"]
RUN dotnet restore "Worker/Worker.csproj"

COPY . .
RUN dotnet publish "Worker/Worker.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Worker.dll"]
