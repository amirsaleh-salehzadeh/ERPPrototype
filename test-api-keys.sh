#!/bin/bash

# Bash script to test API key validation pipeline
# Run this script after starting all services

echo "🔑 ERP Prototype API Key Testing Script"
echo "======================================="
echo ""

# Predefined API keys
declare -A api_keys=(
    ["Admin Master"]="0MyvBtNvMQMrfJHZjORFVxjHcUUYEpv5HrOhJBRrhOY"
    ["Dev Team Lead"]="38c_y0McElpnr4iLNVLsR0VjGQuzRlGP-zeCmVIhI6M"
    ["QA Automation"]="91sd4TPkE2fNyxh7xhSBIJt11JciT8bWHQ9aTGQhiAo"
    ["Monitoring Service"]="8Swc7979DTVqEYebKAdpf3xmiUpE9mcOGsy1emvaoNk"
    ["Analytics Dashboard"]="h02zaXOJKTcdmuytRruPhEf8JutxDuhCpmKkVWgheuA"
)

# Test endpoints
endpoints=(
    "http://localhost:5000/api/weather/hello"
    "http://localhost:5000/api/docs/health"
)

echo "🚫 Testing WITHOUT API Key (should fail):"
echo "----------------------------------------"
response=$(curl -s -w "%{http_code}" http://localhost:5000/api/orders/hello)
http_code="${response: -3}"
if [ "$http_code" = "401" ]; then
    echo "✅ EXPECTED: Request failed without API key (HTTP $http_code)"
else
    echo "❌ UNEXPECTED: Request succeeded without API key (HTTP $http_code)"
fi
echo ""

echo "✅ Testing WITH Valid API Keys:"
echo "-------------------------------"

for key_name in "${!api_keys[@]}"; do
    api_key="${api_keys[$key_name]}"
    echo "🔑 Testing with $key_name API Key:"
    
    for endpoint in "${endpoints[@]}"; do
        response=$(curl -s -H "X-API-Key: $api_key" "$endpoint" 2>/dev/null)
        if [ $? -eq 0 ] && [[ $response == *"message"* ]]; then
            service=$(echo "$response" | grep -o '"service":"[^"]*' | cut -d'"' -f4)
            message=$(echo "$response" | grep -o '"message":"[^"]*' | cut -d'"' -f4)
            echo "  ✅ $service - $message"
        else
            echo "  ❌ Failed: $endpoint"
        fi
    done
    echo ""
done

echo "❌ Testing WITH Invalid API Key:"
echo "--------------------------------"
response=$(curl -s -w "%{http_code}" -H "X-API-Key: invalid-key-123" http://localhost:5000/api/orders/hello)
http_code="${response: -3}"
if [ "$http_code" = "401" ]; then
    echo "✅ EXPECTED: Request failed with invalid API key (HTTP $http_code)"
else
    echo "❌ UNEXPECTED: Request succeeded with invalid API key (HTTP $http_code)"
fi
echo ""

echo "🔓 Testing Public Endpoints (no API key required):"
echo "--------------------------------------------------"
response=$(curl -s http://localhost:5000/api/gateway/services 2>/dev/null)
if [ $? -eq 0 ] && [[ $response == *"services"* ]]; then
    service_count=$(echo "$response" | grep -o '"services":\[' | wc -l)
    echo "✅ Gateway Services: Accessible"
else
    echo "❌ Failed to access gateway services"
fi

response=$(curl -s http://localhost:5000/health 2>/dev/null)
if [ $? -eq 0 ] && [[ $response == *"Status"* ]]; then
    echo "✅ Gateway Health: Accessible"
else
    echo "❌ Failed to access gateway health"
fi
echo ""

echo "🎲 Generate More API Keys:"
echo "--------------------------"
response=$(curl -s -X POST http://localhost:5007/seed/random/3 2>/dev/null)
if [ $? -eq 0 ] && [[ $response == *"message"* ]]; then
    message=$(echo "$response" | grep -o '"message":"[^"]*' | cut -d'"' -f4)
    echo "✅ $message"
else
    echo "❌ Failed to generate API keys"
fi
echo ""

echo "🎉 Testing Complete!"
echo "==================="
echo ""
echo "📚 Access Documentation:"
echo "- Scalar (All Services): http://localhost:5002/scalar/all"
echo "- Scalar via Gateway: http://localhost:5000/api/docs/scalar/all"
echo "- Identity Service: http://localhost:5007/swagger"
echo ""
echo "🔑 Your API Keys:"
for key_name in "${!api_keys[@]}"; do
    echo "- $key_name: ${api_keys[$key_name]}"
done
