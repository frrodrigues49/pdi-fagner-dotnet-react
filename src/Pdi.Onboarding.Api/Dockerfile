# Build context: raiz do repositório (precisa do Pdi.Onboarding.Contracts).
#   docker build -f src/Pdi.Onboarding.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 1º só os .csproj: o restore fica em cache enquanto as dependências não mudarem.
COPY src/Pdi.Onboarding.Contracts/Pdi.Onboarding.Contracts.csproj src/Pdi.Onboarding.Contracts/
COPY src/Pdi.Onboarding.Api/Pdi.Onboarding.Api.csproj src/Pdi.Onboarding.Api/
RUN dotnet restore src/Pdi.Onboarding.Api/Pdi.Onboarding.Api.csproj

COPY src/Pdi.Onboarding.Contracts/ src/Pdi.Onboarding.Contracts/
COPY src/Pdi.Onboarding.Api/ src/Pdi.Onboarding.Api/
RUN dotnet publish src/Pdi.Onboarding.Api/Pdi.Onboarding.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Usuário sem privilégio que já vem na imagem oficial.
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pdi.Onboarding.Api.dll"]
