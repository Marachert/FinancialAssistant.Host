# Shared Elasticsearch Utilities

This folder is reserved for reusable technical Elasticsearch integration helpers.

Allowed examples:

* client registration and typed options helpers;
* retry-safe technical policies;
* index-name validation helpers;
* common serialization conventions;
* health-check adapters;
* testable low-level request utilities.

Not allowed:

* shared business repositories;
* service-owned document models or mappings;
* hard-coded cross-service index access;
* financial calculations or categorization rules;
* analytics read models owned by a specific capability;
* credentials, production endpoints, or index contents.

Each service owns its Elasticsearch indices, aliases, mappings, migrations, read/write repositories, retention rules, and data access policy. A shared helper may connect to Elasticsearch, but it may not decide what another service stores or expose that service's documents.
