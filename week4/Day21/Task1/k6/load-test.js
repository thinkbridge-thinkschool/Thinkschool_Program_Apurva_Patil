import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    vus: 50,
    duration: '10s',
};

const BASE_URL = 'http://localhost:5255';

// setup() runs once before the test; return value is passed to every VU as `data`.
export function setup() {
    const res = http.post(
        `${BASE_URL}/auth/token`,
        JSON.stringify({ userId: 'k6-user', scopes: [] }),
        { headers: { 'Content-Type': 'application/json' } }
    );
    if (res.status !== 200) {
        throw new Error(`Token request failed: ${res.status} ${res.body}`);
    }
    return { token: res.json('accessToken') };
}

export default function (data) {
    const res = http.get(`${BASE_URL}/api/quotes?page=1&size=10`, {
        headers: { Authorization: `Bearer ${data.token}` },
    });

    check(res, { 'status 200': (r) => r.status === 200 });
}
