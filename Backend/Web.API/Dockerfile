FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Web.API/Web.API.csproj", "Web.API/"]
COPY ["Web.API.Domain/Web.API.Domain.csproj", "Web.API.Domain/"]
COPY ["Web.API.Persistence/Web.API.Persistence.csproj", "Web.API.Persistence/"]
COPY ["SHMS.Storage/SHMS.Storage.csproj", "SHMS.Storage/"]
RUN dotnet restore "Web.API/Web.API.csproj"
COPY . .
RUN dotnet publish "Web.API/Web.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Web.API.dll"]
