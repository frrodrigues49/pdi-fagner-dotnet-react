# PDI Onboarding · .NET 8 + React

[![Backend](https://github.com/frrodrigues49/pdi-fagner-dotnet-react/actions/workflows/backend.yml/badge.svg)](https://github.com/frrodrigues49/pdi-fagner-dotnet-react/actions/workflows/backend.yml)
[![Frontend](https://github.com/frrodrigues49/pdi-fagner-dotnet-react/actions/workflows/frontend.yml/badge.svg)](https://github.com/frrodrigues49/pdi-fagner-dotnet-react/actions/workflows/frontend.yml)
[![E2E](https://github.com/frrodrigues49/pdi-fagner-dotnet-react/actions/workflows/e2e.yml/badge.svg)](https://github.com/frrodrigues49/pdi-fagner-dotnet-react/actions/workflows/e2e.yml)

[![Open in GitHub Codespaces](https://github.com/codespaces/badge.svg)](https://codespaces.new/frrodrigues49/pdi-fagner-dotnet-react?quickstart=1)

Abertura de conta orientada a eventos: um cliente faz o **pré-cadastro**, a proposta passa por **análise antifraude**, um analista **aprova** numa tela e o sistema **cria as contas** no core (Oracle). Cada etapa é um serviço, e eles conversam por **Kafka**.

Projeto do meu PDI de transição de **frontend para fullstack**.

```
 Cliente ──▶ API Onboarding ──▶ Mongo precadastro
                 │ finalizou? (outbox)
                 ▼
     [proposta-conta-finalizada] ──▶ Worker ──HTTP──▶ API Antifraude ──▶ Mongo AF
                                                           ▲
     Tela de aprovação (React) ──JWT──────────────────────┘
                                                           │ aprovou? (outbox)
                                                           ▼
     Mongo precadastro ◀── Worker ◀── [proposta-conta-aprovada]
                             │
                             ▼
                      [criar-conta] ──▶ Worker ──▶ Oracle (4 contas: 173, 120, 9, 10)
```

## Testar em 1 clique (Codespaces)

1. Clique em **Open in GitHub Codespaces**, logo acima. Ele usa a máquina de 4 núcleos e sobe a stack inteira sozinho, em cerca de 3 a 5 minutos na primeira vez.
2. Quando a aba **PORTS** mostrar a porta **8080**, abra-a. É a tela de aprovação.
3. Entre com **`analista` / `analista123`**.
4. Vá em **Simular cliente** e clique em **Criar e finalizar proposta**.
5. Volte em **Análises**. Em poucos segundos a proposta aparece, criada pelo Worker a partir do evento no Kafka.
6. Abra a proposta, clique em **Aprovar** e acompanhe a linha do tempo até **"Contas solicitadas ao core"**.

> Para conferir as contas no Oracle, rode no terminal do Codespace:
> `docker exec -i pdi-oracle sqlplus -s pdi/pdi@localhost:1521/FREEPDB1 <<< "select PRODUCT_CODE, ACCOUNT_NUMBER, HOLDER_NAME from ACCOUNTS;"`

Para testar **tudo** automaticamente: `./scripts/e2e.sh`. São 17 verificações, incluindo replay, rejeição e DLQ.

## Rodar na sua máquina

Pré-requisito: Docker, com pelo menos **6 GB de RAM** para os containers por causa do Oracle.

```bash
docker compose --profile app up -d --build   # tudo em container
./scripts/e2e.sh                             # opcional: valida o fluxo inteiro
```

| O quê | Onde |
|---|---|
| Tela de aprovação | http://localhost:8080 (`analista` / `analista123` ou `supervisor` / `supervisor123`) |
| API Onboarding (Swagger) | http://localhost:5003/swagger |
| API Antifraude (Swagger) | http://localhost:5004/swagger (botão **Authorize**: cole o token do `/api/v1/auth/login`) |

**Modo desenvolvimento:** a infra fica em container e o código roda com hot reload.

```bash
docker compose up -d                                   # só Kafka, Mongo e Oracle
dotnet run --project src/Pdi.Onboarding.Api   --launch-profile http
dotnet run --project src/Pdi.Antifraude.Api   --launch-profile http
dotnet run --project src/Pdi.Onboarding.Worker
npm --prefix web/antifraude-ui run dev                 # http://localhost:5173
```

## Stack

| Camada | Tecnologia |
|---|---|
| APIs e Worker | .NET 8, ASP.NET Core, BackgroundService |
| Mensageria | Kafka (KRaft), Confluent.Kafka, commit manual, DLQ |
| Bancos | MongoDB (pré-cadastro e antifraude), Oracle 23 Free + EF Core (contas) |
| Tela | React 18, TypeScript, Vite, React Router |
| Segurança | JWT (tela → Antifraude), API key interna (Worker → Antifraude) |
| Testes | xUnit, fakes manuais, WebApplicationFactory, Vitest, Testing Library, script E2E |
| Entrega | Docker Compose, GitHub Actions, GHCR, Codespaces |

## Decisões que valem a leitura

- **Outbox embutido no documento.** O status e o evento são gravados na *mesma* escrita do Mongo, e um dispatcher publica depois. Se o Kafka cair, nada se perde.
- **Idempotência em cada consumer.** O Kafka entrega *at-least-once*. Por isso há unique index (análise e contas), update condicional (aprovação) e o status funcionando como checkpoint.
- **Retry ou DLQ.** Se o problema é o *ambiente* (API fora, 5xx, 401), o consumer tenta de novo com backoff, pausando a partição. Se o problema é a *mensagem* (JSON inválido, 400), ela vai para `<tópico>.dlq` com o motivo.
- **Número de conta determinístico** (SHA-256 da proposta + produto + dígito Luhn). O replay gera exatamente o mesmo número.

A explicação completa, com perguntas prováveis de apresentação, está em **[docs/pdi-explicacao.md](docs/pdi-explicacao.md)**. O acompanhamento por fase está em [docs/pdi-cobertura.md](docs/pdi-cobertura.md).

## Testes e pipeline

| Workflow | O que faz |
|---|---|
| **Backend** | Sobe Kafka, Mongo e Oracle no runner e roda **testes unitários + de integração** (APIs reais com `WebApplicationFactory`, consumer Kafka real, Oracle real), com **gate de cobertura ≥ 77%** |
| **Frontend** | Typecheck, Vitest e build |
| **E2E e imagens** | Sobe a stack inteira em containers e roda `scripts/e2e.sh`. Na `main`, publica as 4 imagens no **GHCR** |

```bash
dotnet test Pdi.Onboarding.sln                      # integração é pulada se a infra estiver fora do ar
npm --prefix web/antifraude-ui test
```

## Estrutura

```
src/
  Pdi.Onboarding.Contracts/   eventos, tópicos e produtos (compartilhado, sem dependência de infra)
  Pdi.Onboarding.Api/         pré-cadastro (Mongo) + outbox
  Pdi.Antifraude.Api/         análises (Mongo), aprovar/rejeitar, JWT, outbox
  Pdi.Onboarding.Worker/      4 consumers Kafka + Oracle (EF Core)
web/antifraude-ui/            tela de aprovação (React + Vite, servida por nginx)
tests/                        unitários (Onboarding, Antifraude) e integração
scripts/e2e.sh                teste ponta a ponta
.devcontainer/                Codespaces
```

> ⚠️ As senhas e chaves do `appsettings.json` e do compose (`pdi/pdi`, chave interna, chave do JWT, usuários de demo) são **valores de desenvolvimento**, públicos de propósito. Em produção viriam de um cofre de segredos, e o login viria de um provedor de identidade.
