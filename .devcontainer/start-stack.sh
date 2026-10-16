#!/usr/bin/env bash
# Sobe a stack inteira no Codespace. Tenta usar as imagens já publicadas no GHCR
# (sobe em ~2 min); se não conseguir baixar, builda localmente (~5 min).
set -uo pipefail
cd "$(dirname "$0")/.."

if [ -n "${GITHUB_REPOSITORY:-}" ]; then
  export IMAGE_PREFIX="ghcr.io/${GITHUB_REPOSITORY,,}"
  export IMAGE_TAG=latest
  echo "▸ Baixando imagens de ${IMAGE_PREFIX}..."
  if docker compose --profile app pull --quiet onboarding-api antifraude-api worker web; then
    docker compose --profile app up -d --no-build
  else
    echo "▸ Imagens indisponíveis no GHCR; buildando localmente."
    unset IMAGE_PREFIX IMAGE_TAG
    docker compose --profile app up -d --build
  fi
else
  docker compose --profile app up -d --build
fi

cat <<MSG

  Stack subindo (o Oracle leva ~1-2 min na primeira vez).
  Acompanhe:   docker compose --profile app ps
  Tela:        aba PORTS → 8080   (login: analista / analista123)
  Swagger:     portas 5003 e 5004 → /swagger
  Testar tudo: ./scripts/e2e.sh

MSG
