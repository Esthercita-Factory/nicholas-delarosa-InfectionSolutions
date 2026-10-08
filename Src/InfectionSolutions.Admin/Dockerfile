# Panel administrativo (ASP.NET Core MVC). Se construye desde la raiz de la solucion:
#   docker build -f Src/InfectionSolutions.Admin/Dockerfile -t infectionsolutions-admin .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primero solo los .csproj: la restauracion queda en cache mientras no cambien las dependencias.
COPY Src/InfectionSolutions.Domain/InfectionSolutions.Domain.csproj Src/InfectionSolutions.Domain/
COPY Src/InfectionSolutions.Application/InfectionSolutions.Application.csproj Src/InfectionSolutions.Application/
COPY Src/InfectionSolutions.Infrastructure/InfectionSolutions.Infrastructure.csproj Src/InfectionSolutions.Infrastructure/
COPY Src/InfectionSolutions.Admin/InfectionSolutions.Admin.csproj Src/InfectionSolutions.Admin/
RUN dotnet restore Src/InfectionSolutions.Admin/InfectionSolutions.Admin.csproj

COPY Src/ Src/
RUN dotnet publish Src/InfectionSolutions.Admin/InfectionSolutions.Admin.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    UseHttpsRedirection=false
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "InfectionSolutions.Admin.dll"]
