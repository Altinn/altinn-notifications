# Copilot Instructions

## Project Guidelines
- Bracing in if statements (and all control flow statements) is mandatory. Always use curly braces, even for single-line bodies.
- Code should not contain multiple whitespace characters in a row (no double spaces in code).
- Use basic xUnit assertions (e.g., Assert.Equal, Assert.Null, Assert.Throws) for testing instead of FluentAssertions.
- For SMS repository behavior, only single-item retrieval via GetNewNotification is allowed; tests should not use legacy batch GetNewNotifications and must be updated to single-item flow.