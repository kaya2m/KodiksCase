#!/usr/bin/env pwsh

Write-Host "Building E-Commerce Docker Images..." -ForegroundColor Green

# Build API image
Write-Host "Building E-Commerce API image..." -ForegroundColor Yellow
docker build -f src/ECommerce.API/Dockerfile -t ecommerce-api:latest .

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build API image" -ForegroundColor Red
    exit 1
}

# Build Worker image
Write-Host "Building E-Commerce Worker image..." -ForegroundColor Yellow
docker build -f src/ECommerce.Worker/Dockerfile -t ecommerce-worker:latest .

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to build Worker image" -ForegroundColor Red
    exit 1
}

Write-Host "Docker images built successfully!" -ForegroundColor Green

# Show images
Write-Host "`nCreated images:" -ForegroundColor Cyan
docker images | Select-String "ecommerce"