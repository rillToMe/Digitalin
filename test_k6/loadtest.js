import http from 'k6/http';
import { check, sleep } from 'k6';

// 1. Konfigurasi Skenario
export const options = {
  stages: [
    { duration: '10s', target: 20 },  // Naikkan dari 0 ke 20 user dalam 10 detik
    { duration: '20s', target: 20 },  // Tahan di 20 user selama 20 detik (peak usage)
    { duration: '10s', target: 0 },   // Turunkan kembali ke 0 user dalam 10 detik
  ],
  thresholds: {
    http_req_failed: ['rate<0.01'],   // Error harus di bawah 1% (jika >1%, tes dianggap gagal)
    http_req_duration: ['p(95)<500'], // 95% request harus di bawah 500ms
  },
};

// 2. Apa yang Dilakukan Setiap Client (Virtual User)
export default function () {
  const url = 'http://localhost:5173/api/v1/topup'; // sesuaikan route API kamu
  const payload = JSON.stringify({
    user_id: '123456',
    zone_id: '9999',
    item_id: 'diamonds_100',
    payment_method: 'xendit_va'
  });

  const params = {
    headers: { 'Content-Type': 'application/json' },
  };

  const res = http.post(url, payload, params);
  
  check(res, { 'status sukses': (r) => r.status === 200 || r.status === 201 });
  sleep(1);
}