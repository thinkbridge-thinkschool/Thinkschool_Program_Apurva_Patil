# Week 1 — Day 3 Summary

This folder contains the Day 3 exercises for Week 1. The work is organized into multiple task folders, each building on the same ASP.NET Core 10 Minimal API sample for managing quotes and collections.

## What changed for Day 3

- All `QuotesAPI-Amey` task folders were updated to `QuotesAPI-Apurva`.
- Any references to `Amey` inside Day 3 text files were replaced with `Apurva`.

## Task folders

### `task-1-Wire Entra ID as the identity provider`
- Implements Azure Entra ID / Azure AD authentication for the Quotes API.
- Demonstrates using a cloud identity provider to protect API endpoints instead of local username/password authentication.
- Includes a `QuotesAPI-Apurva` project with the API implementation, configuration, and task submission files.

### `task-2-Authorization policies and claims`
- Adds authorization policies and claims-based access control.
- Shows how to enforce rules such as role-based or claim-based permissions in a minimal API.
- Contains a dedicated `Authorization` folder and updated policy registration.

### `task-4-xUnit with Fluent Assertions`
- Adds unit tests using xUnit and Fluent Assertions.
- Covers domain and API behavior with clear, expressive assertions.
- Includes a test project for verifying application rules and response behavior.

### `task-5-Lock down the API end-to-end`
- Extends API security coverage to the full end-to-end flow.
- Ensures the application is locked down with authentication and authorization.
- Contains solution and submission notes describing the secured API behavior.

### `task-6-Integration tests with WebApplicationFactory`
- Adds integration tests using `WebApplicationFactory`.
- Validates the API behavior through the full request pipeline.
- Includes both unit and integration test projects.

### `task-7-Real SQL Server in CI with Testcontainers`
- Uses Testcontainers to run a real SQL Server instance in CI.
- Demonstrates how to test database integration against a real SQL Server environment.
- Includes CI-focused test automation and Docker-backed database setup.

## Common structure

Each task folder contains a `QuotesAPI-Apurva` project with:
- `Program.cs` and minimal API endpoints
- `Data/`, `Models/`, `Services/`, `Extensions/`, and `Validators/`
- Entity Framework Core migrations in `Migrations/`
- Test projects under `QuotesApi.Tests/`, `Quotes.Tests.Unit/`, or `Quotes.Tests.Integration/`
- A `README.md` in the task folder with task-specific instructions

## How to use

1. Open `day3` in Visual Studio or Visual Studio Code.
2. Open any task folder and inspect the project and `README.md` inside it.
3. Run the selected task project with `dotnet run` or execute tests with `dotnet test`.

## Notes

- There is no task 3 folder in this directory structure.
- The Day 3 tasks build on the same quotes API sample and progressively add cloud authentication, authorization, secure design, and test automation.
