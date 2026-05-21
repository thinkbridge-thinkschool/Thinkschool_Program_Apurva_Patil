# task_05_Implement_JWT

This exercise adds JWT authentication to the Quotes API.

Run the API:

```powershell
cd "c:\Users\Vipul Yadav\OneDrive\Desktop\Day-02\task_05_Implement_JWT"
dotnet run
```

Test the endpoints:

- POST without token => 401

```bash
curl -i -X POST http://localhost:5000/api/quotes -H "Content-Type: application/json" -d '{"text":"hi"}'
```

- Login to get token:

```bash
curl -s -X POST http://localhost:5000/api/auth/login -H "Content-Type: application/json" -d '{"email":"alice@example.com","password":"P@ssw0rd!"}'
```

- Use token (replace TOKEN) to POST:

```bash
curl -i -X POST http://localhost:5000/api/quotes -H "Authorization: Bearer TOKEN" -H "Content-Type: application/json" -d '{"text":"hello"}'
```

- Expired token test: login, wait for `expires_in` seconds to pass, then POST — expect `401` and `WWW-Authenticate` header containing `error="invalid_token"` and `error_description`.
