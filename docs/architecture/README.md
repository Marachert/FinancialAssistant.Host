# Architecture Documentation

This folder contains system-level architecture records and diagrams for Financial Assistant.

## Scope

Architecture documentation should cover:

* system context and client-to-backend flows;
* service boundaries and data ownership;
* Public API Gateway responsibilities;
* synchronous REST and asynchronous RabbitMQ interactions;
* Elasticsearch ownership and read-model boundaries;
* OCR, LLM, notification, object-storage, and observability integrations;
* architecture decisions and trade-offs.

## Rules

Architecture documents must distinguish business capabilities from technical utility components.

They must make clear that:

* backend deterministic logic is authoritative for financial data and calculations;
* LLM and OCR outputs are probabilistic inputs, not financial truth;
* each service owns its storage and business events;
* the gateway is a technical perimeter, not a domain service;
* analytics and reporting models are separate from transactional ownership.

Implementation details that belong to a single service should live near that service or in engineering documentation and be linked from architecture records when relevant.
