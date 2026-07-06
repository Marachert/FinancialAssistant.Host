# Local Docker Compose infrastructure

This folder contains the local infrastructure baseline for the Financial Assistant PoC.

It implements the missing repository changes from FIN-12 and its subtasks:

- FIN-243 — local infrastructure baseline;
- FIN-244 — Elasticsearch local service;
- FIN-245 — RabbitMQ queue service;
- FIN-246 — Redis cache service;
- FIN-247 — MinIO media/object storage dependency;
- FIN-248 — Prometheus/Grafana metrics baseline;
- FIN-249 — local startup guide;
- FIN-250 — local service review checklist.

## Services

| Service | Compose service | Local endpoint | Purpose |
| --- | --- | --- | --- |
| Elasticsearch | `elasticsearch` | `http://localhost:9200` | Service-owned operational indices |
| RabbitMQ | `rabbitmq` | AMQP `localhost:5672`, UI `localhost:15672` | Async event bus |
| Redis | `redis` | `localhost:6379` | Disposable cache and short-lived state |
| MinIO | `minio` | API `localhost:9000`, UI `localhost:9001` | Receipt/file object storage |
| Prometheus | `prometheus` | `http://localhost:9090` | Local metrics |
| Grafana | `grafana` | `http://localhost:3000` | Local dashboards |

## First-time setup

```bash
cd infra/docker-compose
cp .env.example .env
docker compose pull
docker compose up -d
docker compose ps
```

Or use the helper script:

```bash
bash scripts/up.sh
```

## Stop services

```bash
docker compose down
```

Or:

```bash
bash scripts/down.sh
```

## Reset local data

This removes local Docker volumes for the stack.

```bash
docker compose down -v
```

Or:

```bash
bash scripts/reset-local-data.sh
```

## Health checks

Run these checks from the host machine after the stack starts:

```bash
curl http://localhost:9200
curl 'http://localhost:9200/_cluster/health?pretty'
curl http://localhost:9000/minio/health/live
curl http://localhost:9090/-/healthy
curl http://localhost:3000/api/health
docker compose exec redis redis-cli ping
docker compose exec rabbitmq rabbitmq-diagnostics -q ping
```

Or:

```bash
bash scripts/healthcheck.sh
```

## Development rules

- `.env.example` is development-only.
- Do not commit `.env`.
- Do not use real user data, real receipts, raw OCR text, raw AI content, production configuration values, or real financial data in local samples.
- Elasticsearch is shared infrastructure, not a shared data ownership contract.
- Each backend service must own only its own Elasticsearch indices/aliases.
- Redis is disposable cache and must not become source of truth.
- MinIO stores binary file content; service-owned storage stores metadata and workflow state.
- Prometheus/Grafana are observability utilities, not business reporting sources.

## Troubleshooting

| Symptom | Likely cause | Action |
| --- | --- | --- |
| Port already in use | Local service already running | Change the host port in `.env` or stop the conflicting service |
| Elasticsearch exits | Not enough Docker memory | Increase Docker Desktop memory or lower local workload |
| Grafana has no datasource | Provisioning path mismatch | Check `monitoring/grafana/provisioning/datasources/prometheus.yml` |
| Prometheus fails | Missing config file | Check `monitoring/prometheus.yml` |
| Redis command fails | Container not running | Run `docker compose ps redis` |
| RabbitMQ UI unavailable | Container still starting or port conflict | Run `docker compose logs rabbitmq` |
| MinIO health fails | Container still starting or port conflict | Run `docker compose logs minio` |

## Future follow-up

When backend service containers are added, update `monitoring/prometheus.yml` with service `/metrics` scrape targets.
