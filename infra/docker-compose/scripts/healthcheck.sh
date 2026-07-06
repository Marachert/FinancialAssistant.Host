#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

cd "${COMPOSE_DIR}"

printf '\nDocker Compose services:\n'
docker compose ps

printf '\nElasticsearch:\n'
curl -fsS http://localhost:9200 >/dev/null && echo "OK"

printf 'Elasticsearch cluster health:\n'
curl -fsS 'http://localhost:9200/_cluster/health?pretty'

printf '\nRedis:\n'
docker compose exec redis redis-cli ping

printf '\nMinIO:\n'
curl -fsS http://localhost:9000/minio/health/live >/dev/null && echo "OK"

printf 'Prometheus:\n'
curl -fsS http://localhost:9090/-/healthy >/dev/null && echo "OK"

printf 'Grafana:\n'
curl -fsS http://localhost:3000/api/health >/dev/null && echo "OK"

printf '\nRabbitMQ:\n'
docker compose exec rabbitmq rabbitmq-diagnostics -q ping
