#!/bin/bash

# Test script for problematic records

RECORDS=(
    "Adshade Wilfred 231 Westminster Av Riverview 386-8496"
    "Adsett Robert B 210 Country View Rd 384-8266"
    "Adesina A Mountain Rd 830-7098"
    "Adesina 0 382-4955"
    "Adeoye Haqq 183 Oakfield Dr 386-4519"
    "Adebukola Bada 115 Lorne St 388-2674"
    "Addington Andrea 26 Fairlane Dr 851-1340"
    "Adao Aurora 858-8912"
    "Adams Walter 88 Sunset Rd 853-4115"
    "Adams Todd 1009 Cleveland Av 853-8202"
    "Adams T 925 Elmwood Dr 204-4110"
    "Adams S Grindstone Dr 854-8144"
    "Adams Nick 7 Parkwood Av 533-7099"
    "Adams M 97 Notingham Dr 204-8479"
    "Adams Jessica 19 Naples Dr 854-1588"
    "Adams D 115 Chateau Dr 857-0697"
    "Adair A 53 Dundee Dr 372-9518"
    "Ackman F Douglas Dr Cap - Brûlé 532-3836"
    "Abli R A22 21 Arden St 830-8056"
    "Aberathna B 150 Wynwood Dr 830-7163"
    "Abdelhadi Z 152 Holland Dr 830-2167"
)

LOG_FILE="debug_classification_log.txt"

echo "=== DEBUG TEST RUN - $(date '+%Y-%m-%d %H:%M:%S') ===" > "$LOG_FILE"
echo "" >> "$LOG_FILE"

for record in "${RECORDS[@]}"; do
    echo ""
    echo "================================================================================"
    echo "Testing: $record"
    echo "================================================================================"
    
    echo "================================================================================" >> "$LOG_FILE"
    echo "INPUT: $record" >> "$LOG_FILE"
    echo "================================================================================" >> "$LOG_FILE"
    
    # Create JSON payload
    JSON_DATA=$(cat <<EOF
{
    "Input": "$record",
    "Province": "NB",
    "AreaCode": "506"
}
EOF
    )
    
    # Make API call
    RESPONSE=$(curl -s -X POST http://localhost:5000/api/Parser/parse \
        -H "Content-Type: application/json" \
        -d "$JSON_DATA")
    
    # Parse response
    if [ ! -z "$RESPONSE" ]; then
        IS_BUSINESS=$(echo "$RESPONSE" | grep -o '"isBusinessName":[^,]*' | cut -d: -f2)
        NAME=$(echo "$RESPONSE" | grep -o '"name":"[^"]*' | cut -d'"' -f4)
        ADDRESS=$(echo "$RESPONSE" | grep -o '"address":"[^"]*' | cut -d'"' -f4)
        PHONE=$(echo "$RESPONSE" | grep -o '"phone":"[^"]*' | cut -d'"' -f4)
        
        if [ "$IS_BUSINESS" == "true" ]; then
            echo "Result: BUSINESS"
            echo "RESULT: BUSINESS" >> "$LOG_FILE"
        else
            echo "Result: RESIDENTIAL"
            echo "RESULT: RESIDENTIAL" >> "$LOG_FILE"
        fi
        
        echo "  Name: $NAME"
        echo "  Address: $ADDRESS"
        echo "  Phone: $PHONE"
        
        echo "  Name: $NAME" >> "$LOG_FILE"
        echo "  Address: $ADDRESS" >> "$LOG_FILE"
        echo "  Phone: $PHONE" >> "$LOG_FILE"
        echo "" >> "$LOG_FILE"
    else
        echo "ERROR: No response from server"
        echo "ERROR: No response from server" >> "$LOG_FILE"
    fi
done

echo ""
echo "Debug log written to: $LOG_FILE"