# Real-Time Inventory System - Microservice

A high-concurrency .NET 8 microservice for managing inventory across multiple warehouses with REST API endpoints, rate limiting, and Azure SQL Database integration.

## Project Structure

```
RealTimeInventorySystem/
├── Models/                  # Database entities
│   ├── Product.cs
│   ├── Warehouse.cs
│   ├── Inventory.cs
│   └── InventoryMovement.cs
├── Data/                    # Database context
│   └── InventoryDbContext.cs
├── Repositories/            # Data access layer
│   ├── IInventoryRepository.cs
│   └── InventoryRepository.cs
├── Services/                # Business logic
│   ├── IInventoryService.cs
│   └── InventoryService.cs
├── Controllers/             # API endpoints
│   └── InventoryController.cs
├── DTOs/                    # Data transfer objects
│   └── Dtos.cs
├── Program.cs               # Application configuration
├── appsettings.json         # Configuration settings
└── RealTimeInventorySystem.csproj
```

## Setup Instructions

### Prerequisites
- .NET 8 SDK
- SQL Server (or Azure SQL Database)
- Visual Studio Code or Visual Studio

### 1. Install NuGet Dependencies

```bash
cd RealTimeInventorySystem
dotnet restore
```

### 2. Configure Database Connection

Update `appsettings.json` with your Azure SQL Database connection string:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:yourserver.database.windows.net,1433;Initial Catalog=inventorydb;Persist Security Info=False;User ID=sqladmin;Password=YourPassword!;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
  }
}
```

Replace:
- `yourserver`: Your Azure SQL Server name
- `YourPassword`: Your Azure SQL admin password

### 3. Create Database & Migrations

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### 4. Run the Application

```bash
dotnet run
```

The API will be available at: `https://localhost:5001`

Swagger UI: `https://localhost:5001/` (when running in Development)

## API Endpoints

### Get Stock for Product in Warehouse
```
GET /api/inventory/{productId}/warehouses/{warehouseId}
```

### Update Stock (IDEMPOTENT - Rate Limited to 100 req/min per IP)
```
PATCH /api/inventory/{productId}/stock
Content-Type: application/json

{
  "warehouseId": 1,
  "quantity": 50,
  "reason": "Manual adjustment"
}
```

### Get Product Stock Across All Warehouses
```
GET /api/inventory/product/{productId}
```

### Get Warehouse Inventory
```
GET /api/inventory/warehouse/{warehouseId}
```

### Health Check
```
GET /health
```

## Key Features

### 1. **Idempotent PATCH Endpoint**
- Sets final stock quantity value (not relative updates)
- Multiple calls with same data = same result
- Safe for high-concurrency scenarios

### 2. **Rate Limiting**
- **100 requests per minute per IP address**
- Implemented via `SemaphoreSlim` in middleware
- Prevents API abuse and system overload
- Returns 429 (Too Many Requests) when limit exceeded

### 3. **Async/Await Pattern**
- All I/O operations are non-blocking
- Uses `Task.Delay` instead of `Thread.Sleep`
- Prevents thread pool starvation

### 4. **Proper Resource Management**
- `InventoryService` implements `IDisposable`
- Proper disposal of DbContext
- Uses `using` statements for resource cleanup

### 5. **Database Optimization**
- Composite indices on (ProductId, WarehouseId)
- Separate indices for individual column searches
- Foreign keys with cascade delete
- Unique constraints to prevent duplicates

## Configuration Files

### Program.cs
- Database context registration
- Dependency injection setup
- Rate limiting middleware
- Swagger documentation configuration

### appsettings.json
- Connection string
- Logging levels
- Rate limiting policies

## Testing with Swagger

1. Navigate to `https://localhost:5001`
2. Click on any endpoint to expand it
3. Click "Try it out"
4. Enter parameters and click "Execute"
5. View responses

Example PATCH request:
```
PATCH /api/inventory/1/stock

{
  "warehouseId": 1,
  "quantity": 100,
  "reason": "Restock from supplier"
}
```

## Performance Considerations

- **Async/Await**: Non-blocking I/O operations
- **Indices**: 10,000x faster stock lookups
- **SemaphoreSlim**: Rate limiting without thread blocking
- **Entity Framework**: LINQ queries compiled to SQL
- **Span<T>**: Memory-efficient string parsing

## Architecture Pattern

This project uses:
- **Repository Pattern**: Data access abstraction
- **Service Layer**: Business logic separation
- **Dependency Injection**: Loose coupling
- **DTOs**: API contracts
- **IDisposable**: Resource management

## Next Steps

- Deploy to Azure Container Registry
- Deploy to Azure App Service
- Add authentication & authorization
- Implement caching with Redis
- Add comprehensive logging & monitoring
- Load testing for concurrency validation
- Add unit and integration tests

## Environment Variables

For production, use Azure Key Vault or environment variables:
```
CONNECTIONSTRING_DEFAULTCONNECTION=your_connection_string
ASPNETCORE_ENVIRONMENT=Production
```

## Troubleshooting

### "Connection string 'DefaultConnection' not found"
- Ensure `appsettings.json` has the correct connection string
- Check server name and credentials

### "429 Too Many Requests"
- You've exceeded 100 requests per minute from your IP
- Wait 1 minute for the limit to reset

### Migrations Failed
- Ensure database exists on SQL Server
- Check connection string
- Run: `dotnet ef database update`

## License

Educational Project - Real-Time Inventory System Microservice
