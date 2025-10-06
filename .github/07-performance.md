# 7 – Performance Benchmarking & Optimisation   <!-- Issue #7 -->

**Goal:** Meet latency and throughput targets for production workloads.

## Tasks
- [ ] Benchmark vector-store queries (p95 < 200 ms)
- [ ] Profile hot paths with `dotnet trace`
- [ ] Introduce async pipelines where blocking
- [ ] Add load tests (k6 / NBomber)

## Acceptance Criteria
- Load test passes with target SLA
- Performance report stored in `/docs/perf/`

_Part of #1_