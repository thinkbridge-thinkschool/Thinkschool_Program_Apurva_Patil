# Why Rich Domain Model Is Better

The rich Quote model moved business rules directly into the entity instead of spreading validation logic across controllers and services. This made the application safer because invalid quotes can no longer be created accidentally from another endpoint or future feature. The static factory method ensures that every Quote always satisfies the required rules before entering the system.

The anemic version only contained properties with public setters. Any controller or developer could directly modify the Text or Author fields without validation. That approach makes bugs much easier to introduce because rules are duplicated in many places or forgotten entirely.

The rich model also protects important business behavior. In this assignment, quote text cannot change after creation. The new design enforces that automatically by removing public setters. Soft deletion is also safer because quotes are marked as deleted instead of being permanently removed from the database.

One real bug the anemic model could have shipped is allowing empty quotes or quotes larger than the allowed size when a new endpoint forgot validation logic. The rich model prevents this because the entity itself rejects invalid state during creation.
