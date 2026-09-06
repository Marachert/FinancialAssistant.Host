export const dashboard = {
  generatedAtUtc: '2026-09-06T12:00:00Z',
  overallStatus: 'degraded',
  dataClassification: 'aggregate-operational-only',
  readiness: {
    componentCount: 5,
    healthyCount: 4,
    degradedCount: 1,
    unavailableCount: 0,
    notConfiguredCount: 0
  },
  services: ['identity', 'receipt-processing', 'ai-orchestration'].map((service, index) => ({
    service,
    status: index === 1 ? 'degraded' : 'healthy',
    latencyMilliseconds: 12 + index * 7,
    checkedAtUtc: '2026-09-06T12:00:00Z',
    errorCategory: index === 1 ? 'timeout' : null
  })),
  rabbitMq: {
    status: 'healthy',
    queueDepth: 4,
    consumerCount: 6
  },
  elasticsearch: {
    status: 'healthy',
    clusterStatus: 'green',
    nodeCount: 1,
    activeShardCount: 12
  },
  metrics: {
    aiUsage: {
      requestCount: 1240,
      successfulRequestCount: 1218,
      inputTokenCount: 78200,
      outputTokenCount: 19400,
      estimatedCostMicros: 145000
    },
    parsingQuality: {
      processedCount: 420,
      successfulCount: 385,
      reviewRequiredCount: 23,
      failedCount: 12,
      successPercent: 91.67
    }
  }
};
export const jobs = {
  generatedAtUtc: '2026-09-06T12:00:00Z',
  capacity: 200,
  retentionHours: 24,
  dataClassification: 'bounded-operational-metadata-only',
  jobs: [{
    operationId: '00000000-0000-4000-8000-000000000001',
    service: 'receipt-processing',
    kind: 'ocr',
    state: 'failed',
    revision: 3,
    observedAtUtc: '2026-09-06T11:59:00Z',
    errorCategory: 'timeout'
  }, {
    operationId: '00000000-0000-4000-8000-000000000002',
    service: 'ai-orchestration',
    kind: 'ai',
    state: 'succeeded',
    revision: 2,
    observedAtUtc: '2026-09-06T11:58:00Z',
    errorCategory: null
  }, {
    operationId: '00000000-0000-4000-8000-000000000003',
    service: 'receipt-processing',
    kind: 'ocr',
    state: 'running',
    revision: 2,
    observedAtUtc: '2026-09-06T11:57:00Z',
    errorCategory: null
  }]
};
export const session = {
  tokenType: 'Bearer',
  accessToken: 'synthetic-access-token-for-tests-only',
  refreshToken: 'synthetic-refresh-token-for-tests-only',
  accessTokenExpiresAtUtc: '2099-01-01T00:00:00Z'
};
