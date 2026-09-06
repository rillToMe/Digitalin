#!/usr/bin/env bash
# Smoke regression — Phase 0 gate (Clean Architecture migration).
# Menjalankan server Development di localhost:5000 lalu memverifikasi:
#   public endpoints, auth 401/403, error shapes ({message}, empty 4xx, ProblemDetails),
#   rate limit 429. Exit code 0 = semua hijau.
#
# Usage:
#   BASE=http://localhost:5000 bash docs/regression/smoke.sh
set -u
BASE="${BASE:-http://localhost:5000}"
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
PASS=0
FAIL=0

check() {
    local desc="$1" expected="$2" actual="$3"
    if [ "$expected" = "$actual" ]; then
        PASS=$((PASS + 1))
        echo "PASS  [$expected] $desc"
    else
        FAIL=$((FAIL + 1))
        echo "FAIL  [expected $expected, got $actual] $desc"
    fi
}

body() { curl -s -w "\n%{http_code}" "$@"; }
code_of() { echo "$1" | tail -1; }

ADMIN_TOKEN=$(python "$ROOT/docs/regression/mint_jwt.py" --role ADMIN)
SUPER_TOKEN=$(python "$ROOT/docs/regression/mint_jwt.py" --role SUPER_ADMIN)

echo "=== Public ==="
check "ping-db" 200 "$(code_of "$(body "$BASE/ping-db")")"
check "banners" 200 "$(code_of "$(body "$BASE/api/v1/banners")")"
check "search games" 200 "$(code_of "$(body "$BASE/api/v1/search/games?q=ml")")"
check "games brands" 200 "$(code_of "$(body "$BASE/api/v1/games/brands")")"
check "check-email blank -> {message}" 400 "$(code_of "$(body "$BASE/api/v1/auth/check-email?email=")")"

echo "=== Auth / Authorization ==="
check "profile tanpa token -> 401" 401 "$(code_of "$(body "$BASE/api/v1/auth/profile")")"
check "admin banners tanpa token -> 401" 401 "$(code_of "$(body "$BASE/api/v1/admin/banners")")"
check "admin banners dengan ADMIN -> 200" 200 "$(code_of "$(body -H "Authorization: Bearer $ADMIN_TOKEN" "$BASE/api/v1/admin/banners")")"
check "admin admins dengan ADMIN -> 403 (SuperAdminOnly)" 403 "$(code_of "$(body -H "Authorization: Bearer $ADMIN_TOKEN" "$BASE/api/v1/admin/admins")")"
check "admin admins dengan SUPER_ADMIN -> 200" 200 "$(code_of "$(body -H "Authorization: Bearer $SUPER_TOKEN" "$BASE/api/v1/admin/admins")")"

echo "=== Error shapes ==="
VOUCHER_404=$(body -H "Authorization: Bearer $ADMIN_TOKEN" "$BASE/api/v1/admin/vouchers/999999")
check "voucher missing -> 404 {message}" 404 "$(code_of "$VOUCHER_404")"
check "voucher missing body = {message}" "{message}" "$(echo "$VOUCHER_404" | head -1 | python -c "import sys,json;d=json.load(sys.stdin);print('{message}' if isinstance(d,dict) and 'message' in d else 'NO')")"
MARGIN_404=$(body -X PUT -H "Authorization: Bearer $ADMIN_TOKEN" -H 'Content-Type: application/json' -d '{"margin":10}' "$BASE/api/v1/admin/products/999999/margin")
check "margin missing product -> 404 empty body" 404 "$(code_of "$MARGIN_404")"
check "margin missing body kosong" "EMPTY" "$(echo "$MARGIN_404" | head -1 | python -c "import sys;d=sys.stdin.read();print('EMPTY' if not d.strip() else 'NONEMPTY')")"
RETRY_404=$(body -X POST -H "Authorization: Bearer $ADMIN_TOKEN" "$BASE/api/v1/admin/orders/NOT-EXIST-999/retry")
check "order retry missing -> 404 {message}" 404 "$(code_of "$RETRY_404")"
WEBHOOK_401=$(body -X POST -H 'Content-Type: application/json' -H 'X-Signature: wrong' -d '{}' "$BASE/webhooks/digiflazz")
check "webhook digiflazz signature salah -> 401" 401 "$(code_of "$WEBHOOK_401")"
WEBHOOK_M401=$(body -X POST -H 'Content-Type: application/json' -d '{"order_id":"NOT-EXIST","status_code":"200","gross_amount":"10000.00","transaction_status":"settlement","fraud_status":"accept","signature_key":"wrong"}' "$BASE/webhooks/midtrans")
check "webhook midtrans signature salah -> 401" 401 "$(code_of "$WEBHOOK_M401")"

echo "=== Rate limit (429) ==="
RL_LAST=0
for i in 1 2 3 4 5 6; do
    RL_LAST=$(code_of "$(body -X POST -H 'Content-Type: application/json' -d '{"username":"x","password":"y"}' "$BASE/api/v1/admin/auth/login")")
done
check "admin login x6 -> 429" 429 "$RL_LAST"
RL_BODY=$(body -X POST -H 'Content-Type: application/json' -d '{"username":"x","password":"y"}' "$BASE/api/v1/admin/auth/login")
check "429 body = {message}" "{message}" "$(echo "$RL_BODY" | head -1 | python -c "import sys,json;d=json.load(sys.stdin);print('{message}' if isinstance(d,dict) and 'message' in d else 'NO')")"

echo ""
echo "=== RESULT: PASS=$PASS FAIL=$FAIL ==="
[ "$FAIL" -eq 0 ] && exit 0 || exit 1