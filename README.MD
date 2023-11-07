## MSSQL

MSSQL_CONNECTION_STRING=your connection string

## S3

S3_ENDPOINT=
S3_ACCESS_KEY=
S3_SECRET_KEY=
S3_BUCKET_NAME=name optional with tenantplaceholder {0}

## Authentication (general)

If you need any authentication you need to enable it.

USE_AUTHENTICATION=true

## Cookie Authentication (optional)

AUTHENTICATION_SCHEME=Cookies
USE_COOKIE_AUTHENTICATION=true
SLIDING_EXPIRATION = true

None=0 Lax=1 Strict=2
SAME_SITE=1 

HTTP_ONLY=true

SameAsRequest=0 Always=1 None=2
SECURE_POLICY=1
EXPIRE_TIME_SPAN=1:00:00
COOKIE_NAME=.myCookie
DOMAIN=example.com
COOKIE_PATH=/

## Application Options

TENANCY_ENABLED=true
FILE_CACHE_ENABLED=true (not implemented yet)
TENANCY_CLAIM_NAME=tenant_id (default)