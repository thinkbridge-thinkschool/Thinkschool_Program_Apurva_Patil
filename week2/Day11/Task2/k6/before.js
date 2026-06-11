// k6 load test — BEFORE fix (N+1 endpoint)
//
// Run:
//   k6 run k6/before.js
//
// Test scope: Collection-1 has 100 items (N=100).
//   Slow path fires 101 SQL queries per HTTP request:
//   1 SELECT for the collection + 100 individual SELECTs in a foreach loop.
//   At 80 VUs: 80 × 101 = 8080 simultaneous DB queries → queue pressure inflates p99.
//   Target: capture p50 and p99 as baseline before the fix.
//
// Prerequisites:
//   1. App running:          dotnet run (inside Day11/Task2/QuotesApi)
//   2. Database seeded:      POST http://localhost:5182/api/dev/seed
//   3. Verify seed:          GET  http://localhost:5182/api/collections/1/with-quotes-slow
//                            Response should show 50 quotes in the collection.

import http from 'k6/http';
import { check } from 'k6';

export const options = {
    stages: [
        { duration: '10s', target: 80 }, // ramp up to 80 virtual users
        { duration: '30s', target: 80 }, // hold steady — capture p50/p99 here
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
