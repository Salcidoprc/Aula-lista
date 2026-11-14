# API .NET 10. Se construye para linux/amd64 y linux/arm64 (Oracle ARM y Azure x86).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY apps/api/global.json apps/api/Directory.Build.props apps/api/.editorconfig apps/api/Aulisima.sln ./
COPY apps/api/src/Aulisima.Api/Aulisima.Api.csproj src/Aulisima.Api/
COPY apps/api/src/Aulisima.Application/Aulisima.Application.csproj src/Aulisima.Application/
COPY apps/api/src/Aulisima.Domain/Aulisima.Domain.csproj src/Aulisima.Domain/
COPY apps/api/src/Aulisima.Infrastructure/Aulisima.Infrastructure.csproj src/Aulisima.Infrastructure/
RUN dotnet restore src/Aulisima.Api/Aulisima.Api.csproj -a $TARGETARCH
COPY apps/api/src/ src/
RUN dotnet publish src/Aulisima.Api/Aulisima.Api.csproj -c Release -a $TARGETARCH --no-restore \
    -p:OpenApiGenerateDocuments=false -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
# Almacén local de archivos (Almacen__Ruta). El volumen hereda este dueño al montarse por primera vez.
RUN mkdir -p /var/lib/aulisima/archivos && chown $APP_UID /var/lib/aulisima/archivos
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Aulisima.Api.dll"]
