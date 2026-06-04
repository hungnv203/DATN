# MovieBooking Project Agent Guide

## Project Overview
MovieBooking is an ASP.NET Core API application built using Clean Architecture principles. It serves as the backend for a comprehensive movie ticketing and cinema management system. 

The system handles users, roles, cinemas, rooms, seats, movies, showtimes, bookings, payments, promotions, and loyalty points.

## Tech Stack
- **Framework**: .NET 10.0
- **ORM**: Entity Framework Core 10.0.4
- **Database**: PostgreSQL (via `Npgsql`)
- **Key Libraries**: AutoMapper, Swashbuckle (Swagger/OpenAPI)

## Architecture Layers
1. **MovieBooking.Domain**: The core of the system. Contains enterprise logic, domain entities (e.g., `User`, `Movie`, `Booking`, `Cinema`), and enums. It has no dependencies on other projects.
2. **MovieBooking.Application**: Contains business logic, use cases, and interfaces (like `ICrudService<,>`). It depends only on the Domain layer.
3. **MovieBooking.Infrastructure**: Implements the interfaces defined in the Application layer. Contains data access logic (`AppDbContext`, `EfCrudService<,>`), database migrations, and mapping profiles (`EntityDtoProfile`).
4. **MovieBooking (API)**: The presentation layer. Exposes RESTful endpoints via Controllers. It wires up the Dependency Injection from Application and Infrastructure layers.

## Agent Role
As an AI agent working on this project, your primary responsibilities are:
- To implement new features adhering strictly to the Clean Architecture boundaries.
- To create or update domain entities, application services, and infrastructure implementations when adding new functionality.
- To build API endpoints using Controllers, ensuring appropriate DTO mapping and request validation.
- To maintain and extend the database schema via EF Core Fluent API.
