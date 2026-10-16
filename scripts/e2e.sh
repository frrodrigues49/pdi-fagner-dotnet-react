#!/usr/bin/env bash
# Teste ponta a ponta do organograma contra a stack rodando em containers:
#   docker compose --profile app up -d --build && ./scripts/e2e.sh
#
# Passa pelo nginx da tela (mesmo caminho que o navegador): /onboarding/* e /antifraude/*.
set -euo pipefail

UI="${UI_URL:-http://localhost:8080}"
ONB="$UI/onboarding/api/v1"
AF="$UI/antifraude/api/v1"
INTERNAL_KEY="${INTERNAL_API_KEY:-dev-internal-key-troque-em-producao}"
RUN_ID="$(date +%s)"
PASS=0

green() { printf '\033[32m%s\033[0m\n' "$*"; }
red() { printf '\033[31m%s\033[0m\n' "$*"; }
step() { printf '\n\033[1m▸ %s\033[0m\n' "$*"; }
ok() { PASS=$((PASS + 1)); green "  ✓ $*"; }
fail() { red "  ✗ $*"; exit 1; }
json() { python3 -c "import sys,json; d=json.load(sys.stdin); print($1)"; }

# eventually <segundos> <descrição> <comando...>: repete até o comando ter sucesso
eventually() {
  local timeout=$1 what=$2; shift 2
  local deadline=$((SECONDS + timeout))
  until "$@" >/dev/null 2>&1; do
    [ $SECONDS -ge $deadline ] && fail "timeout (${timeout}s): $what"
    sleep 1
  done
}

expect_status() { # <esperado> <método> <url> [curl args...]
  local expected=$1 method=$2 url=$3; shift 3
  local got
  got=$(curl -s -o /dev/null -w '%{http_code}' -X "$method" "$url" "$@")
  [ "$got" = "$expected" ] || fail "$method $url → HTTP $got (esperado $expected)"
}

oracle() {
  docker exec -i pdi-oracle sqlplus -s pdi/pdi@localhost:1521/FREEPDB1 <<SQL | tr -d '[:space:]'
set heading off feedback off pages 0
$1
SQL
}

customer() { # <nome> [cpf]
  cat <<JSON
{"fullName":"$1","taxId":"${2:-52998224725}","email":"e2e@email.com","phone":"11999998888","birthDate":"1990-05-15",
 "street":"Av. Paulista, 1000","city":"Sao Paulo","state":"SP","postalCode":"01310100","motherName":"Maria E2E"}
JSON
}

new_finalized_proposal() { # <nome> → imprime o id
  # `set -e` NÃO vale dentro de $(...): cada falha precisa de `|| return 1` explícito.
  local id status
  id=$(curl -sf -X POST "$ONB/proposals" -H 'Content-Type: application/json' -d "$(customer "$1")" | json "d['id']") || return 1
  status=$(curl -s -o /dev/null -w '%{http_code}' -X POST "$ONB/proposals/$id/finalize")
  [ "$status" = 200 ] || { echo "finalize → HTTP $status" >&2; return 1; }
  echo "$id"
}

analysis_of() { # <proposalId> → imprime o id da análise (vazio se não existe)
  curl -sf "$AF/analyses" -H "Authorization: Bearer $TOKEN" | json "next((a['id'] for a in d if a['proposalId']=='$1'), '')"
}

has_analysis() { [ -n "$(analysis_of "$1")" ]; }
proposal_is() { [ "$(curl -sf "$ONB/proposals/$1" | json "d['status']")" = "$2" ]; }

# --------------------------------------------------------------------------- 0
step "Stack no ar"
eventually 180 "tela (nginx)" curl -sf "$UI/"
eventually 180 "API Onboarding" curl -sf "$UI/onboarding/health"
eventually 180 "API Antifraude" curl -sf "$UI/antifraude/health"
eventually 240 "Worker com 4 consumers" bash -c '[ "$(docker logs pdi-worker 2>&1 | grep -c "listening to")" -ge 4 ]'
ok "tela, 2 APIs e Worker (4 consumers) respondendo"

# --------------------------------------------------------------------------- 1
step "Segurança"
expect_status 401 GET "$AF/analyses"
ok "GET /analyses sem JWT → 401"
expect_status 401 POST "$AF/analyses" -H 'Content-Type: application/json' -d '{}'
ok "POST /analyses (rota interna) sem X-Internal-Api-Key → 401"
expect_status 401 POST "$AF/auth/login" -H 'Content-Type: application/json' -d '{"username":"analista","password":"errada"}'
ok "login com senha errada → 401"
TOKEN=$(curl -sf -X POST "$AF/auth/login" -H 'Content-Type: application/json' \
  -d '{"username":"analista","password":"analista123"}' | json "d['accessToken']")
[ -n "$TOKEN" ] || fail "login não devolveu token"
ok "login analista → JWT"

# --------------------------------------------------------------------------- 2
step "Validação no finalize"
BAD=$(curl -sf -X POST "$ONB/proposals" -H 'Content-Type: application/json' -d "$(customer "E2E Invalida $RUN_ID" 11111111111)" | json "d['id']") || fail "criar rascunho"
BODY=$(curl -s -X POST "$ONB/proposals/$BAD/finalize")
echo "$BODY" | grep -q "TaxId must be a valid CPF" || fail "finalize com CPF inválido deveria listar o erro de TaxId: $BODY"
proposal_is "$BAD" Draft || fail "proposta inválida saiu de Draft"
ok "CPF inválido → 400 com o erro de TaxId, proposta continua Draft"

# --------------------------------------------------------------------------- 3
step "Fluxo de APROVAÇÃO: Onboarding → Kafka → Worker → Antifraude → Kafka → Worker → Oracle"
APPROVED_NAME="E2E Aprovada $RUN_ID"
P1=$(new_finalized_proposal "$APPROVED_NAME") || fail "criar/finalizar proposta"
ok "proposta $P1 criada e finalizada"
eventually 60 "análise criada pelo Consumer Proposta Finalizada" has_analysis "$P1"
A1=$(analysis_of "$P1")
ok "análise $A1 apareceu no antifraude (outbox → proposta-conta-finalizada → Worker)"

DECIDED=$(curl -sf -X POST "$AF/analyses/$A1/approve" -H "Authorization: Bearer $TOKEN" | json "d['decidedBy']")
[ "$DECIDED" = "analista" ] || fail "decidedBy=$DECIDED (esperado analista)"
ok "aprovada por 'analista' (usuário do JWT)"
expect_status 409 POST "$AF/analyses/$A1/approve" -H "Authorization: Bearer $TOKEN"
ok "aprovar de novo → 409"

eventually 90 "proposta AccountsRequested" proposal_is "$P1" AccountsRequested
ok "pré-cadastro: Finalized → Approved → AccountsRequested"
accounts() { oracle "select count(*) from ACCOUNTS where HOLDER_NAME = '$APPROVED_NAME';"; }
eventually 90 "4 contas no Oracle" bash -c "[ \"\$(docker exec -i pdi-oracle sqlplus -s pdi/pdi@localhost:1521/FREEPDB1 <<< \"set heading off feedback off pages 0
select count(*) from ACCOUNTS where HOLDER_NAME = '$APPROVED_NAME';\" | tr -d '[:space:]')\" = 4 ]"
PRODUCTS=$(oracle "select listagg(PRODUCT_CODE, ',') within group (order by PRODUCT_CODE) from ACCOUNTS where HOLDER_NAME = '$APPROVED_NAME';")
[ "$PRODUCTS" = "9,10,120,173" ] || fail "produtos=$PRODUCTS"
ok "4 contas no Oracle: produtos $PRODUCTS"

# --------------------------------------------------------------------------- 4
step "Idempotência: replay de criar-conta"
MSG=$(docker exec pdi-kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 \
  --topic criar-conta --from-beginning --timeout-ms 8000 2>/dev/null | grep "$P1" | head -1)
[ -n "$MSG" ] || fail "mensagem de criar-conta não encontrada"
for _ in 1 2; do echo "$MSG" | docker exec -i pdi-kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server localhost:9092 --topic criar-conta; done
eventually 60 "Worker ignorar os 2 replays" bash -c "[ \"\$(docker logs pdi-worker 2>&1 | grep -c '$P1 already has all accounts')\" -ge 2 ]"
[ "$(accounts)" = 4 ] || fail "replay duplicou contas: $(accounts)"
ok "2 replays → 'replay ignored', continuam 4 contas"

# --------------------------------------------------------------------------- 5
step "Fluxo de REJEIÇÃO"
P2=$(new_finalized_proposal "E2E Rejeitada $RUN_ID") || fail "criar/finalizar proposta"
eventually 60 "análise da proposta rejeitada" has_analysis "$P2"
A2=$(analysis_of "$P2")
expect_status 400 POST "$AF/analyses/$A2/reject" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"reason":"x"}'
ok "motivo curto → 400"
expect_status 200 POST "$AF/analyses/$A2/reject" -H "Authorization: Bearer $TOKEN" -H 'Content-Type: application/json' -d '{"reason":"Documento ilegivel"}'
eventually 60 "proposta Rejected" proposal_is "$P2" Rejected
[ "$(curl -sf "$ONB/proposals/$P2" | json "d['rejectionReason']")" = "Documento ilegivel" ] || fail "motivo não chegou ao pré-cadastro"
ok "pré-cadastro: Finalized → Rejected, com o motivo"
[ "$(oracle "select count(*) from ACCOUNTS where HOLDER_NAME = 'E2E Rejeitada $RUN_ID';")" = 0 ] || fail "rejeitada criou conta!"
ok "nenhuma conta criada para a rejeitada"

# --------------------------------------------------------------------------- 6
step "DLQ: mensagem que nunca vai passar"
echo "e2e-poison-$RUN_ID nao e json" | docker exec -i pdi-kafka /opt/kafka/bin/kafka-console-producer.sh --bootstrap-server localhost:9092 --topic proposta-conta-finalizada
in_dlq() {
  docker exec pdi-kafka /opt/kafka/bin/kafka-console-consumer.sh --bootstrap-server localhost:9092 \
    --topic proposta-conta-finalizada.dlq --from-beginning --timeout-ms 5000 --property print.headers=true 2>/dev/null \
    | grep "e2e-poison-$RUN_ID" | grep -q "dlq-reason:Invalid JSON"
}
eventually 60 "poison na DLQ" in_dlq
ok "JSON inválido → proposta-conta-finalizada.dlq com dlq-reason"
eventually 30 "Worker seguir consumindo" bash -c "docker logs pdi-worker 2>&1 | tail -50 | grep -q 'sent to proposta-conta-finalizada.dlq'"

printf '\n'
green "E2E OK: $PASS verificações passaram."
