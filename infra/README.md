# Infrastructure

This folder contains the Financial Assistant local platform assets and
repeatable infrastructure bootstrap tooling.

## Assets

- [Docker Compose local stack](docker-compose/README.md) for Elasticsearch,
  RabbitMQ, Redis, MinIO, Prometheus, and Grafana.
- [Elasticsearch sample bootstrap](elasticsearch/bootstrap/README.md) for the
  first Identity Service-owned index template and stable aliases.
- [Windows Server PoC stack](windows-poc/README.md) for the production-like
  single-host service topology, reverse proxy, secrets, and backup/restore.

Infrastructure is shared at the platform level, but data ownership is not.
Each backend service exclusively owns its Elasticsearch namespace, mappings,
indices, aliases, migrations, retention rules, and credentials.

Use development-only configuration and synthetic data. Never commit `.env`
files, credentials, production configuration, user identities, receipts, OCR or
LLM content, or financial records.
