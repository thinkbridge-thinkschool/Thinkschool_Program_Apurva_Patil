// k6 load test — BEFORE fix (N+1 endpoint)
//
// Run:
//   k6 run before.js
//
// What to look for in the summary:
//   http_req_duration p(50) and p(99) — baseline latency under 10 VUs
//   http_reqs — total requests; divide by duration for throughput
//
// Prerequisites:
//   1. App running:          dotnet run (inside Day11/Task2/QuotesApi)
//   2. Database seeded:      POST http://localhost:5182/api/dev/seed

import http from 'k6/http';
import { check } from 'k6';

export const options = {
    stages: [
        { duration: '10s', target: 10 }, // ramp up to 10 virtual users
        { duration: '30s', target: 10 }, // hold steady — capture p50/p99 here
        { duration: '10s', target: 0  }, // ramp down
    ],
    thresholds: {
        // Intentionally loose — this endpoint is expected to be slow
        http_req_duration: ['p(50)<10000', 'p(99)<20000'],
        http_req_failed:   ['rate<0.01'],
    },
};

export default function () {
    const res = http.get('http://localhost:5182/api/collections/1/with-quotes-slow', {
        timeout: '30s',
    });

    check(res, {
        'status 200': (r) => r.status === 200,
    });

    // No sleep — back-to-back requests to stress the N+1 path
}
