# Week 1 Summary

This repository contains the exercises and sample projects completed during Week 1. The work is organized by day and focuses on basic C# and TypeScript development, API design, dependency injection, asynchronous programming, domain modeling, authentication, and secure API testing.

## Day 1

### `hello-cs`
- Simple C# console application.
- Prints `Hello, World!` to demonstrate C# project structure and runtime.

### `hello-ts`
- Simple TypeScript script.
- Prints `Hello, World!` and demonstrates TypeScript syntax and execution.

### `QuotesApi`
- ASP.NET Core Web API project.
- Uses Entity Framework Core with migrations and a relational database.
- Implements JWT authentication and authorization.
- Includes application services, middleware, and endpoint registration.
- Features user registration, login, refresh token handling, and quotes/collection endpoints.
- Contains a `WHY.md` file explaining the domain model design decisions.

### `QuotesApiNode`
- Minimal Node.js + TypeScript API using the native HTTP module.
- Uses SQLite (`better-sqlite3`) for storage and `pino` for structured logging.
- Supports JSON validation, pagination, and basic CRUD endpoints for quotes.
- Runs directly with `node --loader tsx`.

## Day 2

### `day2-readme`
- Placeholder folder for day 2 notes.

### `task1-dependency-injection`
- Demonstrates dependency injection in ASP.NET Core.
- Covers service lifetimes: singleton, scoped, and transient.
- Uses an `IClock` abstraction and constructor injection in controllers.

### `task2-async`
- Contains an async programming exercise.
- Focuses on `async`/`await` pattern and asynchronous flow control.

### `task2-cancellation-tests`
- Includes cancellation testing scenarios.
- Contains a test project for verifying cancellation token behavior.

### `task3-domain`
- Implements domain modeling and separation of domain logic.
- Contains domain objects and a dedicated domain project.

### `task3-domain-tests`
- Contains domain and aggregate tests.
- Verifies domain invariants and behavior through unit tests.

### `task4-anemic-to-rich`
- Refactors an anemic data model to a rich domain model.
- Moves business rules into the entity and removes public setters.
- Includes a `WHY.md` explaining the benefits of the rich model.

### `task5-implement-jwt`
- Adds JWT authentication to the Quotes API.
- Demonstrates protected endpoints, token issuance, and token validation.
- Includes examples for login, authorized requests, and expired token behavior.

### `task6-refresh-token-with-rotation`
- Implements refresh token persistence with rotation and reuse detection.
- Uses a refresh token model with hashed token storage, expiry, revocation, and replacement tracking.

## Day 3

### `task-1-Wire Entra ID as the identity provider`
- Implements Azure Entra ID / Azure AD authentication for the Quotes API.
- Demonstrates using a cloud identity provider to protect API endpoints instead of local username/password authentication.
- Includes a `QuotesAPI-Apurva` project with the API implementation and configuration.

### `task-2-Authorization policies and claims`
- Adds authorization policies and claims-based access control.
- Shows how to enforce role-based and claim-based permissions in a minimal API.
- Includes dedicated policy registration and secure endpoint handling.

### `task-4-xUnit with Fluent Assertions`
- Adds unit tests with xUnit and Fluent Assertions.
- Verifies domain invariants and API behavior with clear, expressive assertions.
- Contains test projects for application rules and controller behavior.

### `task-5-Lock down the API end-to-end`
- Secures the API flow from authentication to authorization.
- Covers end-to-end protection of endpoints and data access.
- Includes a locked-down solution with submission notes.

### `task-6-Integration tests with WebApplicationFactory`
- Adds integration tests using `WebApplicationFactory`.
- Validates the full ASP.NET Core request pipeline with realistic scenarios.
- Includes both unit and integration test projects.

### `task-7-Real SQL Server in CI with Testcontainers`
- Uses Testcontainers to run a real SQL Server instance in CI.
- Demonstrates database integration testing against a real SQL Server environment.
- Includes CI-friendly test automation and Docker-backed database setup.

