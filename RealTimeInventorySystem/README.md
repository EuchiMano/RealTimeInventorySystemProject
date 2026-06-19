# Real-Time Inventory System

ASP.NET Core 8 Web API para gestión de inventario multi-almacén, extendido con demos aplicados de patrones de arquitectura distribuida.

## Setup

**Requisitos:** .NET 8 SDK · SQL Server · Postman (para los demos)

```bash
dotnet restore
dotnet ef database update   # requiere connection string en user secrets
dotnet run
```

Swagger: `https://localhost:7247/swagger`  
Colección Postman: `RealTimeInventorySystem.Demo.postman_collection.json` (raíz del repo)

---

## API de Inventario

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/inventory/{productId}/warehouses/{warehouseId}` | Stock de un producto en un almacén |
| PATCH | `/api/inventory/{productId}/stock` | Actualizar stock (idempotente, rate-limited) |
| GET | `/api/inventory/product/{productId}` | Stock en todos los almacenes |
| GET | `/api/inventory/warehouse/{warehouseId}` | Inventario completo de un almacén |

Rate limiting: 100 req/min por IP → 429 Too Many Requests.

---

## Demos de Patrones — Semana 1

### 1. Zero-Allocation Span Parser
Parser de líneas CSV usando `ReadOnlySpan<char>` — sin `Split`, sin `Substring`, sin Regex.

| Método | Ruta |
|--------|------|
| POST | `/api/order-import` |
| POST | `/api/order-import/single` |

### 2. Idempotency-Key
Garantiza que la misma orden no se procese dos veces usando `ConcurrentDictionary`.

| Caso | Resultado |
|------|-----------|
| Primera vez con la key | 201 Created |
| Misma key + mismo body | 200 OK (replay) |
| Misma key + body distinto | 409 Conflict |

Endpoint: `POST /api/orders` · Header: `Idempotency-Key: <uuid>`

### 3. Concurrencia Optimista (ETag + If-Match)
Detecta Lost Updates con ETag versionado.

| Método | Ruta |
|--------|------|
| GET | `/api/order-concurrency/{orderId}` |
| PUT | `/api/order-concurrency/{orderId}` |

ETag stale → 412 Precondition Failed.

### 4. Options Pattern
Tres variantes de configuración en caliente.

| Endpoint | Variante | Comportamiento |
|----------|----------|----------------|
| GET `/api/payment-demo/static-config` | `IOptions<T>` | Leído una vez al arrancar |
| GET `/api/payment-demo/feature-flags` | `IOptionsSnapshot<T>` | Recalculado por request |
| POST `/api/payment-demo/charge` | `IOptionsMonitor<T>` | Hot-reload sin reiniciar |

### 5. Outbox Pattern (in-memory)
`ConcurrentQueue` + `BackgroundService` que drena la cola cada 2 segundos.

| Método | Ruta |
|--------|------|
| POST | `/api/outbox-demo/place-order` |
| GET | `/api/outbox-demo/pending` |

---

## Demos de Patrones — Semana 2

### 6. Outbox Pattern con transacción SQL
Demuestra la atomicidad correcta: orden + evento en la **misma transacción DB**.

```
BEGIN TRANSACTION
  INSERT Orders ...
  INSERT OutboxMessages ...    ← mismo commit
COMMIT
```

Si la app cae antes de publicar el evento, el worker lo reintenta al reiniciar.  
Endpoint: `POST /api/sql-outbox-demo/place-order`

### 7. Saga Pattern con compensación
Orquestador in-memory que ejecuta pasos secuenciales y compensa en orden inverso si alguno falla.

| amount | Resultado |
|--------|-----------|
| ≤ 1000 | `Completed` — 4 pasos completados |
| > 1000 | `Compensated` — cobro rechazado → libera inventario → cancela orden |

| Método | Ruta |
|--------|------|
| POST | `/api/saga-demo/checkout` |
| GET | `/api/saga-demo/{sagaId}` |

### 8. Reintentos con Polly
Pipeline: **Retry** (3 reintentos, exponential backoff + jitter) → **Circuit Breaker** (se abre si ≥50% de llamadas fallan).

| failTimes | Resultado |
|-----------|-----------|
| ≤ 3 | Polly recupera → 200 OK |
| > 3 | Agota reintentos → 502 |

Endpoint: `POST /api/retry-demo/charge`

### 9. Azure Service Bus simulado
Topic + 3 subscriptions independientes (Inventory, Billing, Notifications) + Dead Letter Queue.

| Método | Ruta | Acción |
|--------|------|--------|
| POST | `/api/servicebus-demo/publish` | Publica a todos los suscriptores |
| GET | `/api/servicebus-demo/receive/{subscriber}` | Consume el próximo mensaje |
| POST | `/api/servicebus-demo/complete` | Marca como procesado |
| POST | `/api/servicebus-demo/abandon` | Falla entrega (×3 → DLQ) |
| GET | `/api/servicebus-demo/dlq` | Ver Dead Letter Queue |

### 10. Concurrencia Optimista con EF Core
Muestra `IsConcurrencyToken()` sobre el campo `Version` del Inventory real (EF Core + SQL Server).

```sql
-- SQL que genera EF Core internamente:
UPDATE Inventory SET Quantity = @q WHERE Id = @id AND Version = @version
-- 0 rows → DbUpdateConcurrencyException → 409 Conflict
```

| Método | Ruta |
|--------|------|
| GET | `/api/ef-concurrency/inventory/{id}` |
| PUT | `/api/ef-concurrency/inventory/{id}` |

Header requerido: `If-Version: <valor obtenido del GET>`

### 11. Observabilidad
Tres pilares implementados:

- **Logs:** `ILogger` con campos estructurados (`CorrelationId`, `OrderId`, `UserId`, `ElapsedMs`)
- **Traces:** spans por paso (`CreateOrder`, `ReserveInventory`, `ChargePayment`, `SendEmail`) con `ElapsedMs` individual
- **Métricas:** `System.Diagnostics.Metrics` — `Counter` (checkouts iniciados/completados/fallidos) y `Histogram` (duración)
- **CorrelationId middleware:** lee `X-Correlation-Id` del header o genera uno; lo propaga a todos los logs del request

Endpoint: `POST /api/observability-demo/checkout`  
Tip: enviá `X-Correlation-Id: mi-traza-123` para ver el ID propagado en logs y respuesta.

---

## Tecnologías

- ASP.NET Core 8 · Entity Framework Core 8 · SQL Server
- Polly v8 (resiliencia y reintentos)
- System.Diagnostics.Metrics (métricas built-in .NET)
