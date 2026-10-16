# Build context: raiz do repositório (precisa do Pdi.Onboarding.Contracts).
#   docker build -f src/Pdi.Onboarding.Worker/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1º só os .csproj: o restore fica em cache enquanto as dependências não mudarem.
COPY src/Pdi.Onboarding.Contracts/Pdi.Onboarding.Contracts.csproj src/Pdi.Onboarding.Contracts/
COPY src/Pdi.Onboarding.Worker/Pdi.Onboarding.Worker.csproj src/Pdi.Onboarding.Worker/
RUN dotnet restore src/Pdi.Onboarding.Worker/Pdi.Onboarding.Worker.csproj

COPY src/Pdi.Onboarding.Contracts/ src/Pdi.Onboarding.Contracts/
COPY src/Pdi.Onboarding.Worker/ src/Pdi.Onboarding.Worker/
RUN dotnet publish src/Pdi.Onboarding.Worker/Pdi.Onboarding.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app .
# Usuário sem privilégio que já vem na imagem oficial.
USER app

ENTRYPOINT ["dotnet", "Pdi.Onboarding.Worker.dll"]
