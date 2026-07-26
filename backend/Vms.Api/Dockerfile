FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src

COPY Vms.Api/Vms.Api.csproj Vms.Api/
RUN dotnet restore Vms.Api/Vms.Api.csproj

COPY Vms.Api/ Vms.Api/
RUN dotnet publish Vms.Api/Vms.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0.10 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ffmpeg libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /var/lib/vms/recordings \
    && chown $APP_UID:$APP_UID /var/lib/vms/recordings
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV HEALTH_CHECK_URL=http://127.0.0.1:8080/health
EXPOSE 8080

USER $APP_UID
ENTRYPOINT ["dotnet", "Vms.Api.dll"]
