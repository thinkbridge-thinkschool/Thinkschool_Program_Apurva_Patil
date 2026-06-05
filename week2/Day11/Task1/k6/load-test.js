import http from 'k6/http';
import { check } from 'k6';

export const options = {
  scenarios: {
    slow_endpoint: {
      executor: 'constant-vus',
      vus: 10,
      duration: '20s',
      tags: { endpoint: 'slow' },
      exec: 'slowTest',
    },
    fast_endpoint: {
      executor: 'constant-vus',
      vus: 10,
      duration: '20s',
      startTime: '25s',            // runs after slow finishes
      tags: { endpoint: 'fast' },
      exec: 'fastTest',
    },
  },
  thresholds: {
    'http_req_duration{endpoint:slow}': ['p(99)<5000'],
    'http_req_duration{endpoint:fast}': ['p(99)<500'],
  },
};

const BASE = 'http://localhost:5182';

export function slowTest() {
  const res = http.get(`${BASE}/api/collections/1/with-quotes-slow`);
  check(res, { 'slow 200': r => r.status === 200 });
}

export function fastTest() {
  const res = http.get(`${BASE}/api/collections/1/with-quotes-fast`);
  check(res, { 'fast 200': r => r.status === 200 });
}
