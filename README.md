# Booking Management Service

A backend service for managing bookings of shared resources such as meeting rooms, desks, and equipment.

The system provides RESTful APIs for managing users, resources, and bookings, with built-in validation and protection against double bookings.

---

## Tech Stack

- .NET 10 Web API
- ASP.NET Core
- Entity Framework Core 10
- SQL Server / LocalDB
- xUnit
- Entity Framework Core InMemory
- HTML5
- CSS3
- JavaScript (ES6)
- REST API
- 3-Tier Layered Architecture

---

## Architecture

The project follows a 3-Tier Layered Architecture:

```text
Controller
    ↓
Service Interface
    ↓
Service
    ↓
Data Access
    ↓
SQL Server
```

### Components

- **Controllers**: Handle HTTP requests and responses and communicate with application services.
- **Interfaces**: Define contracts for application services (`IBookingService`, `IResourceService`, `IUserService`).
- **Services**: Contain application business logic and booking rules (`BookingService`, `ResourceService`, `UserService`).
- **Data**: Contains Entity Framework Core `AppDbContext` for database communication.

---

## Main Features

### 1. User Management
- Create users
- Retrieve users

### 2. Resource Management
- Create shared resources (e.g., Meeting Rooms, Desks, Equipment)
- Retrieve resources

### 3. Booking Management
- Create bookings
- Retrieve bookings with date range filtering, pagination, and sorting
- Cancel bookings (soft cancellation)
- Prevent overlapping active bookings

---

## Booking Rules & Concurrency

A resource cannot have two active bookings that overlap in time.

### Overlapping Booking Example (Rejected):
```text
10:00 ───────── 12:00
       Booking 1

11:00 ───────── 13:00
       Booking 2  (Rejected - Overlaps with Booking 1)
```

### Back-to-Back Booking Example (Allowed):
```text
10:00 ───── 11:00
       Booking 1

11:00 ───── 12:00
       Booking 2  (Allowed - Half-open boundary)
```

> **Cancelled Bookings**: Cancelled bookings do not prevent a resource from being booked again.

---

## Concurrency Protection (Extension Task — Option 1)

The booking creation process uses a database transaction with `Serializable` isolation to protect the overlap check from concurrent requests:

```text
Begin Transaction (Serializable)
       ↓
Check for overlapping active booking
       ↓
Create booking (Status = Active)
       ↓
Save Changes
       ↓
Commit Transaction
```

---

## 📑 Design Write-up (Assignment Q&A)

### A. How did you define and enforce overlapping bookings, and why?
- **Definition**: Booking $[S_{new}, E_{new})$ overlaps active booking $[S_{old}, E_{old})$ if:
  $$\text{Start}_{new} < \text{End}_{old} \quad \land \quad \text{End}_{new} > \text{Start}_{old}$$
- **Boundaries**: Back-to-back bookings (e.g. 10–11 and 11–12) do not collide because intervals are half-open $[Start, End)$.
- **Enforcement**: Checked inside `BookingService.CreateBookingAsync` within a `Serializable` transaction to prevent race conditions.

### B. What did you assume about concurrency?
- We assumed multiple users can attempt to book the same resource for overlapping time slots at the exact same millisecond. Application-level `AnyAsync()` checks alone are insufficient due to read-after-read phantom condition races.

### C. What would break in your design at scale, and where would the first bottleneck be?
- **First Bottleneck**: **Database Transaction Locking**.
  `IsolationLevel.Serializable` locks rows/tables during evaluation. Under high write throughput for popular resources, requests will queue up waiting for locks, leading to connection timeouts or `409 Conflict` spikes.

### D. How would you evolve this into a distributed system?
1. **Redis Distributed Lock (Redlock)**: Lock at the resource level (`lock:resource:{id}`) in Redis before database operations.
2. **CQRS**: Separate write DB from read replicas for queries.
3. **Event Broker**: Publish `BookingCreated` events to RabbitMQ/Kafka for async notification/billing services.

### E. Which tradeoff did you prioritize — simplicity, correctness, or performance — and why?
- **Priority**: **Correctness**. Double-booking in a shared resource environment is a critical business failure. Absolute consistency was prioritized over write speed.

---

## API Endpoints

### Users
- **GET /api/Users**: Retrieve all registered users.
- **POST /api/Users**: Create a user.
  ```json
  {
    "name": "Sanad Rahahleh",
    "email": "sanad@example.com"
  }
  ```

### Resources
- **GET /api/Resources**: Retrieve all shared resources.
- **POST /api/Resources**: Create a resource.
  ```json
  {
    "name": "Meeting Room A",
    "type": "Room"
  }
  ```

### Bookings
- **POST /api/Bookings**: Create a booking.
  ```json
  {
    "resourceId": 1,
    "userId": 1,
    "startDateTime": "2026-09-10T10:00:00Z",
    "endDateTime": "2026-09-10T12:00:00Z"
  }
  ```
- **GET /api/Bookings**: Retrieve bookings with filtering, pagination, and sorting.
  - Query params: `resourceId`, `from`, `to`, `page`, `pageSize`, `sortBy`, `sortOrder`.
  - Example: `GET /api/Bookings?resourceId=1&from=2026-09-01T00:00:00Z&to=2026-09-30T23:59:59Z&page=1&pageSize=10`
- **DELETE /api/Bookings/{id}**: Soft-cancel a booking (changes status to `Cancelled`).

---

## Testing

The project includes unit tests using **xUnit** and **EF Core InMemory Database**:

```bash
dotnet test
```

### Covered Test Scenarios:
- Booking validation (`StartDateTime < EndDateTime`)
- Overlapping booking prevention
- Back-to-back bookings
- Ignored cancelled bookings
- Soft cancellation behavior
- Pagination & Sorting logic

---

## Getting Started

### 1. Prerequisites
- .NET 10 SDK
- SQL Server or LocalDB
- IDE (Visual Studio / VS Code)

### 2. Run Application
```bash
# Navigate to API project
cd "OOKING MANAGEMENT SERVICE"

# Run API
dotnet run
```
API available at `http://localhost:5084` and `https://localhost:7098`.

### 3. Run Frontend Dashboard
Open [`frontend/index.html`](frontend/index.html) in a web browser while the backend API is running.

---

## Project Structure

```text
Booking-Management-Service
│
├── OOKING MANAGEMENT SERVICE
│   ├── Controllers
│   │   ├── UsersController.cs
│   │   ├── ResourcesController.cs
│   │   └── BookingsController.cs
│   ├── Data
│   │   └── AppDbContext.cs
│   ├── DTOs
│   ├── Interfaces
│   │   ├── IBookingService.cs
│   │   ├── IResourceService.cs
│   │   └── IUserService.cs
│   ├── Migrations
│   ├── Models
│   │   ├── User.cs
│   │   ├── Resource.cs
│   │   └── Booking.cs
│   ├── Services
│   │   ├── BookingService.cs
│   │   ├── ResourceService.cs
│   │   └── UserService.cs
│   ├── appsettings.json
│   └── Program.cs
│
├── BookingManagementService.Tests
│   ├── BookingServiceTests.cs
│   └── BookingManagementService.Tests.csproj
│
└── frontend
    ├── index.html
    ├── style.css
    └── app.js
```
This project was developed as a backend engineering project to demonstrate REST API development, layered architecture, database design, business logic, testing, and concurrency handling.