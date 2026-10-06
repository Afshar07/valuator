# MVP verification

This repository is a native C#/.NET Avalonia desktop application. Use xUnit for
domain/application and SQLite integration tests, and Avalonia Headless for desktop
interaction tests where practical. Vitest and Playwright do not execute this native
application; do not add a JavaScript stack just to run tests.

Keep tests deterministic by supplying explicit UTC instants to attention rules.
Persistence tests use isolated temporary databases, never the user's application data.
Runtime tests use a transport fixture, clearly distinguished from a live provider run.
Do not transmit real project data during verification.
