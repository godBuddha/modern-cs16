FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /out .
RUN mkdir -p uploads/kyc
EXPOSE 7000
ENV API_PORT=7000
ENTRYPOINT ["dotnet", "Api.dll"]
