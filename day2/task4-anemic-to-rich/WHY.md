The rich domain model moves validation and invariants into the `Quote` itself, rather than scattering checks across controllers, services, and repositories. By providing a single `Quote.Create(author, text)` factory we centralize length checks (text 1–1000 chars, author 1–200 chars) and return an explicit domain error on failure. This keeps construction safe and prevents partially-validated objects from existing in the system.

Specific gains:
- Consistency: every creation uses the same rules; no duplicate validation logic.  
- Clarity: the API layer calls the factory and handles a single success/failure result, simplifying controller code.  
- Encapsulation: `Text` is immutable after creation and the only allowed state transition is a soft-delete, preventing accidental updates that violate audit or business intent.

Bug scenario the anemic model would allow:
An anemic `Quote` with public setters lets a service or UI layer modify `Text` after creation. Imagine a moderation flow that tries to replace offensive text by editing the entity in place — if a request partially fails mid-transaction, inconsistent historical views or audit trails could show different text across reads. The rich model prevents any runtime text mutation, so such a bug is impossible by design.

In short, the rich model shifts responsibility to the domain, improves correctness guarantees, and reduces duplicated validation logic across the codebase.