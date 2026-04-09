FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj files first (better Docker layer caching)
COPY Api/Api.csproj Api/
COPY Dashboard/Dashboard.csproj Dashboard/

# Restore Dashboard (will pull Api as dependency)
RUN dotnet restore Dashboard/Dashboard.csproj

# Copy all source
COPY Api/ Api/
COPY Dashboard/ Dashboard/

# Publish Dashboard in one shot — disable duplicate-file error
# (Api + Dashboard both have appsettings.json, ErrorOnDuplicatePublishOutputFiles=false suppresses NETSDK1152)
RUN dotnet publish Dashboard/Dashboard.csproj -c Release -o /out \
    -p:ErrorOnDuplicatePublishOutputFiles=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .
EXPOSE 7001
ENV DASHBOARD_PORT=7001
ENTRYPOINT ["dotnet", "Dashboard.dll"]
