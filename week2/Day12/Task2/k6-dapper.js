import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    stages: [
        { duration: '5s', target: 20 },   // ramp up to 20 users
        { duration: '30s', target: 20 },  // hold at 20 users
        { duration: '5s', target: 0 },    // ramp down
    ],
    thresholds: {
        http_req_failed: ['rate<0.01'],    // less than 1% errors
        http_req_duration: ['p(95)<500'],  // 95% of requests under 500ms
    },
};

export default function () {
    const res = http.get('http://localhost:5182/api/quotes/dapper?page=1&size=10');

    check(res, {
        'status is 200': (r) => r.status === 200,
        'returned quotes': (r) => JSON.parse(r.body).length > 0,
    });

    sleep(0.1);
}
