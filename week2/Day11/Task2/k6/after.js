// k6 load test — AFTER fix (IN-clause endpoint)
//
// Run:
//   k6 run after.js
//
// Compare the p50 and p99 numbers with before.js output.
// Target: p99 at least 4-5x lower than the before run.
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
