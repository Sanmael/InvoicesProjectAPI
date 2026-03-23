#!/bin/sh
set -e

echo "==> Running database migrations..."
./efbundle --connection "$ConnectionStrings__DefaultConnection"

echo "==> Migrations complete. Starting application..."
exec dotnet InvoicesProjectAPI.dll
