# Booking Management Service

A robust, production-grade backend service built with **.NET 10** for managing bookings of shared resources such as meeting rooms, desks, and equipment.

The system provides RESTful APIs for managing users, resources, and bookings, featuring strict boundary validation, soft cancellation, high-efficiency indexing, and rigorous concurrency protection against double bookings.

---

## 🚀 Tech Stack

- **Framework**: .NET 10 Web API (ASP.NET Core 10)
- **Data Access & ORM**: Entity Framework Core 10
- **Database**: Microsoft SQL Server / LocalDB
- **Testing**: xUnit, EF Core InMemory (Unit Tests), SQL Server LocalDB with `Task.WhenAll` (Relational Concurrency Tests)
- **Frontend Dashboard**: HTML5, CSS3, Vanilla JavaScript (ES6) with XSS sanitization
- **API Documentation**: Microsoft OpenAPI / Swagger
- **Architecture**: 3-Tier Layered Architecture (Controllers ➔ Services ➔ Data Layer)

---

## 🏗️ Architecture & Clean Design

The project strictly adheres to a **3-Tier Layered Architecture** with clean separation of concerns and Dependency Injection:

```text
HTTP Clients (Frontend / Swagger / cURL)
                     │
                     ▼
             Controllers Layer
       (HTTP Status Codes, Routing, Location Headers)
                     │
                     ▼
             Service Interfaces
      (IBookingService, IResourceService, IUserService)
                     │
                     ▼
          Application Services Layer
     (Business Rules, Overlap Checks, Bounded Deadlock Retry)
                     │
                     ▼
            Data Layer (EF Core)
        (AppDbContext, Indexes, Migrations)
                     │
                     ▼
               SQL Server
```

### Key Components

- **Controllers**: Thin controllers responsible only for HTTP protocol concerns, route resolution (`CreatedAtAction` with valid `Location` headers), request validation, and status code dispatching.
- **Interfaces**: Clear abstraction boundaries allowing mockability and decoupled testing.
- **Services**: Pure business logic containing booking rules, time validation, slot collision algorithms, and transient concurrency retry.
- **Middleware**: `GlobalExceptionHandlingMiddleware` guarantees centralized, consistent JSON error responses across all endpoints without leaking internal stack traces.
- **Data**: `AppDbContext` configuring relational foreign keys (`DeleteBehavior.Restrict`), string length boundaries, and composite indexes.

---

## 📅 Booking Invariants & Overlap Logic

A resource cannot have two active bookings that collide in time.

### Mathematical Definition of Overlap

Booking intervals are treated as **half-open intervals** $[Start, End)$.

A new booking candidate $[S_{new}, E_{new})$ collides with an existing active booking $[S_{old}, E_{old})$ if and only if:
$$\text{Start}_{new} < \text{End}_{old} \quad \land \quad \text{End}_{new} > \text{Start}_{old}$$

```text
Overlapping Booking Example (REJECTED — 409 Conflict):
10:00 ────────────── 12:00  [Existing Active Booking]
       11:00 ────────────── 13:00  [New Request -> REJECTED]

Back-to-Back Booking Example (ALLOWED — 201 Created):
10:00 ─────── 11:00  [Existing Booking]
              11:00 ─────── 12:00  [New Request -> ALLOWED: 11:00 < 11:00 is False]
```

### Core Business Rules
1. **Time Order**: `StartDateTime < EndDateTime` is strictly enforced.
2. **Entity Existence**: Both `ResourceId` and `UserId` must exist in the database.
3. **Cancelled Bookings**: Cancelled bookings (`Status = Cancelled`) do not block availability. Slots become available immediately upon cancellation.
4. **Independent Resources**: Multiple resources can be booked for the exact same time slot without collision.
5. **Auditing**: All timestamps (`CreatedAt`, `StartDateTime`, `EndDateTime`) are tracked in UTC.
6. **Soft Cancellation**: Bookings are never physically deleted (`DELETE /api/Bookings/{id}` updates status to `Cancelled`), preserving historical audit trails. Repeated cancellation is idempotent.

---

## ⚡ Concurrency & Deadlock Resiliency

### The Concurrency Challenge
When multiple concurrent requests attempt to book the same resource for an overlapping time slot in a millisecond window, application-level `AnyAsync()` checks in default `ReadCommitted` isolation are prone to **phantom reads** and race conditions where both requests see the slot as open and both insert duplicate bookings.

### Serializable Isolation & SQL Server Behavior
To guarantee absolute correctness, `BookingService.CreateBookingAsync` executes inside a database transaction with `IsolationLevel.Serializable`.

In SQL Server, `Serializable` transactions acquire Range Key locks (`RangeS-S`). When two concurrent transactions read the same range simultaneously and subsequently attempt to insert, SQL Server detects lock contention and selects one transaction as a **deadlock victim** (`SqlException` Error 1205).

### Bounded Retry Strategy
Rather than blindly misclassifying transient database deadlocks as domain booking conflicts, `BookingService` implements a **bounded retry mechanism** (up to 3 attempts with exponential jitter backoff):
1. **Attempt 1**: Two concurrent requests execute. One transaction commits successfully. The other encounters a transient lock contention / deadlock.
2. **Transient Detection**: `IsDeadlockException` inspects the entire exception hierarchy. If Error 1205 is detected, the transaction rolls back, clears EF Core's change tracker, and waits briefly.
3. **Retry**: The retrying transaction re-executes. Because Transaction 1 committed, the business overlap query (`AnyAsync(...)`) detects the now-committed booking and raises a clean `BookingConflictException`.
4. **Domain Conflict Mapping**: The middleware translates `BookingConflictException` into an accurate `409 Conflict` HTTP response.
5. **Exhausted Deadlocks**: If contention cannot be resolved after all retries, a `ConcurrencyException` is thrown and mapped to `503 Service Unavailable`, guaranteeing system stability.

---

## 📡 REST API Endpoints

### 1. Users (`/api/Users`)

| Method | Endpoint | Description | Status Code |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/Users` | Retrieve all registered users | `200 OK` |
| `GET` | `/api/Users/{id}` | Retrieve single user by ID | `200 OK` / `404 Not Found` |
| `POST` | `/api/Users` | Create a new user | `201 Created` (with `Location` header) |

**Sample POST `/api/Users` Request:**
```json
{
  "name": "Sanad Rahahleh",
  "email": "sanad@example.com"
}
```

---

### 2. Resources (`/api/Resources`)

| Method | Endpoint | Description | Status Code |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/Resources` | Retrieve all shared resources | `200 OK` |
| `GET` | `/api/Resources/{id}` | Retrieve single resource by ID | `200 OK` / `404 Not Found` |
| `POST` | `/api/Resources` | Create a new shared resource | `201 Created` (with `Location` header) |

**Sample POST `/api/Resources` Request:**
```json
{
  "id": "room-101",
  "name": "Executive Boardroom",
  "type": "Meeting Room"
}
```

---

### 3. Bookings (`/api/Bookings`)

| Method | Endpoint | Description | Status Code |
| :--- | :--- | :--- | :--- |
| `POST` | `/api/Bookings` | Create a new booking | `201 Created` / `400 Bad Request` / `404 Not Found` / `409 Conflict` |
| `GET` | `/api/Bookings/{id}` | Retrieve single booking by ID | `200 OK` / `404 Not Found` |
| `GET` | `/api/Bookings` | Filter bookings by resource, date range, pagination, and sorting | `200 OK` / `400 Bad Request` |
| `DELETE` | `/api/Bookings/{id}` | Soft-cancel booking (status set to `Cancelled`) | `204 No Content` / `404 Not Found` |

**Sample POST `/api/Bookings` Request:**
```json
{
  "resourceId": "room-101",
  "userId": "user-guid-here",
  "startDateTime": "2026-10-15T09:00:00Z",
  "endDateTime": "2026-10-15T11:00:00Z"
}
```

**Query Parameters for `GET /api/Bookings`:**
- `resourceId` (string, required): ID of resource to filter.
- `from` (ISO DateTime, required): Start of query boundary.
- `to` (ISO DateTime, required): End of query boundary (`from < to`).
- `page` (int, optional, default: 1): Page number.
- `pageSize` (int, optional, default: 10, max: 100): Page size.
- `sortBy` (string, optional, default: `StartDateTime`): Sort field (`StartDateTime`, `EndDateTime`, `CreatedAt`, `Id`).
- `sortOrder` (string, optional, default: `asc`): Sort direction (`asc` or `desc`).

---

## 🛡️ Validation & Exception Handling

### DTO Validations
All input DTOs utilize Data Annotations to enforce data integrity before reaching services:
- `Name`: Required, trimmed, length 1–100 characters.
- `Email`: Required, valid email format, length up to 150 characters.
- `Type`: Required, length 1–50 characters.
- `ResourceId` & `UserId`: Required, non-empty, length up to 50 characters.
- Pagination bounds: `page >= 1`, `pageSize` between 1 and 100.

### Global Exception Mapping
The application middleware intercepts all exceptions and transforms them into standard JSON payloads:

| Exception Type | HTTP Status | Response Payload |
| :--- | :--- | :--- |
| `ArgumentException` | `400 Bad Request` | `{"message": "<Validation details>"}` |
| `KeyNotFoundException` | `404 Not Found` | `{"message": "<Entity not found>"}` |
| `BookingConflictException` | `409 Conflict` | `{"message": "The resource is already booked during this time."}` |
| `ConcurrencyException` | `503 Service Unavailable` | `{"message": "Database is currently busy under high concurrent contention..."}` |
| Unhandled / System Exceptions | `500 Internal Server Error` | `{"message": "An unexpected error occurred. Please try again later."}` |

---

## 💾 Database Schema & Lifecycle

### Schema Design
- **`Users`**: `Id (nvarchar(50), PK)`, `Name (nvarchar(100))`, `Email (nvarchar(150))`.
- **`Resources`**: `Id (nvarchar(50), PK)`, `Name (nvarchar(100))`, `Type (nvarchar(50))`.
- **`Bookings`**: `Id (int, PK Identity)`, `ResourceId (nvarchar(50), FK)`, `UserId (nvarchar(50), FK)`, `StartDateTime (datetime2)`, `EndDateTime (datetime2)`, `Status (int)`, `CreatedAt (datetime2)`.
- **Optimized Composite Indexes**:
  - `IX_Bookings_ResourceId_Status_StartDateTime_EndDateTime` on `(ResourceId, Status, StartDateTime, EndDateTime)`: Enables direct index-seek operations for slot collision and range queries.
  - Foreign key constraints configured with `DeleteBehavior.Restrict`.

### Safe Database Lifecycle
- Destructive operations (`EnsureDeleted()`) have been eliminated.
- **Development**: Pending migrations are applied safely on startup via `context.Database.MigrateAsync()` without modifying existing data.
- **Production**: Automatic schema mutations are disabled; migrations are managed through standard CI/CD deployment pipelines or `dotnet ef database update`.

---

## ⚙️ Configuration & Secrets

Connection strings are kept strictly separate from production code:
- **`appsettings.Development.json`**: Points to local development SQL Server LocalDB instance:
  ```json
  {
    "ConnectionStrings": {
      "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=BookingManagementDb;Trusted_Connection=True;TrustServerCertificate=True;"
    }
  }
  ```
- **`appsettings.json`**: Contains an empty placeholder for production.
- **`appsettings.example.json`**: Provides reference configurations for LocalDB, Docker containers, and dedicated SQL Server instances.
- **Production Environment Variables**: In production, configure the database connection string using:
  ```bash
  ConnectionStrings__DefaultConnection="Server=your-sql-server;Database=BookingDb;User Id=user;Password=secret;TrustServerCertificate=True;"
  ```
- Startup validates the presence of `DefaultConnection` and provides an explicit diagnostic message if missing.

---

## 🔒 Security Measures

1. **CORS Hardening**:
   - In Development: Allows local origins for rapid frontend testing.
   - In Production: Rejects arbitrary origins and restricts traffic to origins explicitly defined under `Cors:AllowedOrigins`.
2. **XSS Protection**:
   - The frontend dashboard uses `escapeHtml()` to sanitize all text inputs (`u.name`, `r.name`, `b.id`) before rendering into the DOM.
3. **Information Disclosure Prevention**:
   - Internal stack traces and server internals are intercepted by middleware and never exposed to API consumers in 500 error responses.
4. **SQL Injection Immunity**:
   - All database communication utilizes Entity Framework Core parameterized SQL queries.

---

## 🧪 Testing Suite & Verification

The solution includes a full test suite with **19 automated tests** covering unit scenarios and relational integration concurrency:

```bash
dotnet test
```

### Test Coverage Highlights
- **18 Unit Tests (`BookingServiceTests.cs`)**:
  - Valid booking creation & response structure.
  - Invalid time order (`start >= end`).
  - Blank and whitespace identifier validation.
  - Missing resource & missing user handling (404).
  - Slot overlap rejection (`BookingConflictException`).
  - Back-to-back booking allowance ($[Start, End)$ boundary).
  - Independent simultaneous bookings across distinct resources.
  - Slot reuse following soft cancellation.
  - Single booking lookup by ID.
  - Query pagination, sorting, and boundary validation.
  - Idempotent soft cancellation.
- **1 Relational Concurrency Integration Test (`BookingConcurrencyTests.cs`)**:
  - Executes directly against **SQL Server LocalDB** using `Task.WhenAll`.
  - Fires two concurrent requests for the exact same resource and overlapping time range.
  - Verifies that exactly 1 request succeeds and 1 fails with `BookingConflictException`.
  - Verifies that the database contains exactly one active booking record.

---

## 🚦 Getting Started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (or SQL Server LocalDB / Docker container)

### 1. Clone & Restore
```bash
git clone https://github.com/SanadRahahleh/Booking-Management-Service.git
cd Booking-Management-Service
dotnet restore
```

### 2. Update Database Schema
```bash
dotnet ef database update --project "OOKING MANAGEMENT SERVICE" --startup-project "OOKING MANAGEMENT SERVICE"
```

### 3. Run Application
```bash
cd "OOKING MANAGEMENT SERVICE"
dotnet run
```
API endpoints are served at `http://localhost:5084` and `https://localhost:7098`.
OpenAPI documentation is available in development mode at `/openapi/v1.json`.

### 4. Run Frontend Dashboard
Open [`frontend/index.html`](frontend/index.html) in your browser. The frontend automatically connects to the backend with automatic HTTPS/HTTP fallback.

---

## 🔮 Scalability Trade-offs & Future Improvements

- **Database Locking vs Distributed Locks**: `Serializable` transactions guarantee absolute correctness for single-node SQL Server instances. In high-throughput distributed systems, resource-level distributed locking (e.g. via **Redis Redlock**) or optimistic concurrency tokens can alleviate database lock contention.
- **CQRS & Read Replicas**: Separate query models from write models to scale booking search queries across read replicas.
- **Authentication & Authorization**: Currently excluded by design to focus on core backend architecture, concurrency, and validation. In a multi-tenant production system, integrating ASP.NET Core Identity with JWT Bearer tokens and Role-Based Access Control (RBAC) would be the next step.