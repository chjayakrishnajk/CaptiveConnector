#!/bin/bash

# Configuration
CHECK_URL="http://www.google.com"  # URL to check for captive portal
USERNAME="your_username"           # Replace with your username
PASSWORD="your_password"           # Replace with your password
INTERFACE="wlp1s0"                  # Your network interface

# Function to check for captive portal and get the login URL
get_captive_portal_url() {
    REDIRECT_URL=$(curl -s -I "$CHECK_URL" | grep -i "Location" | awk '{print $2}' | tr -d '\r')
    echo "$REDIRECT_URL"
}

# Function to perform login
perform_login() {
    LOGIN_URL=$1
    if [[ -n "$LOGIN_URL" ]]; then
        curl -s -X POST "$LOGIN_URL" \
            -d "username=$USERNAME" \
            -d "password=$PASSWORD"
    else
        echo "No login URL provided."
    fi
}

# Main script
while true; do
    # Check if connected to Wi-Fi
    if nmcli -t -f DEVICE,STATE device | grep -q "^$INTERFACE:connected$"; then
        echo "Connected to Wi-Fi. Checking for captive portal..."
        
        # Get the captive portal URL
        CAPTIVE_PORTAL_URL=$(get_captive_portal_url)
        if [[ -n "$CAPTIVE_PORTAL_URL" ]]; then
            echo "Captive portal detected. Redirect URL: $CAPTIVE_PORTAL_URL"
            echo "Logging in..."
            perform_login "$CAPTIVE_PORTAL_URL"
            echo "Login attempt made. Retrying check in 60 seconds..."
            sleep 60
        else
            echo "No captive portal detected. Checking again in 10 seconds..."
            sleep 10
        fi
    else
        echo "Not connected to Wi-Fi. Checking again in 10 seconds..."
        sleep 10
    fi
done

