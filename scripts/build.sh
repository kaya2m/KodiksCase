 
#!/bin/bash

set -e

echo "🚀 Starting E-Commerce System Build Process..."

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_message() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# Check if .NET is installed
if ! command -v dotnet &> /dev/null; then
    print_error ".NET SDK is not installed. Please install .NET 9.0 SDK."
    exit 1
fi

# Check .NET version
DOTNET_VERSION=$(dotnet --version)
print_message "Using .NET version: $DOTNET_VERSION"

# Clean previous builds
print_step "Cleaning previous builds..."
dotnet clean
rm -rf ./artifacts
mkdir -p ./artifacts

# Restore dependencies
print_step "Restoring NuGet packages..."
dotnet restore

# Build solution
print_step "Building solution..."
dotnet build --configuration Release --no-restore

# Run tests
print_step "Running unit tests..."
dotnet test --configuration Release --no-build --verbosity normal --collect:"XPlat Code Coverage"

# Check if Docker is available
if command -v docker &> /dev/null; then
    print_step "Building Docker images..."
    
    # Build API image
    print_message "Building E-Commerce API image..."
    docker build -f src/ECommerce.API/Dockerfile -t ecommerce-api:latest . || {
        print_error "Failed to build API Docker image"
        exit 1
    }
    
    # Build Worker image
    print_message "Building E-Commerce Worker image..."
    docker build -f src/ECommerce.Worker/Dockerfile -t ecommerce-worker:latest . || {
        print_error "Failed to build Worker Docker image"
        exit 1
    }
    
    print_message "Docker images built successfully!"
    docker images | grep ecommerce
else
    print_warning "Docker not found. Skipping Docker image build."
fi

# Create deployment package
print_step "Creating deployment package..."
dotnet publish src/ECommerce.API/ECommerce.API.csproj --configuration Release --output ./artifacts/api --no-build
dotnet publish src/ECommerce.Worker/ECommerce.Worker.csproj --configuration Release --output ./artifacts/worker --no-build

# Copy additional files
cp docker-compose.yml ./artifacts/
cp docker-compose.production.yml ./artifacts/
cp -r scripts/ ./artifacts/
cp README.md ./artifacts/

print_message "✅ Build completed successfully!"
print_message "📦 Artifacts available in ./artifacts/ directory"

echo ""
echo "🎯 Next steps:"
echo "  • Run locally: docker-compose up -d"
echo "  • Run tests: dotnet test"
echo "  • Deploy to production: Use artifacts/ directory"