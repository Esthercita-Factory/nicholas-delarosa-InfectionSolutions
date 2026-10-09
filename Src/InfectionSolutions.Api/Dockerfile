# API REST (ASP.NET Core + JWT + Swagger). Se construye desde la raiz de la solucion:
#   docker build -f Src/InfectionSolutions.Api/Dockerfile -t infectionsolutions-api .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: la restauracion queda en cache mientras no cambien las dependencias.
COPY Src/InfectionSolutions.Domain/InfectionSolutions.Domain.csproj Src/InfectionSolutions.Domain/
COPY Src/InfectionSolutions.Application/InfectionSolutions.Application.csproj Src/InfectionSolutions.Application/
COPY Src/InfectionSolutions.Infrastructure/InfectionSolutions.Infrastructure.csproj Src/InfectionSolutions.Infrastructure/
COPY Src/InfectionSolutions.Api/InfectionSolutions.Api.csproj Src/InfectionSolutions.Api/
RUN dotnet restore Src/InfectionSolutions.Api/InfectionSolutions.Api.csproj

COPY Src/ Src/
RUN dotnet publish Src/InfectionSolutions.Api/InfectionSolutions.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    Receipts__Path=/app/App_Data/recibos
EXPOSE 8080
COPY --from=build /app/publish .
# Carpeta que la API escribe en tiempo de ejecucion: recibos PDF.
RUN mkdir -p /app/App_Data/recibos && chown -R $APP_UID /app/App_Data
USER $APP_UID
ENTRYPOINT ["dotnet", "InfectionSolutions.Api.dll"]
