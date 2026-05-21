# Quotes API — Dependency Injection exercise

This small Web API demonstrates using DI lifetimes and an `IClock` abstraction.

To run:

```powershell
cd "c:\Users\Vipul Yadav\OneDrive\Desktop\Day-02\task-01_Dependency-Injection"
dotnet run
```

Files of interest:

- Program.cs — registers `IClock` as singleton, repository as scoped, formatter as transient.
- Services/IClock.cs — clock abstraction.
- Services/SystemClock.cs / FakeClock.cs — implementations for production and tests.
- Controllers/QuotesController.cs — uses constructor injection and calls `IClock.UtcNow`.
