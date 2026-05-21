# Week 1 Summary

This repository contains the exercises and sample projects completed during Week 1. The work is organized by day and focuses on basic C# and TypeScript development, API design, dependency injection, asynchronous programming, domain modeling, and authentication.

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

## Notes

- `day3/` is present but does not contain completed work yet.
- `QuotesApi` includes production-style startup configuration, error handling, authentication, and database migration logic.
- `QuotesApiNode` shows a lightweight alternative API built without Express.

## How to use

- Open the workspace in Visual Studio or Visual Studio Code.
- Run C# projects using `dotnet run` from the project folder.
- Run the Node.js project using `npm install` and `npm start` from `QuotesApiNode`.
- Explore the `README.md` files inside the task folders for more specific instructions.
