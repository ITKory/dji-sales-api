FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["sales-performance-api/sales-performance-api.csproj", "sales-performance-api/"]
RUN dotnet restore "sales-performance-api/sales-performance-api.csproj"
COPY sales-performance-api/ sales-performance-api/
RUN dotnet publish "sales-performance-api/sales-performance-api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
USER root
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "sales-performance-api.dll"]
