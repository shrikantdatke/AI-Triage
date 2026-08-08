#!/bin/bash
# Quick TopDesk connectivity + triage test
# Usage: ./tools/test-triage.sh

BASE_URL="${1:-http://localhost:4280}"

echo "=== AI Triage Test ==="
echo "Target: $BASE_URL"
echo ""

echo "--- Health check ---"
curl -s "$BASE_URL/api/health" | python3 -m json.tool
echo ""

echo "--- Fetch open incidents ---"
curl -s "$BASE_URL/api/incidents" | python3 -m json.tool
echo ""

echo "--- Triage all (AI) ---"
curl -s -X POST "$BASE_URL/api/incidents/triage" \
  -H "Content-Type: application/json" \
  | python3 -m json.tool
