# Plano de cobertura — PDI Onboarding (organograma)

Documento de acompanhamento do PDI. O projeto é construído do zero, caixa por caixa do organograma. A explicação técnica de ponta a ponta (para apresentar) está em [`pdi-explicacao.md`](pdi-explicacao.md).

| | |
|---|---|
| Referência | Organograma `pdi.jpeg` |
| Repositório | `pdi-fagner` |
| Última atualização | 15/10/2026 |
| Completude estimada | 100% (7 de 7 fases) |
| Fase atual | Concluído |

Como atualizar: marque `[x]` o que fechou, ajuste a data e recalcule o percentual.

---

## Fluxo do organograma

```
Postman → API Onboarding ──grava──▶ Mongo precadastro
               │ finalizou?
               ▼
      [proposta-conta-finalizada] ──▶ Consumer Proposta Finalizada ──▶ API Antifraude ──▶ Mongo AF
                                                                                          ▲
      Tela de aprovação ──▶ API Antifraude ── aprovou? ──▶ [proposta-conta-aprovada]
                                                                   │
      Mongo precadastro ◀── Consumer Proposta Aprovada ◀───────────┘
                                   │
                                   ▼
                            [criar-conta] ──▶ Consumer CriarConta ──▶ Oracle (cria as contas)
```

Mensageria: **Kafka**. Tópicos: `proposta-conta-finalizada`, `proposta-conta-aprovada` e `criar-conta`.

| Serviço | Porta | Banco |
|---|---|---|
| API Onboarding | 5003 | Mongo `precadastro` |
| API Antifraude | 5004 | Mongo `antifraude` |
| Worker (3 consumers) | — | Mongo `precadastro` + Oracle `PDI` |

---

## Definition of Done (vale para toda fase)

1. `dotnet build` limpo e `dotnet test` verde
2. Testes unitários do que a fase criou (fakes manuais)
3. `.http` ou comando Kafka provando o caminho feliz **e** o replay
4. Checklist abaixo marcado

---

## Resumo

| Fase | Caixa do organograma | Completude |
|---|---|---|
| 0 | Fundação: infra e contratos | 100% |
| 1 | API Onboarding → Mongo precadastro | 100% |
| 2 | API Antifraude → Mongo AF | 100% |
| 3 | Worker + Consumer Proposta Finalizada | 100% |
| 4 | Consumers Proposta Aprovada + Rejeitada | 100% |
| 5 | Consumer CriarConta → Oracle | 100% |
| 6 | Tela de aprovação + entrega | 100% |

---

## Fase 0 — Fundação

- [x] Compose: Kafka, `mongo:7` e `gvenzl/oracle-free:23-slim` (schema `PDI`)
- [x] Projeto `Pdi.Onboarding.Contracts`
- [x] `Topics` com os 3 tópicos
- [x] `ProposalFinalizedEvent`, `ProposalApprovedEvent` e `CreateAccountEvent` (`EventId`, `CorrelationId`, `Version`)
- [x] `IOnboardingEvent` e `ProductCodes` (173, 120, 9, 10)

## Fase 1 — API Onboarding → Mongo precadastro

- [x] `MongoOptions`, `MongoConventions` e `IMongoDatabase` no DI
- [x] Documento `Proposal` (10 campos + status, versão e datas)
- [x] `POST`, `PATCH` (só em `Draft`, com versão otimista) e `GET /api/v1/proposals`
- [x] `POST /api/v1/proposals/{id}/finalize`: valida, faz `Draft → Finalized` condicional e publica `proposta-conta-finalizada`
- [x] `KafkaEventPublisher` usando `Topics`
- [x] `ProposalServiceTests` e `ProposalValidatorTests`
- [x] `.http` com o fluxo de proposals

## Fase 2 — API Antifraude → Mongo AF

- [x] Coleção `analyses` no banco `antifraude`, com unique index em `proposalId`
- [x] `POST /api/v1/analyses` (uso interno do consumer; replay → 204)
- [x] `GET /api/v1/analyses?status=` e `GET /api/v1/analyses/{id}`
- [x] `POST /api/v1/analyses/{id}/approve`: `Pending → Approved` condicional e publica `proposta-conta-aprovada`
- [x] `POST /api/v1/analyses/{id}/reject`: motivo obrigatório, não publica
- [x] Projeto `Pdi.Antifraude.UnitTests` com `AnalysisServiceTests` e testes dos validators (19 testes)
- [x] `.http` da API Antifraude

Validado em 15/10 contra Mongo/Kafka reais: 201 → replay 204 → 400 (motivo curto) → approve 200 → reapprove 409; índice `ux_proposalId` único; evento em `proposta-conta-aprovada`.

## Fase 3 — Worker + Consumer Proposta Finalizada

- [x] Base `KafkaConsumerService<TEvent>` + `IMessageHandler<TEvent>` (commit manual, scope por mensagem, retry com backoff 1s→30s)
- [x] `AddKafkaConsumer<TEvent, THandler>(topic, groupId)`: registrar um consumer novo ocupa uma linha
- [x] `ProposalFinalizedConsumer` lendo `proposta-conta-finalizada` (GroupId `consumer-proposta-finalizada`)
- [x] `AntifraudeApiClient` → `POST /api/v1/analyses` (o body é o próprio evento)
- [x] Política de commit: 2xx comita; 400 → DLQ; 401, 404, 429, 5xx e timeout tentam de novo
- [x] Testes: política, backoff e handler com `StubHttpMessageHandler`
- [x] `ConsumeException` não fatal tratado (tópico inexistente não derruba mais o Worker)

Validado em 16/10 com os 3 processos: finalize → análise criada sozinha (201); replay → 204 sem duplicar; JSON inválido → logado e ignorado; Antifraude fora → retries 1, 2, 4, 8s e, ao voltar, a análise é criada.

## Fase 4 — Consumer Proposta Aprovada

- [x] `ProposalApprovedHandler` lendo `proposta-conta-aprovada` (GroupId `consumer-proposta-aprovada`)
- [x] Atualização condicional `Finalized → Approved` no Mongo precadastro (`$set` + `$inc version`, nunca replace)
- [x] Publica `criar-conta` com o cliente e os produtos e depois passa a proposta para `AccountsRequested`
- [x] Idempotente com status como checkpoint: `Approved` retoma a publicação, `AccountsRequested` só comita
- [x] `ProposalRejectedHandler` lendo `proposta-conta-rejeitada`: `Finalized → Rejected` com motivo
- [x] Testes: 16 novos (fake de store que devolve cópia, como o Mongo)

Validado em 16/10: aprovar → `AccountsRequested` + 1 `criar-conta` com `[173,120,9,10]`; rejeitar → `Rejected` com motivo; replay → `replay ignored`, sem 2º `criar-conta`; dados do cliente preservados.

## Fase 5 — Consumer CriarConta → Oracle

- [x] `Oracle.EntityFrameworkCore` 8.23.70, `AccountsDbContext`, entidade `Account` e migration `CreateAccounts` (aplicada no startup do Worker)
- [x] `CreateAccountHandler` lendo `criar-conta` (GroupId `consumer-criar-conta`): uma conta por produto (173, 120, 9, 10)
- [x] Número de conta determinístico: `PPP` + 9 dígitos de SHA-256(proposta, produto) + dígito Luhn (13 dígitos)
- [x] Idempotência em 2 camadas: cria só os produtos que faltam + unique `UX_ACCOUNTS_PROPOSAL_PRODUCT` (violação → Retry → relê → Commit)
- [x] Mensagem inválida (produto desconhecido, CPF sem 11 dígitos, sem produtos) → DLQ sem tocar no banco
- [x] Testes: 21 novos (gerador + handler)

Validado em 16/10: histórico de `criar-conta` reprocessado (5 propostas × 4 contas), produto 999 → DLQ, replay → `replay ignored` e continua com 4 contas, INSERT manual duplicado → `ORA-00001`. Determinismo: depois de apagar e recriar a tabela, os números de conta saíram **idênticos**.

## Fase 6 — Tela de aprovação + entrega

- [x] `web/antifraude-ui` (React 18 + TypeScript + Vite): login, lista com filtro e atualização a cada 5s, detalhe, aprovar, rejeitar (com motivo)
- [x] Acompanhamento da proposta em tempo real (linha do tempo até "contas solicitadas")
- [x] "Simular cliente": cria e finaliza propostas sem Postman (gera CPF válido)
- [x] JWT na API Antifraude (`POST /api/v1/auth/login`); `DecidedBy` vem do token
- [x] Sem CORS: nginx (produção) e Vite (dev) fazem proxy de `/onboarding` e `/antifraude` na mesma origem
- [x] Dockerfiles das 2 APIs e do Worker; compose com profile `app` (stack inteira) e listener interno do Kafka
- [x] Testes de integração (`Pdi.IntegrationTests`, 19) contra Kafka/Mongo/Oracle reais
- [x] Gate de cobertura ≥ 77% no CI (coverlet + reportgenerator)
- [x] `scripts/e2e.sh`: 17 verificações ponta a ponta passando pelo nginx
- [x] GitHub Actions: Backend, Frontend, E2E + publicação das imagens no GHCR
- [x] Codespaces (`.devcontainer/`): stack inteira em 1 clique
- [x] README

---

## Endpoints

| Serviço | Rota | Status |
|---|---|---|
| Onboarding | `POST /api/v1/proposals` | Feito |
| Onboarding | `PATCH /api/v1/proposals/{id}` | Feito |
| Onboarding | `GET /api/v1/proposals/{id}` | Feito |
| Onboarding | `POST /api/v1/proposals/{id}/finalize` | Feito |
| Antifraude | `POST /api/v1/analyses` (header `X-Internal-Api-Key`) | Feito |
| Antifraude | `GET /api/v1/analyses?status=` | Feito |
| Antifraude | `GET /api/v1/analyses/{id}` | Feito |
| Antifraude | `POST /api/v1/analyses/{id}/approve` | Feito |
| Antifraude | `POST /api/v1/analyses/{id}/reject` | Feito |

---

## Critérios de aceite

| # | Critério | Status | Evidência |
|---|---|---|---|
| 1 | O pré-cadastro é salvo no Mongo e pode ser editado enquanto está em `Draft` | Feito | `ProposalService` + `MongoProposalRepository` |
| 2 | O finalize valida os 10 campos e publica um único `proposta-conta-finalizada` | Feito | `ProposalServiceTests` |
| 3 | Dados inválidos impedem a finalização com erros claros (400) | Feito | `ProposalValidator` |
| 4 | O consumer finalizada cria uma única análise no Mongo AF, mesmo com replay | Feito | Replay manual no tópico → 204, 1 análise |
| 5 | A aprovação publica `proposta-conta-aprovada` uma única vez (reaprovação → 409) | Feito | `AnalysisServiceTests` + teste manual |
| 6 | A rejeição exige motivo e não dispara a criação de contas | Feito | `RejectAsync_WhenPending_ShouldRejectAndQueueRejectedEvent` (só publica `proposta-conta-rejeitada`) |
| 7 | O consumer aprovada atualiza o precadastro e publica `criar-conta` de forma idempotente | Feito | `ProposalApprovedHandlerTests` + replay manual |
| 8 | O consumer CriarConta cria exatamente os produtos 173, 120, 9 e 10 no Oracle | Feito | `CreateAccountHandlerTests` + `SELECT` no Oracle |
| 9 | Replay de `criar-conta` não duplica conta | Feito | Replay manual + `ORA-00001` no INSERT duplicado |
| 10 | Transições inválidas são recusadas | Parcial | 409 em patch/finalize fora de `Draft` e em approve/reject fora de `Pending` |

Marcador: **9 feitos · 1 parcial · 0 abertos**.

---

## Produtos

| Código | Tipo | Conta |
|---|---|---|
| 173 | Sinacor | Negociação |
| 120 | Investimentos | Investimentos |
| 9 | Login | Acesso tipo 9 |
| 10 | Login | Acesso tipo 10 |

---

## Stack

| Tecnologia | Papel | Status |
|---|---|---|
| .NET 8 / ASP.NET Core | API Onboarding, API Antifraude, Worker | 2 APIs prontas |
| MongoDB | precadastro e antifraude | Ambos prontos |
| Oracle + EF Core | Contas | `ACCOUNTS` com 2 unique indexes, migration EF |
| Kafka | 4 tópicos + 4 DLQs (`kafka-init`) | Outbox nas 2 APIs, 4 consumers |
| React/Vite | Tela de aprovação | Pronta, 20 testes Vitest |
| xUnit, WebApplicationFactory, Vitest | Testes | 103 unitários .NET + 19 de integração + 20 do front + E2E (17 verificações) |
| Docker Compose | Demo local e CI | Infra + profile `app` com a stack inteira |

---

## Débitos técnicos

Resolvidos em 16/10/2026 (todos validados com os serviços rodando):

| Débito | Solução | Prova |
|---|---|---|
| 🐛 Tópico inexistente derrubava o Worker | `ConsumeException` não fatal → log + espera 5s | Consumer real num tópico inexistente: host vivo |
| Evento publicado depois do save (finalize/approve) | **Outbox embutido no documento** (`pendingEvents`) + `OutboxDispatcher` | Kafka parado no finalize → 200, `pending=1` no Mongo → Kafka volta → `pending=0` e análise criada |
| Rejeição não avisava ninguém | `proposta-conta-rejeitada` via outbox | Evento no tópico com `reason` |
| Sem DLQ | `<tópico>.dlq` com headers `dlq-reason`/`dlq-source-*` | JSON inválido foi para a DLQ com o motivo |
| Retry longo estourava `max.poll.interval.ms` | Partições pausadas + `Consume()` durante o backoff | 5 tentativas com a Antifraude fora, recuperação sem rebalance |
| `POST /analyses` aberto | `X-Internal-Api-Key` (comparação em tempo constante, fail closed) | Sem chave/chave errada → 401 |
| Tópicos criados "por acaso" | `kafka-init` no compose cria 4 tópicos + 4 DLQs | `kafka-topics --list` |

Ainda abertos (conscientes):

| Débito | Quando |
|---|---|
| Várias réplicas da API publicariam o mesmo evento do outbox | Aceito: at-least-once + consumers idempotentes. Solução: lease/lock no dispatcher |
| Segredos de demo no `appsettings.json` | Aceito para o PDI: o compose já aceita `INTERNAL_API_KEY`/`AUTH_SIGNING_KEY` por env; em produção, cofre de segredos |
| Sem ferramenta para reprocessar a DLQ | Hoje é manual (`kafka-console-consumer`/`producer`) |
