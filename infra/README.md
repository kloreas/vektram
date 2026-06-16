# /infra — Infrastructure

Docker, Kubernetes, and Terraform configuration.

## Planned Contents

```
infra/
  docker-compose.dev.yml    Local dev stack (Nakama + CockroachDB)
  docker-compose.test.yml   CI integration-test stack
  k8s/                      Kubernetes manifests (production)
  terraform/                Cloud resource definitions
```

## Local Dev

```bash
docker compose -f infra/docker-compose.dev.yml up
```

Prerequisites: Docker Desktop, `docker compose` v2+.
