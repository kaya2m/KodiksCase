# E-Commerce Backend System - Case Study

> **Case Duration**: 2 Days  
> **Developer**: Muhammet Kaya  
> **Contact**: [Muhammet Kaya](https://muhammetkaya.net/)  
> **Technology Stack**: .NET 9, PostgreSQL, Redis, RabbitMQ, Docker

## 📋 Case Requirements Implementation

This project implements a comprehensive e-commerce backend system as requested in the case study, featuring order processing, message queuing, caching, and notification systems.

### ✅ Completed Requirements

**1. API Layer (.NET Core Web API)**
- ✅ Order creation endpoint with validation
- ✅ JWT-based authentication 
- ✅ Input validation and error handling
- ✅ Swagger documentation

**2. Order Queueing (RabbitMQ)**
- ✅ Order validation and database persistence
- ✅ Event publishing to `order-placed` queue
- ✅ Reliable message delivery

**3. Order Processing Worker**
- ✅ Background service listening to queue
- ✅ Simulated processing with delays
- ✅ Redis logging with timestamps
- ✅ Notification system

**4. Caching Layer (Redis)**
- ✅ User orders caching with 2-minute TTL
- ✅ Cache invalidation on new orders
- ✅ GET `/orders/{userId}` endpoint

**5. Logging & Monitoring**
- ✅ Serilog integration with console/file sinks
- ✅ Correlation ID for request tracing
- ✅ Comprehensive error logging

**6. Security & Code Quality**
- ✅ JWT token-based authentication
- ✅ Input validation and error handling
- ✅ Clean Architecture principles
- ✅ SOLID design patterns

### 🎯 Bonus Features Implemented

- ✅ **Unit & Integration Tests** - Comprehensive test coverage
- ✅ **Swagger UI** - Interactive API documentation
- ✅ **Docker Support** - Full containerization
- ✅ **Health Checks** - System monitoring endpoints
- ✅ **Rate Limiting** - API protection
- ✅ **Performance Monitoring** - Request tracking
- ✅ **Postman Collection** - Ready-to-use API testing collection

## 🚀 Quick Start

### Prerequisites
- Docker & Docker Compose
- .NET 9.0 SDK (optional, for local development)

### 1. Start with Docker (Recommended)
```bash
# Clone repository
git clone https://github.com/kaya2m/KodiksCase.git
cd KodiksCase

# Start all services
docker-compose up -d

# Check health
curl http://localhost:8080/api/health
```

### 2. Access the Application
- **API**: http://localhost:8080
- **Swagger**: http://localhost:8080 (Interactive API documentation)
- **RabbitMQ Management**: http://localhost:15672 (guest/guest)

## 📖 API Usage

### Option 1: Using Postman Collection (Recommended)

A comprehensive Postman collection is included for easy API testing:

```bash
# Import the collection
# File: ECommerce-API-Collection.json
# Location: docs/postman/ECommerce-API-Collection.json
```

**Postman Collection Features:**
- ✅ **Automated Authentication** - Token automatically saved after login
- ✅ **Complete Test Coverage** - All endpoints with validation tests
- ✅ **Error Scenarios** - Comprehensive error handling tests
- ✅ **Load Testing** - Performance testing scenarios
- ✅ **Cache Testing** - Redis caching validation
- ✅ **Background Processing Tests** - Worker service verification

**Import Steps:**
1. Open Postman
2. Click **Import** → **Upload Files**
3. Select `docs/postman/ECommerce-API-Collection.json`
4. Collection will be imported with all variables set

**Test Execution Order:**
1. **Health Check** - Verify services are running
2. **Login** - Get JWT token (automatically saved)
3. **Create Order** - Test order creation
4. **Get Orders** - Test caching functionality
5. **Error Scenarios** - Validate error handling

### Option 2: Manual cURL Commands

#### Authentication
```bash
# Login to get JWT token
curl -X POST http://localhost:8080/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"userId": "user123", "password": "password"}'
```

#### Create Order
```bash
# Create order (requires authentication)
curl -X POST http://localhost:8080/api/orders \
  -H "Authorization: Bearer YOUR_JWT_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "userId": "user123",
    "productId": "product001", 
    "quantity": 2,
    "paymentMethod": 0
  }'
```

#### Get User Orders
```bash
# Get orders for user (cached response)
curl -X GET http://localhost:8080/api/orders/user123 \
  -H "Authorization: Bearer YOUR_JWT_TOKEN"
```

## 🧪 Testing Options

### 1. Automated Testing with Postman
```bash
# Collection Runner (GUI)
1. Import docs/postman/ECommerce-API-Collection.json
2. Right-click collection → "Run collection"
3. Set iterations: 10, delay: 500ms
4. Click "Run E-Commerce API Collection"

# Newman CLI (Command Line)
npm install -g newman
newman run docs/postman/ECommerce-API-Collection.json --iteration-count 10
```

### 2. Unit & Integration Tests
```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/ECommerce.API.Tests

# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
```

**Test Coverage:**
- Unit Tests for Services and Controllers
- Integration Tests for API endpoints
- Authentication and Authorization tests
- Error handling and validation tests
- Postman collection with automated assertions

### 3. Load Testing
```bash
# Using Postman Collection
# Load Test scenarios included in collection
# - Multiple order creation
# - Cache performance testing
# - Concurrent user simulation

# Using Newman for CLI load testing
newman run docs/postman/ECommerce-API-Collection.json \
  --iteration-count 100 \
  --delay-request 100 \
  --reporters cli,html \
  --reporter-html-export load-test-report.html
```

## 🏗️ Architecture

```
┌─────────────────┐    ┌─────────────────┐
│   ECommerce.API │    │ ECommerce.Worker│
│                 │    │                 │
│  • Controllers  │    │ • OrderProcessor│
│  • Middleware   │    │ • RabbitMQ      │
│  • JWT Auth     │    │   Consumer      │
└─────────┬───────┘    └─────────┬───────┘
          │                      │
          ▼                      ▼
┌─────────────────┐    ┌─────────────────┐
│   PostgreSQL    │    │    RabbitMQ     │
│                 │    │                 │
│ • Orders        │    │ • order-placed  │
│ • Logs          │    │   queue         │
└─────────────────┘    └─────────────────┘
          ▲                      ▲
          │                      │
          └──────┐    ┌──────────┘
                 ▼    ▼
          ┌─────────────────┐
          │      Redis      │
          │                 │
          │ • User Orders   │
          │ • Process Logs  │
          └─────────────────┘
```

## 📦 Project Structure

```
KodiksCase/
├── src/
│   ├── ECommerce.API/          # Web API
│   ├── ECommerce.Core/         # Domain Models
│   ├── ECommerce.Application/  # Business Logic
│   ├── ECommerce.Infrastructure/ # Data Access
│   ├── ECommerce.Shared/       # Common Libraries
│   └── ECommerce.Worker/       # Background Service
├── tests/
│   ├── ECommerce.API.Tests/
│   └── ECommerce.Application.Tests/
├── docs/
│   └── postman/
│       └── ECommerce-API-Collection.json  # Postman Collection
├── scripts/
│   └── database/init.sql       # Database Setup
└── docker-compose.yml
```

## 🔧 Configuration

Key configuration files:
- `appsettings.json` - Application settings
- `docker-compose.yml` - Service orchestration
- `scripts/database/init.sql` - Database schema
- `docs/postman/ECommerce-API-Collection.json` - Postman API tests

## 📊 System Monitoring

- **Health Checks**: `/api/health` and `/api/health/detailed`
- **Structured Logging**: JSON formatted logs with correlation IDs
- **Performance Metrics**: Request timing and error tracking
- **Queue Monitoring**: RabbitMQ management interface
- **API Testing**: Comprehensive Postman collection with automated tests

## 🎯 Technical Decisions

**Architecture Choices:**
- **Clean Architecture** for maintainability and testability
- **CQRS Pattern** for command/query separation
- **Repository Pattern** with Unit of Work for data access
- **Event-Driven Architecture** with RabbitMQ for scalability

**Technology Justifications:**
- **PostgreSQL** for reliable ACID transactions
- **Redis** for high-performance caching
- **RabbitMQ** for reliable message queuing
- **JWT** for stateless authentication
- **Docker** for consistent deployment

**Testing Strategy:**
- **Postman Collection** for comprehensive API testing
- **Unit Tests** for business logic validation
- **Integration Tests** for end-to-end scenarios
- **Load Testing** with Newman CLI integration

## 📈 Performance Considerations

- Connection pooling for database efficiency
- Redis caching with appropriate TTL settings
- Async/await throughout for non-blocking operations
- Message acknowledgment for reliable processing
- Index optimization for database queries
- Performance monitoring with Postman collection

## 🔒 Security Features

- JWT token authentication with configurable expiration
- Input validation and sanitization
- SQL injection prevention with EF Core
- CORS configuration for cross-origin requests
- Rate limiting for API protection
- Security testing scenarios in Postman collection

## 📋 Postman Collection Details

The included Postman collection (`docs/postman/ECommerce-API-Collection.json`) provides:

### **Test Categories:**
- 🔐 **Authentication Tests** - Login and token validation
- 📦 **Order Management** - CRUD operations with caching
- 💚 **Health Monitoring** - System status verification  
- ❌ **Error Handling** - Comprehensive error scenarios
- 🚀 **Load Testing** - Performance under load
- 🔄 **Background Processing** - Worker service validation

### **Automated Features:**
- **Token Management** - JWT automatically saved and used
- **Variable Handling** - Dynamic test data generation
- **Response Validation** - Automated assertion testing
- **Performance Tracking** - Response time monitoring
- **Cache Testing** - Redis caching verification
- **Error Logging** - Detailed error reporting

### **Usage Instructions:**
1. Ensure services are running: `docker-compose up -d`
2. Import collection from `docs/postman/ECommerce-API-Collection.json`
3. Run individual requests or entire collection
4. View automated test results and performance metrics
5. Use for development, testing, and demonstration

---

**Case completion time**: 2 days as requested  
**Status**: ✅ All requirements implemented with bonus features  
**Documentation**: Comprehensive API docs, deployment guides, and Postman collection included  
**Testing**: Complete test coverage with automated Postman collection for easy validation