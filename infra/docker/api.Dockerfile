# API .NET 10. Se construye para linux/amd64 y linux/arm64 (Oracle ARM y Azure x86).
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
COPY apps/api/global.json apps/api/Directory.Build.props apps/api/.editorconfig apps/api/AulaLista.sln ./
COPY apps/api/src/AulaLista.Api/AulaLista.Api.csproj src/AulaLista.Api/
COPY apps/api/src/AulaLista.Application/AulaLista.Application.csproj src/AulaLista.Application/
COPY apps/api/src/AulaLista.Domain/AulaLista.Domain.csproj src/AulaLista.Domain/
COPY apps/api/src/AulaLista.Infrastructure/AulaLista.Infrastructure.csproj src/AulaLista.Infrastructure/
RUN dotnet restore src/AulaLista.Api/AulaLista.Api.csproj -a $TARGETARCH
COPY apps/api/src/ src/
RUN dotnet publish src/AulaLista.Api/AulaLista.Api.csproj -c Release -a $TARGETARCH --no-restore \
    -p:OpenApiGenerateDocuments=false -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
# Almacén local de archivos (Almacen__Ruta). El volumen hereda este dueño al montarse por primera vez.
RUN mkdir -p /var/lib/aula-lista/archivos && chown $APP_UID /var/lib/aula-lista/archivos
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "AulaLista.Api.dll"]
