# Pruebas automatizadas (xUnit). Es la puerta del despliegue: docker-compose levanta
# el resto de servicios solo si este contenedor termina con codigo 0.
#   docker build -f Src/InfectionSolutions.Tests/Dockerfile -t infectionsolutions-tests .
#   docker run --rm infectionsolutions-tests

FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

# Primero los .csproj: la restauracion queda en cache mientras no cambien las dependencias.
COPY Src/InfectionSolutions.Domain/InfectionSolutions.Domain.csproj Src/InfectionSolutions.Domain/
COPY Src/InfectionSolutions.Application/InfectionSolutions.Application.csproj Src/InfectionSolutions.Application/
COPY Src/InfectionSolutions.Infrastructure/InfectionSolutions.Infrastructure.csproj Src/InfectionSolutions.Infrastructure/
COPY Src/InfectionSolutions.Api/InfectionSolutions.Api.csproj Src/InfectionSolutions.Api/
COPY Src/InfectionSolutions.Tests/InfectionSolutions.Tests.csproj Src/InfectionSolutions.Tests/
RUN dotnet restore Src/InfectionSolutions.Tests/InfectionSolutions.Tests.csproj

COPY Src/ Src/
RUN dotnet build Src/InfectionSolutions.Tests/InfectionSolutions.Tests.csproj -c Release --no-restore

# Si una prueba falla, dotnet test termina con codigo distinto de 0 y el contenedor tambien.
ENTRYPOINT ["dotnet", "test", "Src/InfectionSolutions.Tests/InfectionSolutions.Tests.csproj", \
            "-c", "Release", "--no-build", "--logger", "console;verbosity=normal"]
