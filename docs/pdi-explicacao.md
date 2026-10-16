# PDI Onboarding — explicação de ponta a ponta

Guia para apresentar o projeto: o que ele faz, como cada peça funciona, por que as decisões foram tomadas e o que ainda falta. O acompanhamento do progresso fica em [`pdi-cobertura.md`](pdi-cobertura.md).

> Legenda: ✅ construído e testado · 🚧 planejado (fase indicada)

---

## 1. O problema em uma frase

Um cliente quer abrir conta. Ele preenche um **pré-cadastro**, o cadastro passa por **análise antifraude**, alguém **aprova** numa tela e, só então, o sistema **cria as contas** do cliente no sistema core (Oracle).

Cada etapa é feita por um serviço diferente, e eles conversam por **eventos no Kafka**. Assim, uma etapa lenta ou fora do ar não derruba as outras, e nenhum evento se perde no caminho.

---

## 2. O organograma

```
 Postman ──▶ API Onboarding ──grava──▶ Mongo precadastro
                  │
             finalizou?
                  ▼
        [proposta-conta-finalizada]
                  │
                  ▼
     Consumer Proposta Finalizada ──HTTP──▶ API Antifraude ──grava──▶ Mongo AF
                                                                        ▲
     Tela de aprovação ──HTTP──▶ API Antifraude ─────────────────────────┘
                                      │
                                  aprovou?
                                      ▼
                         [proposta-conta-aprovada]
                                      │
                                      ▼
     Mongo precadastro ◀── Consumer Proposta Aprovada
                                      │
                                      ▼
                               [criar-conta]
                                      │
                                      ▼
                         Consumer CriarConta ──▶ Oracle (cria as contas)
```

O que cada caixa é no código:

| Caixa do organograma | Projeto | Status |
|---|---|---|
| API Onboarding | `src/Pdi.Onboarding.Api` (porta 5003) | ✅ |
| Mongo precadastro | Banco `precadastro`, coleção `proposals` | ✅ |
| Tópicos Kafka | Nomes em `Pdi.Onboarding.Contracts/Topics.cs`, criados pelo `kafka-init` | ✅ |
| API Antifraude | `src/Pdi.Antifraude.Api` (porta 5004) | ✅ |
| Mongo AF | Banco `antifraude`, coleção `analyses` | ✅ |
| Consumer Proposta Finalizada | `src/Pdi.Onboarding.Worker` | ✅ |
| Consumer Proposta Aprovada (+ Rejeitada) | `src/Pdi.Onboarding.Worker` | ✅ |
| Consumer CriarConta + Oracle | `src/Pdi.Onboarding.Worker` + schema `PDI` | ✅ |
| Tela de aprovação | `web/antifraude-ui` (React/Vite, porta 8080) | ✅ |

Além do organograma, o sistema trata a **rejeição** (tópico `proposta-conta-rejeitada`) e as mensagens que nunca vão passar (tópicos `*.dlq`).

---

## 3. Arquitetura

### 3.1 Projetos da solution

```
Pdi.Onboarding.sln
├── src/
│   ├── Pdi.Onboarding.Contracts/   biblioteca compartilhada: eventos, tópicos, produtos
│   ├── Pdi.Onboarding.Api/         API do pré-cadastro (+ OutboxDispatcher)
│   ├── Pdi.Antifraude.Api/         API da análise antifraude (+ OutboxDispatcher)
│   └── Pdi.Onboarding.Worker/      host dos consumers Kafka
└── tests/
    ├── Pdi.Onboarding.UnitTests/   API Onboarding + Worker
    └── Pdi.Antifraude.UnitTests/
```

### 3.2 Camadas dentro de cada API

```
Controllers/        HTTP: recebe a request e traduz o Result em status code
Application/        regras: Service, Validator, Request/Response, Result, interfaces
Domain/             entidades e enums (sem dependência de framework)
Infrastructure/     Mongo (repositório, outbox store, índices), Kafka (publisher, dispatcher), Security
```

A regra de dependência é que **Application não conhece Mongo nem Kafka**. Ela só conhece as interfaces (`IProposalRepository`, `IAnalysisRepository`, `IOutboxStore`). É isso que permite testar os services com *fakes* em memória, sem subir infraestrutura.

### 3.3 Infraestrutura (docker-compose)

| Container | Imagem | Porta | Uso |
|---|---|---|---|
| `pdi-kafka` | `apache/kafka:3.8.0` (KRaft, sem Zookeeper) | 9092 | Os tópicos, com 3 partições cada |
| `pdi-kafka-init` | `apache/kafka:3.8.0` | — | Roda uma vez, cria os 4 tópicos e as 4 DLQs e sai |
| `pdi-mongo` | `mongo:7` | 27017 | Bancos `precadastro` e `antifraude` |
| `pdi-oracle` | `gvenzl/oracle-free:23-slim` | 1521 | Schema `PDI`, onde ficam as contas |

> **"Por que dois bancos no mesmo Mongo?"** Em produção seriam dois clusters. O que importa é que **cada serviço só acessa o próprio banco** (*database per service*). A Antifraude nunca lê `precadastro`.

---

## 4. Contratos (`Pdi.Onboarding.Contracts`)

É a única coisa compartilhada entre os serviços. Contém só tipos simples, sem dependência de Mongo, Kafka ou ASP.NET.

### 4.1 Tópicos

| Constante | Tópico | Quem publica | Quem consome |
|---|---|---|---|
| `Topics.ProposalFinalized` | `proposta-conta-finalizada` | API Onboarding (outbox) | Consumer Proposta Finalizada ✅ |
| `Topics.ProposalApproved` | `proposta-conta-aprovada` | API Antifraude (outbox) | Consumer Proposta Aprovada ✅ |
| `Topics.ProposalRejected` | `proposta-conta-rejeitada` | API Antifraude (outbox) | Consumer Proposta Rejeitada ✅ |
| `Topics.CreateAccount` | `criar-conta` | Consumer Proposta Aprovada ✅ | Consumer CriarConta ✅ |
| `Topics.DeadLetter(t)` | `<tópico>.dlq` | Worker, quando a mensagem nunca vai passar | Pessoas (inspeção e reprocessamento manual) |

### 4.2 Envelope comum dos eventos

Todo evento implementa `IOnboardingEvent` e carrega:

| Campo | Para que serve |
|---|---|
| `EventId` | Identidade **desta mensagem**. É novo a cada evento e também é o id da mensagem no outbox. |
| `CorrelationId` | Identidade **do fluxo inteiro**. É igual ao `ProposalId` e atravessa todos os serviços, então dá para seguir uma proposta nos logs de ponta a ponta. |
| `Version` | Versão do contrato (`ContractVersion = 1`). Permite evoluir o evento sem quebrar consumidores antigos. |
| `OccurredAt` | Quando o fato aconteceu (UTC). |

### 4.3 Os eventos

| Evento | Carga útil |
|---|---|
| `ProposalFinalizedEvent` | `ProposalId` + os 10 dados do cliente (nome, CPF, e-mail, telefone, nascimento, endereço, CEP, UF, nome da mãe) |
| `ProposalApprovedEvent` | `ProposalId`, `AnalysisId` e `ApprovedBy` |
| `ProposalRejectedEvent` | `ProposalId`, `AnalysisId`, `RejectedBy` e `Reason` |
| `CreateAccountEvent` | `ProposalId`, `FullName`, `TaxId` e `ProductCodes` |

### 4.4 Produtos (`ProductCodes`)

| Código | Produto |
|---|---|
| 173 | Sinacor (conta de negociação) |
| 120 | Investimentos |
| 9 | Login tipo 9 |
| 10 | Login tipo 10 |

### 4.5 Chave da mensagem e ordenação

A mensagem é publicada com o `CorrelationId` (o id da proposta) como **key**. O Kafka manda mensagens com a mesma key sempre para a mesma partição, e **a ordem só é garantida dentro de uma partição**. Resultado: todos os eventos de uma proposta ficam em ordem, enquanto propostas diferentes são processadas em paralelo nas 3 partições.

O producer usa `Acks = All` (só confirma depois que o broker gravou) e `MessageTimeoutMs = 10s`. O padrão seria 5 minutos; com 10s, uma falha aparece rápido e o outbox tenta de novo.

---

## 5. O fluxo, passo a passo

Vamos acompanhar a cliente **Ana Souza** do começo ao fim.

### Passo 1 — Criar o rascunho ✅

```
POST /api/v1/proposals
{ "fullName": "Ana Souza", "taxId": "52998224725", "email": "ana@email.com" }
```

- O `ProposalService.CreateAsync` gera um `Id` (Guid), define `Status = Draft` e `Version = 1` e grava no Mongo `precadastro.proposals`.
- **Nenhuma validação é aplicada** nesse momento: um rascunho pode estar incompleto, e é isso que permite preencher o cadastro em várias telas.
- A resposta é **201 Created**.

### Passo 2 — Completar os dados ✅

```
PATCH /api/v1/proposals/{id}
{ "phone": "11999998888", "birthDate": "1990-05-15", "street": "Rua A, 100", ... }
```

- **Atualização parcial**: só os campos enviados mudam. Um campo `null` significa "não mexer".
- Os dados são normalizados: `Trim()` em todos e UF em maiúsculas.
- Só é permitido em `Draft`; fora disso a API devolve **409**.
- **Concorrência otimista**: o service lê o documento na versão N e grava com o filtro `{ _id, status: Draft, version: N }`, já com `version = N+1`. Se outra request gravou antes, o filtro não encontra nada e a resposta é **409**.

### Passo 3 — Finalizar ✅ (é o "finalizou?" do organograma)

```
POST /api/v1/proposals/{id}/finalize
```

1. Busca a proposta. Se não existe, **404**. Se não está em `Draft`, **409**.
2. O `ProposalValidator` valida os 10 campos. Se houver erro, devolve **400** com todos os erros de uma vez:

   | Campo | Regra |
   |---|---|
   | FullName, MotherName | obrigatório, ≥ 3 caracteres |
   | TaxId | CPF válido (11 dígitos, não repetidos, os dois dígitos verificadores) |
   | Email | formato `x@y.z` |
   | Phone | 10 ou 11 dígitos |
   | BirthDate | maior de 18 anos |
   | Street, City | obrigatórios |
   | State | uma das 27 UFs |
   | PostalCode | 8 dígitos |

3. Muda para `Finalized` e **coloca o `ProposalFinalizedEvent` em `pendingEvents`**, o outbox embutido no próprio documento.
4. Grava tudo numa **única escrita condicional** (`Draft → Finalized`). Ou o status e o evento são gravados juntos, ou nenhum dos dois.
5. Responde **200**. Ainda não falou com o Kafka.
6. Em background, o **`OutboxDispatcher`** verifica a cada 1s quais documentos têm `pendingEvents`, publica cada evento no Kafka e remove do documento (`$pull`) o que já saiu.

> **O que isso resolve:** antes, a API gravava e depois publicava. Se o Kafka caísse entre os dois passos, a proposta ficava `Finalized` para sempre sem ninguém saber. Agora, se o Kafka estiver fora, o finalize responde 200 normalmente, o evento espera no Mongo e sai sozinho quando o Kafka voltar. *Isso foi testado parando o container do Kafka.*

### Passo 4 — Consumer Proposta Finalizada ✅

- É um consumer no Worker (`GroupId = consumer-proposta-finalizada`) lendo `proposta-conta-finalizada`.
- Para cada mensagem, chama `POST http://localhost:5004/api/v1/analyses` com o próprio evento como body e o header **`X-Internal-Api-Key`**.
- É construído sobre a base genérica **`KafkaConsumerService<TEvent>`**. O consumer concreto só implementa um `IMessageHandler<TEvent>`, e as fases 4 e 5 reaproveitam tudo.
- **Commit manual de offset** (`EnableAutoCommit = false`). O offset só é comitado depois que o efeito colateral aconteceu:

  | Resposta da API | O que o consumer faz | Por quê |
  |---|---|---|
  | 201 Created | comita | análise criada |
  | 204 No Content | comita | já existia (replay), então é sucesso |
  | 400 Bad Request | manda para a **DLQ** e comita | o problema é a *mensagem*, e ela nunca vai passar (*poison message*) |
  | 401, 404, 429, 5xx, timeout, API fora | **não** comita e tenta de novo com backoff | o problema é o *ambiente* (config, deploy, API fora); a mensagem é boa |
  | JSON inválido | manda para a **DLQ** e comita | nem dá para ler a mensagem |

- **Backoff exponencial** entre as tentativas: 1s, 2s, 4s, 8s, 16s e depois 30s fixos.
- **Durante a espera, as partições ficam pausadas**, e o consumer continua chamando `Consume()`. Sem isso, uma espera longa passaria do `max.poll.interval.ms` (5 min), e o Kafka expulsaria o consumer do grupo.
- **A DLQ guarda a mensagem original** (mesma key e value) com os headers `dlq-reason`, `dlq-source-topic`, `dlq-source-partition` e `dlq-source-offset`. Ela pode ser inspecionada e reprocessada depois.
- Um **erro de consumo não fatal** (por exemplo, o tópico ainda não existe) é logado, e o consumer tenta de novo depois de 5s. Erros fatais, como credencial errada, derrubam o processo de propósito.

### Passo 5 — Criar a análise ✅

```
POST /api/v1/analyses   (uso interno; exige X-Internal-Api-Key, senão 401)
```

- A chave é comparada em **tempo constante**, para que não dê para descobri-la medindo o tempo de resposta. O filtro é **fail closed**: se a chave não estiver configurada, ninguém entra.
- O `CreateAnalysisValidator` confere a integridade da mensagem (`EventId`, `ProposalId`, `FullName`, `TaxId`).
- Cria a `Analysis` em `Pending` com um **snapshot** dos dados da cliente. A Antifraude não consulta o precadastro.
- **Idempotência pelo banco**: o índice único `ux_proposalId` faz um replay falhar com `DuplicateKey`. O service devolve `AlreadyExists`, e a API responde **204**.

> **"Por que índice único e não 'consultar antes de inserir'?"** Porque *consultar e depois inserir* tem janela de corrida: duas réplicas do consumer podem consultar ao mesmo tempo, ninguém acha nada e as duas inserem. O índice único é atômico no banco.

### Passo 6 — Tela de aprovação consulta ✅

```
GET /api/v1/analyses?status=Pending     lista ordenada por data de criação
GET /api/v1/analyses/{id}               detalhe com todos os dados
```

O índice `ix_status_createdAt` deixa essa consulta eficiente.

### Passo 7a — Aprovar ✅ (é o "aprovou?" do organograma)

```
POST /api/v1/analyses/{id}/approve
```

1. Se a análise não existe, **404**. Se não está `Pending`, **409**.
2. Marca `Approved`, `DecidedBy` e `DecidedAt` e coloca o `ProposalApprovedEvent` no outbox da análise.
3. Faz o `ReplaceOne` **condicional** com o filtro `{ _id, status: Pending }`. Se alguém rejeitou no mesmo instante, nada é gravado (nem a decisão, nem o evento) e a resposta é **409**.
4. Responde **200**. O `OutboxDispatcher` da Antifraude publica em `proposta-conta-aprovada`.
5. Uma segunda aprovação devolve **409** e não gera evento.

### Passo 7b — Rejeitar ✅

```
POST /api/v1/analyses/{id}/reject
{ "reason": "Documentos ilegíveis" }
```

- O motivo é obrigatório, com 3 a 500 caracteres (senão **400**).
- Usa a mesma troca condicional `Pending → Rejected` e o mesmo outbox. O evento vai para **`proposta-conta-rejeitada`**, com o motivo.
- **Nunca** gera `criar-conta`: nenhuma conta é criada.

### Passo 8 — Consumers Proposta Aprovada / Rejeitada ✅

**Aprovada** (`ProposalApprovedHandler`, GroupId `consumer-proposta-aprovada`):
1. Lê `proposta-conta-aprovada` e busca a proposta no Mongo `precadastro`.
2. Troca condicional `Finalized → Approved` (grava `analysisId`, `approvedBy`, `approvedAt`).
3. Publica `criar-conta` com nome, CPF e `ProductCodes.All` (173, 120, 9, 10).
4. Troca `Approved → AccountsRequested`.

**O status funciona como checkpoint.** Se o processo cair no meio, o replay continua de onde parou:

| Status encontrado | O que já tinha sido feito | O que o handler faz |
|---|---|---|
| `Finalized` | nada | aprova → publica → marca |
| `Approved` | aprovou, talvez não publicou | publica → marca |
| `AccountsRequested` | tudo | só comita (replay) |
| `Draft`, `Rejected`, inexistente | impossível | DLQ |

O status `Approved` significa "falta publicar `criar-conta`", então ele faz o papel de um outbox sem precisar de outbox. Um conflito de concorrência no passo 2 dá **Retry**: na próxima tentativa o handler relê o documento.

**Rejeitada** (`ProposalRejectedHandler`, GroupId `consumer-proposta-rejeitada`): `Finalized → Rejected` com o motivo. Se já estiver `Rejected`, só comita; em qualquer outro status, DLQ.

**`$set`, nunca `ReplaceOne`.** O Worker tem uma visão parcial do documento (`ProposalDocument`: só status, nome, CPF e os campos de decisão). Um replace com essa visão apagaria e-mail, endereço e `pendingEvents`. O `$set` altera só os campos do Worker, e o `$inc` na `version` mantém valendo a concorrência otimista da API. A API pode continuar usando replace porque só faz isso em `Draft`, e o Worker nunca grava nada nesse status.

### Passo 9 — Consumer CriarConta → Oracle ✅

`CreateAccountHandler` (GroupId `consumer-criar-conta`) lê `criar-conta` e grava no Oracle (schema `PDI`, tabela `ACCOUNTS`) via EF Core (`Oracle.EntityFrameworkCore`).

1. **Valida antes de tocar no banco**: `ProposalId`, nome, CPF com 11 dígitos (pontuação é removida), pelo menos 1 produto, todos conhecidos. Mensagem inválida vai para a DLQ.
2. **Vê o que já existe**: busca as contas da proposta e calcula os produtos que **faltam**. Se não falta nada, é replay e só comita.
3. **Cria as que faltam** num único `SaveChanges`, que é uma transação: entram todas ou nenhuma.
4. Se o banco recusar por unique (outro Worker inseriu ao mesmo tempo), devolve **Retry**. Na próxima tentativa, o passo 2 vê tudo criado e comita.

**Número da conta determinístico** (13 dígitos): `PPP` (produto) + 9 dígitos de `SHA-256(proposalId, produto)` + **dígito Luhn**. A mesma proposta e o mesmo produto geram sempre o mesmo número, sem sequence e sem estado. *Prova real: a tabela foi apagada e recriada, o histórico foi reprocessado e os números saíram idênticos.*

**Tabela `ACCOUNTS`** (gerada pela migration `CreateAccounts`, aplicada no startup do Worker):

| Coluna | Tipo Oracle | Observação |
|---|---|---|
| `ID` | `NUMBER(19)` identity | PK |
| `PROPOSAL_ID` | `RAW(16)` | é assim que o Oracle guarda um Guid |
| `PRODUCT_CODE` | `NUMBER(10)` | 173, 120, 9, 10 |
| `ACCOUNT_NUMBER` | `NVARCHAR2(13)` | determinístico |
| `HOLDER_NAME`, `HOLDER_TAX_ID` | `NVARCHAR2` | CPF só com dígitos |
| `SOURCE_EVENT_ID` | `RAW(16)` | qual mensagem criou a conta (rastreabilidade) |
| `CREATED_AT` | `TIMESTAMP(7)` | UTC |

Índices únicos: **`UX_ACCOUNTS_PROPOSAL_PRODUCT (PROPOSAL_ID, PRODUCT_CODE)`**, a garantia de idempotência, e **`UX_ACCOUNTS_NUMBER (ACCOUNT_NUMBER)`**.

### Máquinas de estado

```
Proposta (precadastro)                      Análise (antifraude)

  Draft ──finalize──▶ Finalized ──────┐       Pending ──approve──▶ Approved
                         │            │          │
                (consumer aprovada)   │ (consumer rejeitada)   └────reject────▶ Rejected
                         ▼            ▼
                      Approved     Rejected
                         │
                         ▼
                 AccountsRequested
```

Qualquer transição fora dessas setas é recusada com **409**.

---

## 6. Referência de endpoints

### API Onboarding — `http://localhost:5003` ✅

| Método | Rota | Sucesso | Erros |
|---|---|---|---|
| POST | `/api/v1/proposals` | 201 | — |
| GET | `/api/v1/proposals/{id}` | 200 | 404 |
| PATCH | `/api/v1/proposals/{id}` | 200 | 404, 409 (status ou concorrência) |
| POST | `/api/v1/proposals/{id}/finalize` | 200 + evento no outbox | 400, 404, 409 |
| GET | `/health` | 200 | — |

### API Antifraude — `http://localhost:5004` ✅

| Método | Rota | Sucesso | Erros |
|---|---|---|---|
| POST | `/api/v1/auth/login` | 200 + JWT | 401 |
| POST | `/api/v1/analyses` 🔒 interno | 201 / **204 replay** | 400, **401** |
| GET | `/api/v1/analyses?status=Pending` | 200 | — |
| GET | `/api/v1/analyses/{id}` | 200 | 404 |
| POST | `/api/v1/analyses/{id}/approve` | 200 + evento no outbox | 404, 409 |
| POST | `/api/v1/analyses/{id}/reject` | 200 + evento no outbox | 400, 404, 409 |
| GET | `/health` | 200 | — |

🔒 = exige o header `X-Internal-Api-Key`. As demais rotas de `/analyses` exigem **JWT** (`Authorization: Bearer ...`) e respondem 401 sem ele. As duas APIs têm Swagger em `/swagger` (ambiente Development) e serializam enums como texto (`"Pending"`).

---

## 7. Decisões técnicas (e como justificar)

### 7.1 Result pattern em vez de exceções

Os services devolvem um `ProposalResult`/`AnalysisResult` com `Status`, `Errors` e `Data`. O controller só traduz o status em código HTTP com um `switch`.
- Exceção fica para o que é **excepcional** (o banco caiu). "CPF inválido" é um resultado esperado.
- O service não conhece HTTP.

### 7.2 Concorrência otimista com update condicional

Não usamos lock nem transação. Cada escrita leva no filtro o **estado esperado** (`status` e, no precadastro, `version`). Se o estado mudou, `ModifiedCount == 0` e o service devolve conflito. É barato e funciona com N réplicas.

### 7.3 Transactional Outbox (embutido no documento)

**O problema:** "gravar no banco" e "publicar no Kafka" são dois sistemas, e não existe transação entre eles. Qualquer ordem falha:
- gravar e depois publicar: se o Kafka cair no meio, o evento se perde;
- publicar e depois gravar: se o banco cair no meio, sai um evento de algo que não aconteceu.

**A solução:** o evento é gravado **no banco, junto com o dado**, e um processo separado (`OutboxDispatcher`) publica depois.

**Por que dentro do documento e não numa coleção `outbox`?** Gravar em duas coleções atomicamente exige transação multi-documento no Mongo, e isso exige replica set. Uma escrita num **único documento** é sempre atômica no Mongo. Por isso a lista `pendingEvents` fica dentro da própria proposta ou análise, e o `ReplaceOne` condicional grava a mudança de status e o evento de uma vez.

**Garantia resultante:** *at-least-once*. Se o dispatcher publicar e cair antes do `$pull`, o evento sai de novo. Não tem problema, porque os consumers são idempotentes (7.4).

### 7.4 Idempotência em camadas

Com Kafka (e com outbox), a entrega é **at-least-once**: a mesma mensagem pode chegar mais de uma vez. Por isso cada consumidor precisa ser idempotente:

| Etapa | Mecanismo |
|---|---|
| Criar análise | índice único em `proposalId` → 204 |
| Aprovar/rejeitar análise | update condicional `Pending → ...` → 409 na segunda vez |
| Aprovar proposta | status como checkpoint: `Approved` retoma, `AccountsRequested` só comita |
| Criar contas | cria só os produtos que faltam + unique `(PROPOSAL_ID, PRODUCT_CODE)` + número determinístico |

> Frase para a apresentação: **"Não tentamos garantir que a mensagem chegue uma vez só, porque isso é caro e frágil. Garantimos que processá-la duas vezes dá o mesmo resultado."**

### 7.5 Commit manual, retry e DLQ

- **Commit manual:** com auto-commit, o Kafka pode marcar a mensagem como lida antes de o processamento terminar, e um crash a perderia. Nós comitamos só depois do efeito.
- **Retry segurando a partição:** enquanto uma mensagem falha por causa do ambiente, as seguintes daquela partição esperam. É de propósito: preserva a ordem dos eventos da proposta.
- **DLQ:** se a mensagem é ruim, insistir trava a partição para sempre. Ela vai para `<tópico>.dlq` com o motivo, e a fila anda.
- **A regra para decidir:** *o problema é a mensagem?* Então DLQ. *O problema é o ambiente?* Então retry.

### 7.6 Pausa durante o backoff

O Kafka considera morto um consumer que passa mais de `max.poll.interval.ms` (5 min) sem chamar `Consume()`, e então redistribui as partições dele. Durante o backoff, o consumer **pausa as partições** e continua chamando `Consume()` a cada 200ms. Ele segue vivo no grupo, mas não recebe mensagens novas. Se um rebalance entregar uma partição nova, que não vem pausada, a mensagem que chegar é devolvida com `Seek` e lida depois, na ordem.

### 7.7 Snapshot dos dados na Antifraude

A análise guarda uma cópia dos dados da cliente no momento da finalização. Isso evita acoplamento de banco, serve de auditoria (o analista aprovou exatamente aqueles dados) e deixa a tela funcionando mesmo com a API Onboarding fora.

### 7.8 Segurança serviço → serviço

O `POST /analyses` só deve ser chamado pelo Worker. Ele exige uma **API key interna** no header: um segredo compartilhado, comparado com `CryptographicOperations.FixedTimeEquals` e com política *fail closed*. Para usuários (a tela), o certo é JWT, que entra na Fase 6.

### 7.9 Convenções do Mongo (`MongoConventions`)

- **camelCase** nos campos (`proposalId`, `pendingEvents`).
- **Enums como string** (`"Pending"`), para que o banco seja legível no `mongosh`.
- **Guid em representação Standard** (UUID binário subtipo 4).
- **`IgnoreExtraElements`**: um campo novo não quebra documentos antigos, e um campo ausente vira o valor padrão. Foi assim que `pendingEvents` entrou sem migração.
- O registro roda **antes** da criação dos índices; senão o índice seria criado em `ProposalId` e não protegeria nada.

### 7.10 Infraestrutura duplicada, contrato compartilhado

`KafkaEventPublisher`, `OutboxDispatcher` e `MongoConventions` existem nas duas APIs, de propósito. Em microsserviços, **compartilha-se contrato, não implementação**: o `Contracts` não pode depender do driver do Mongo nem do Kafka.

### 7.11 Ciclo de vida no DI

| Registro | Lifetime | Motivo |
|---|---|---|
| `IMongoClient`, `IMongoDatabase` | Singleton | o client é thread-safe e mantém o pool de conexões |
| Producers Kafka (publisher, DLQ) | Singleton | caros de criar e thread-safe; o `Dispose` faz `Flush` |
| `IOutboxStore` | Singleton | só depende do `IMongoDatabase` |
| `OutboxDispatcher`, consumers | Hosted service | rodam em background enquanto o processo vive |
| Validators | Singleton | não têm estado |
| Repositórios, services, handlers | Scoped | um por request/mensagem (o consumer cria um scope por mensagem) |

---

## 8. Testes

| Projeto | Testes | O que cobre |
|---|---|---|
| `Pdi.Onboarding.UnitTests` | 70 | `ProposalService` (criar, patch, finalize com evento no outbox, inválido, status, conflito), `ProposalValidator`, `OutboxDispatcher` (ordem, falha do Kafka mantém pendente) e Worker (política de commit, backoff, handler com `StubHttpMessageHandler`, consumers aprovada/rejeitada com checkpoint, replay, conflito e Kafka fora, `AccountNumberGenerator` com Luhn e `CreateAccountHandler` com idempotência parcial/total, concorrência e DLQ) |
| `Pdi.Antifraude.UnitTests` | 27 | `AnalysisService` (criar, replay, aprovar/rejeitar com evento no outbox, concorrência), validators, `OutboxDispatcher` e `InternalApiKeyFilter` (chave certa, errada, ausente, não configurada) |

**Estratégia:** testes unitários com **fakes escritos à mão**, sem framework de mock.
- O fake de repositório **simula o índice único** e tem a flag `SimulateConflict` para reproduzir a corrida.
- Os testes de service conferem o **outbox**: o tópico, a key e o payload desserializado do evento gravado junto com o status.
- O `StubHttpMessageHandler` substitui a rede no `HttpClient`.
- O que depende de Kafka e Mongo reais (dispatcher, DLQ, pausa, tópico inexistente) foi **validado rodando os serviços**. Testes de integração com Testcontainers são o próximo passo natural.
- A meta de cobertura é **≥ 77%** (coverlet + reportgenerator, Fase 6).

```
dotnet test Pdi.Onboarding.sln
```

---

## 9. Débitos técnicos

### Resolvidos (e como foi provado)

| Débito | Solução | Prova |
|---|---|---|
| Evento publicado depois do save | Outbox embutido + `OutboxDispatcher` | Kafka parado no finalize: 200, `pending=1`; Kafka volta: `pending=0` e análise criada |
| Rejeição não avisava ninguém | `proposta-conta-rejeitada` via outbox | Evento no tópico com `reason` |
| Poison message travava ou sumia | DLQ com headers de diagnóstico | JSON inválido na DLQ com o motivo |
| Retry longo expulsava o consumer do grupo | Partições pausadas + `Consume()` contínuo | 5 tentativas com a Antifraude fora, recuperação sem rebalance |
| Tópico inexistente derrubava o Worker | `ConsumeException` não fatal → espera 5s | Consumer num tópico inexistente: host vivo |
| Rota interna aberta | `X-Internal-Api-Key` | Sem chave ou com chave errada: 401 |
| Qualquer um aprovava; `DecidedBy` fixo | JWT na Antifraude; `DecidedBy` = `sub` do token | Sem token: 401; aprovação registra `analista`/`supervisor` |
| Tópicos criados "por acaso" | `kafka-init` | 4 tópicos + 4 DLQs |

### Ainda abertos (conscientes)

| Débito | Risco | Plano |
|---|---|---|
| N réplicas da API publicariam o mesmo evento do outbox | Duplicata | Aceito (consumers idempotentes); a solução seria um lease no dispatcher |
| Segredos no `appsettings.json` | Vazamento | Variáveis de ambiente no compose (Fase 6) |
| Reprocessar a DLQ é manual | Operação | Uma ferramenta ou endpoint de replay |

---

## 10. Roteiro de demo (ponta a ponta)

> **Atalho:** `docker compose --profile app up -d --build` sobe tudo, inclusive a tela em http://localhost:8080 (`analista` / `analista123`). E `./scripts/e2e.sh` roda este roteiro inteiro sozinho (17 verificações). Na nuvem, o botão **Open in Codespaces** do README faz o mesmo.

```bash
# 1. Infra (o kafka-init cria os tópicos e sai)
docker compose up -d --remove-orphans

# 2. Serviços (um terminal para cada)
dotnet run --project src/Pdi.Onboarding.Api   --launch-profile http   # :5003
dotnet run --project src/Pdi.Antifraude.Api   --launch-profile http   # :5004
dotnet run --project src/Pdi.Onboarding.Worker
```

1. **Onboarding** (`Pdi.Onboarding.Api.http`): criar → patch → finalize. Mostre também o 400 de um CPF inválido.
2. **A seta funcionando:** `GET /api/v1/analyses?status=Pending` já mostra a análise, criada pelo Worker sem ninguém chamar o POST.
3. **Aprovar** → 200. **Aprovar de novo** → 409. Mostrar o evento:
   ```bash
   docker exec pdi-kafka /opt/kafka/bin/kafka-console-consumer.sh \
     --bootstrap-server localhost:9092 --topic proposta-conta-aprovada --from-beginning
   ```
4. **Outbox (o momento mais forte da demo):**
   ```bash
   docker stop pdi-kafka
   # finalize uma proposta nova → 200 mesmo assim
   docker exec -it pdi-mongo mongosh precadastro --eval 'db.proposals.find({"pendingEvents.0":{$exists:true}})'
   docker start pdi-kafka
   # em segundos: pendingEvents vazio e a análise aparece na Antifraude
   ```
5. **Resiliência do consumer:** derrube a API Antifraude e finalize uma proposta. O Worker loga as tentativas 1, 2, 3... com espera crescente. Suba a Antifraude de novo e a análise aparece.
6. **Replay:** republique uma mensagem em `proposta-conta-finalizada`. O log mostra `answered 204` e não há duplicata.
7. **DLQ:** publique `isso nao e json` no tópico e mostre a mensagem na DLQ com os headers:
   ```bash
   docker exec pdi-kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 \
     --topic proposta-conta-finalizada.dlq --from-beginning --property print.headers=true
   ```
8. **Segurança:** `POST /analyses` sem o header → 401.
9. **Oracle, o fim do organograma:**
   ```bash
   docker exec -i pdi-oracle sqlplus -s pdi/pdi@localhost:1521/FREEPDB1 <<'SQL'
   set lines 200 pages 50
   col HOLDER_NAME format a22
   select PRODUCT_CODE, ACCOUNT_NUMBER, HOLDER_NAME from ACCOUNTS order by ID;
   SQL
   ```
   São 4 contas por proposta aprovada. Republique o `criar-conta`: o log mostra `replay ignored` e continuam 4. Tente um INSERT duplicado à mão e o banco responde `ORA-00001`.

---

## 11. Perguntas prováveis

**Por que Kafka e não chamadas HTTP diretas?**
Desacoplamento temporal. Se a Antifraude estiver fora, a finalização continua funcionando; o evento espera no tópico e é processado quando ela voltar.

**E se o Kafka cair?**
As APIs continuam respondendo. Os eventos ficam no outbox, dentro do Mongo, e o `OutboxDispatcher` publica quando o Kafka voltar. *Isso foi testado.*

**Por que não publicar direto no Kafka dentro do endpoint?**
Porque banco e Kafka não compartilham transação. Ver 7.3: qualquer ordem pode perder ou inventar um evento.

**O que acontece se o Worker cair no meio do processamento?**
O offset não foi comitado, então ao voltar ele relê a mesma mensagem. A idempotência garante que não haverá duplicidade.

**E uma mensagem que nunca vai funcionar?**
Ela vai para a DLQ com o motivo nos headers, e a partição segue. Nada some e nada trava.

**E se duas pessoas clicarem em aprovar ao mesmo tempo?**
Só o primeiro `ReplaceOne` com filtro `status: Pending` encontra o documento. O segundo recebe 409, e como o evento está no mesmo documento, ele também não é gravado.

**Por que o 401/404 tenta de novo e o 400 vai para a DLQ?**
O 400 diz que *a mensagem* está errada, e reenviar não muda nada. O 401 e o 404 dizem que *o ambiente* está errado (chave ou URL mal configurada, deploy incompleto). Quando alguém corrigir a configuração, a mesma mensagem passa.

**Por que Mongo para o cadastro e Oracle para as contas?**
O pré-cadastro é um documento flexível, preenchido aos poucos, e isso combina com um banco de documentos. A conta é um registro financeiro com integridade forte (unique constraint, relacional), e isso combina com o SGBD relacional do core.

**O que você faria diferente em produção?**
JWT e segredos em cofre, observabilidade (OpenTelemetry com o `CorrelationId` como trace), testes de integração com Testcontainers, lease no outbox para várias réplicas e uma ferramenta de replay da DLQ.

---

## 12. Tela, segurança e entrega (Fase 6)

### A tela (`web/antifraude-ui`)

React 18 + TypeScript + Vite, servida por **nginx** no container `web` (porta 8080).

| Página | O que faz |
|---|---|
| **Login** | `POST /antifraude/api/v1/auth/login`; guarda a sessão no `sessionStorage` |
| **Análises** | Lista com filtro (pendentes, aprovadas, rejeitadas, todas), atualizada a cada 5s: a análise "aparece sozinha" quando o Worker a cria |
| **Detalhe** | Dados do cliente (o snapshot), aprovar, rejeitar com motivo (3 a 500 caracteres, mesma regra da API) e a **linha do tempo da proposta**, que consulta a API Onboarding a cada 2s até "contas solicitadas" ou "rejeitada" |
| **Simular cliente** | Faz o papel do Postman do organograma: gera um cliente com CPF válido, cria o rascunho e finaliza |

**Sem CORS, de propósito.** A tela só chama a própria origem (`/onboarding/*` e `/antifraude/*`). O nginx, no container, e o Vite, em dev, repassam para as APIs. Mesma origem significa que não há CORS para configurar nem para errar.

> Bug real encontrado pelo E2E: o `proxy_pass` do nginx fala **HTTP/1.0** por padrão, e o Kestrel recusa (400) um `POST` HTTP/1.0 sem corpo, como o `/finalize` e o `/approve`. A correção foi `proxy_http_version 1.1`.

### Segurança

| Quem chama | Como se autentica | Por quê |
|---|---|---|
| Analista (tela) → API Antifraude | **JWT** HS256 (`POST /api/v1/auth/login`), `[Authorize]` nas rotas | É uma pessoa: o token diz *quem* decidiu (`sub` → `DecidedBy`) |
| Worker → `POST /api/v1/analyses` | **API key** interna (`X-Internal-Api-Key`) | É um serviço, não uma pessoa: segredo compartilhado, comparado em tempo constante |
| Cliente → API Onboarding | aberta | É o "Postman" do organograma |

Os usuários de demo (`analista`, `supervisor`) ficam na configuração. Em produção, o login viria de um provedor de identidade (Entra ID, Keycloak...), e a API só **validaria** o token.

### Containers e compose

- Um **Dockerfile multi-stage** por serviço .NET: o SDK compila e só o runtime vai para a imagem, rodando com o usuário `app`, sem root.
- `docker compose up -d` sobe só a infra (para `dotnet run`). `docker compose --profile app up -d` sobe **tudo**.
- **Kafka com dois listeners**: `localhost:9092` para quem está na máquina e `kafka:29092` para quem está na rede do compose. O broker devolve ao cliente o endereço *anunciado*; com só `localhost`, um container tentaria falar com ele mesmo.
- `depends_on` com `service_healthy` (Mongo, Oracle, Kafka) e `service_completed_successfully` (`kafka-init`), para que ninguém suba antes da dependência estar pronta.

### Testes em 4 níveis

| Nível | Ferramenta | O que prova |
|---|---|---|
| Unitário (.NET, 103) | xUnit + fakes manuais | Regras de negócio, idempotência, políticas de retry/DLQ, JWT |
| Integração (.NET, 19) | `WebApplicationFactory` + Kafka/Mongo/Oracle reais | Program/DI/controllers de verdade, índices do Mongo, outbox, consumer Kafka (ordem, retry, DLQ, commit, tópico inexistente), unique do Oracle |
| Front (20) | Vitest + Testing Library | Cliente HTTP (JWT, 401, erros), formulário de rejeição, CPF |
| Ponta a ponta (17 verificações) | `scripts/e2e.sh` | O organograma inteiro em containers, passando pelo nginx |

### Pipeline (GitHub Actions)

- **Backend**: sobe a infra no runner, roda unitários + integração e aplica o **gate de cobertura ≥ 77%**. O relatório aparece no resumo do job.
- **Frontend**: typecheck, testes e build.
- **E2E e imagens**: stack inteira + `e2e.sh`. Na `main`, publica as 4 imagens em `ghcr.io/<dono>/pdi-fagner-dotnet-react/*`.
- **Codespaces**: o `.devcontainer/` baixa essas imagens (ou builda) e sobe tudo; a porta 8080 abre a tela.

---

## 13. Glossário

| Termo | Significado |
|---|---|
| **At-least-once** | A mensagem chega pelo menos uma vez, possivelmente mais |
| **Idempotência** | Processar a mesma coisa N vezes tem o mesmo efeito de processar uma |
| **Outbox** | Gravar o evento junto com o dado e publicar depois, para que os dois aconteçam ou nenhum aconteça |
| **DLQ (dead-letter queue)** | Tópico para onde vão as mensagens que nunca vão conseguir ser processadas |
| **Offset** | Posição de uma mensagem numa partição; "comitar" é dizer ao Kafka "já processei até aqui" |
| **Consumer group / GroupId** | Consumers com o mesmo GroupId dividem as partições; GroupIds diferentes recebem cópias independentes |
| **Rebalance** | O Kafka redistribui as partições entre os consumers do grupo (alguém entrou, saiu ou foi considerado morto) |
| **Poison message** | Mensagem que sempre falha; sem DLQ, trava a partição |
| **Backoff exponencial** | Esperar cada vez mais entre as tentativas (1s, 2s, 4s...) para não sobrecarregar quem está com problema |
| **Concorrência otimista** | Não bloqueia na leitura; detecta o conflito na escrita (filtro por versão ou status) |
| **Snapshot** | Cópia dos dados num momento, guardada por outro serviço |
| **Fail closed** | Na dúvida (por exemplo, sem configuração), negar acesso |
| **Database per service** | Cada serviço é dono exclusivo do seu banco |
