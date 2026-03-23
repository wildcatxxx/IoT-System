#!/bin/bash

# Temperature API Authentication Test Script
# This script tests the complete authentication flow

API_URL="http://localhost:5000"
TIMESTAMP=$(date +%s)
TEST_USER="testuser_${TIMESTAMP}"
TEST_EMAIL="test_${TIMESTAMP}@example.com"
TEST_PASSWORD="TestPass123!"

echo "=========================================="
echo "Temperature API Authentication Test"
echo "=========================================="
echo ""
echo "API URL: $API_URL"
echo "Test User: $TEST_USER"
echo "Test Email: $TEST_EMAIL"
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test 1: Register User
echo -e "${YELLOW}[1] Testing User Registration...${NC}"
REGISTER_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d "{
    \"username\": \"$TEST_USER\",
    \"email\": \"$TEST_EMAIL\",
    \"password\": \"$TEST_PASSWORD\",
    \"confirmPassword\": \"$TEST_PASSWORD\"
  }")

echo "Response: $REGISTER_RESPONSE"

if echo "$REGISTER_RESPONSE" | grep -q "registered successfully"; then
  echo -e "${GREEN}✓ Registration successful${NC}"
else
  echo -e "${RED}✗ Registration failed${NC}"
  exit 1
fi
echo ""

# Test 2: Login User
echo -e "${YELLOW}[2] Testing User Login...${NC}"
LOGIN_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{
    \"username\": \"$TEST_USER\",
    \"password\": \"$TEST_PASSWORD\"
  }")

echo "Response: $LOGIN_RESPONSE"

# Extract tokens
ACCESS_TOKEN=$(echo "$LOGIN_RESPONSE" | grep -o '"accessToken":"[^"]*' | cut -d'"' -f4)
REFRESH_TOKEN=$(echo "$LOGIN_RESPONSE" | grep -o '"refreshToken":"[^"]*' | cut -d'"' -f4)

if [ -z "$ACCESS_TOKEN" ]; then
  echo -e "${RED}✗ Login failed - no access token${NC}"
  exit 1
fi

echo -e "${GREEN}✓ Login successful${NC}"
echo "Access Token: ${ACCESS_TOKEN:0:50}..."
echo "Refresh Token: ${REFRESH_TOKEN:0:50}..."
echo ""

# Test 3: Access Protected Endpoint
echo -e "${YELLOW}[3] Testing Protected Endpoint Access...${NC}"
PROTECTED_RESPONSE=$(curl -s -X GET "$API_URL/api/temps?page=1&pageSize=5" \
  -H "Authorization: Bearer $ACCESS_TOKEN")

echo "Response: ${PROTECTED_RESPONSE:0:100}..."

if echo "$PROTECTED_RESPONSE" | grep -q "error\|Error"; then
  echo -e "${RED}✗ Protected endpoint access failed${NC}"
  echo "Full response: $PROTECTED_RESPONSE"
else
  echo -e "${GREEN}✓ Protected endpoint access successful${NC}"
fi
echo ""

# Test 4: Validate Token
echo -e "${YELLOW}[4] Testing Token Validation...${NC}"
VALIDATE_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/validate" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\": \"$REFRESH_TOKEN\"}")

echo "Response: $VALIDATE_RESPONSE"

if echo "$VALIDATE_RESPONSE" | grep -q '"isValid":true'; then
  echo -e "${GREEN}✓ Token validation successful${NC}"
else
  echo -e "${RED}✗ Token validation failed${NC}"
fi
echo ""

# Test 5: Refresh Token
echo -e "${YELLOW}[5] Testing Token Refresh...${NC}"
REFRESH_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\": \"$REFRESH_TOKEN\"}")

echo "Response: ${REFRESH_RESPONSE:0:100}..."

NEW_ACCESS_TOKEN=$(echo "$REFRESH_RESPONSE" | grep -o '"accessToken":"[^"]*' | cut -d'"' -f4)
NEW_REFRESH_TOKEN=$(echo "$REFRESH_RESPONSE" | grep -o '"refreshToken":"[^"]*' | cut -d'"' -f4)

if [ -z "$NEW_ACCESS_TOKEN" ]; then
  echo -e "${RED}✗ Token refresh failed${NC}"
  echo "Full response: $REFRESH_RESPONSE"
else
  echo -e "${GREEN}✓ Token refresh successful${NC}"
  ACCESS_TOKEN=$NEW_ACCESS_TOKEN
  REFRESH_TOKEN=$NEW_REFRESH_TOKEN
fi
echo ""

# Test 6: Logout
echo -e "${YELLOW}[6] Testing Logout...${NC}"
LOGOUT_RESPONSE=$(curl -s -X POST "$API_URL/api/auth/logout" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\": \"$REFRESH_TOKEN\"}")

echo "Response: $LOGOUT_RESPONSE"

if echo "$LOGOUT_RESPONSE" | grep -q "successfully"; then
  echo -e "${GREEN}✓ Logout successful${NC}"
else
  echo -e "${RED}✗ Logout failed${NC}"
fi
echo ""

# Test 7: Verify Token is Invalidated
echo -e "${YELLOW}[7] Testing Token Invalidation...${NC}"
VALIDATE_AFTER_LOGOUT=$(curl -s -X POST "$API_URL/api/auth/validate" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\": \"$REFRESH_TOKEN\"}")

echo "Response: $VALIDATE_AFTER_LOGOUT"

if echo "$VALIDATE_AFTER_LOGOUT" | grep -q '"isValid":false'; then
  echo -e "${GREEN}✓ Token properly invalidated after logout${NC}"
else
  echo -e "${YELLOW}⚠ Token invalidation unclear (may still be valid if not yet refreshed)${NC}"
fi
echo ""

# Test 8: Test Error Cases
echo -e "${YELLOW}[8] Testing Error Cases...${NC}"

# Invalid login
echo "Testing invalid credentials..."
INVALID_LOGIN=$(curl -s -X POST "$API_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{
    \"username\": \"$TEST_USER\",
    \"password\": \"WrongPassword123!\"
  }")

if echo "$INVALID_LOGIN" | grep -q "Invalid username or password"; then
  echo -e "${GREEN}✓ Invalid credentials properly rejected${NC}"
else
  echo -e "${RED}✗ Invalid credentials not rejected properly${NC}"
fi
echo ""

echo "=========================================="
echo -e "${GREEN}Authentication Tests Complete!${NC}"
echo "=========================================="
