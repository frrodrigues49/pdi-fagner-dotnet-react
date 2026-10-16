# Build context: raiz do repositório (precisa do Pdi.Onboarding.Contracts).
#   docker build -f src/Pdi.Antifraude.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1º só os .csproj: o restore fica em cache enquanto as dependências não mudarem.
COPY src/Pdi.Onboarding.Contracts/Pdi.Onboarding.Contracts.csproj src/Pdi.Onboarding.Contracts/
COPY src/Pdi.Antifraude.Api/Pdi.Antifraude.Api.csproj src/Pdi.Antifraude.Api/
RUN dotnet restore src/Pdi.Antifraude.Api/Pdi.Antifraude.Api.csproj

COPY src/Pdi.Onboarding.Contracts/ src/Pdi.Onboarding.Contracts/
COPY src/Pdi.Antifraude.Api/ src/Pdi.Antifraude.Api/
RUN dotnet publish src/Pdi.Antifraude.Api/Pdi.Antifraude.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Usuário sem privilégio que já vem na imagem oficial.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pdi.Antifraude.Api.dll"]
