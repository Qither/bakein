FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY ["bakein.api/bakein.api.csproj", "bakein.api/"]
RUN dotnet restore "bakein.api/bakein.api.csproj"

COPY . .
WORKDIR "/src/bakein.api"
RUN dotnet publish "bakein.api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "bakein.api.dll"]
