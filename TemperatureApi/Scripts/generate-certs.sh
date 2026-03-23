#!/bin/bash

# Generate self-signed certificate for local development/testing
# For production, use Let's Encrypt or a trusted CA

CERT_DIR="certs"
CERT_FILE="$CERT_DIR/certificate.crt"
KEY_FILE="$CERT_DIR/certificate.key"
PFX_FILE="$CERT_DIR/certificate.pfx"

# Create certs directory if it doesn't exist
mkdir -p "$CERT_DIR"

# Check if certificates already exist
if [ -f "$CERT_FILE" ] && [ -f "$KEY_FILE" ]; then
    echo "✅ Certificates already exist at $CERT_FILE and $KEY_FILE"
    exit 0
fi

echo "📋 Generating self-signed certificates..."

# Generate private key and certificate
openssl req -new -x509 -newkey rsa:4096 -keyout "$KEY_FILE" -out "$CERT_FILE" \
    -days 365 -nodes \
    -subj "/C=US/ST=State/L=City/O=Organization/OU=IT/CN=localhost"

if [ $? -eq 0 ]; then
    echo "✅ Generated certificate: $CERT_FILE"
    echo "✅ Generated key: $KEY_FILE"
    
    # Generate PFX file for ASP.NET Core (optional)
    if command -v openssl &> /dev/null; then
        openssl pkcs12 -export -out "$PFX_FILE" -inkey "$KEY_FILE" -in "$CERT_FILE" \
            -passout pass:changeit
        echo "✅ Generated PFX file: $PFX_FILE (password: changeit)"
    fi
else
    echo "❌ Failed to generate certificates"
    exit 1
fi

echo ""
echo "📌 Certificate Details:"
openssl x509 -in "$CERT_FILE" -text -noout | grep -E "Subject:|Issuer:|Not Before|Not After"
echo ""
echo "⚠️  WARNING: These are self-signed certificates for development only!"
echo "For production, use Let's Encrypt (certbot) or a trusted certificate authority."
echo ""
echo "For production with Let's Encrypt:"
echo "  1. Install certbot: sudo apt-get install certbot python3-certbot-nginx"
echo "  2. Run: sudo certbot certonly --nginx -d yourdomain.com"
echo "  3. Update NGINX configuration with certificate paths from /etc/letsencrypt/live/"
