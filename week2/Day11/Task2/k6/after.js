// k6 load test — AFTER fix (IN-clause endpoint)
//
// Run:
//   k6 run k6/after.js
//
// Test scope: Collection-1 has 100 items (N=100).
//   Fast path fires ALWAYS 2 SQL queries per HTTP request:
//   1 SELECT for the collection + 1 SELECT WHERE Id IN (...100 ids...).
//   At 80 VUs: 80 × 2 = 160 simultaneous DB queries — bounded even at high concurrency.
//   Same N=100 collection, same VU count, same no-sleep pattern as before.js.
//   Target: p99 >= 10x lower than before.js.
//
// Prerequisites:
//   1. App running:          dotnet run (inside Day11/Task2/QuotesApi)
//   2. Database seeded:      POST http://localhost:5182/api/dev/seed

import http from 'k6/http';
import { check } from 'k6';

export const options = {
    stages: [
        { duration: '10s', target: 80 }, // ramp up to 80 virtual users
        { duration: '30s', target: 80 }, // hold steady — capture p50/p99 here
        { duration: '10s', target: 0  }, // ramp down
    ],
    thresholds: {
        http_req_duration: ['p(50)<500', 'p(99)<1000'],
        http_req_failed:   ['rate<0.01'],
    },
};

export default function () {
    const res = http.get('http://localhost:5182/api/collections/1/with-quotes-fast', {
        timeout: '30s',
    });

    check(res, {
        'status 200': (r) => r.status === 200,
    });

    // No sleep — same load pattern as before.js for a fair comparison
}
