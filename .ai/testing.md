# MVP verification

This repository is a native C#/.NET Avalonia desktop application. Use xUnit for
domain/application and SQLite integration tests, and Avalonia Headless for desktop
interaction tests where practical. Vitest and Playwright do not execute this native
application; do not add a JavaScript stack just to run tests.

Desktop UI tests drive the real window headlessly. Projects without open tasks are not on the dashboard, so tests reach them through All projects; an empty database shows the welcome overlay (`OnboardingOverlay`) and hides Needs attention, Calendar and Documents. Task, milestone and project editing is in dialogs (`TaskDialog`, `MilestoneDialog`, `EditProjectDialog`); the checklist edits in place (`RequirementDetail`). Set `PROJECTOPS_UI_CAPTURE_DIR` to save screenshots from the screen-level tests. The solution targets `net10.0`; a .NET 10 SDK is required. `dotnet format` reports end-of-line findings on Windows checkouts with CRLF; those are not code issues.

Keep tests deterministic by supplying explicit UTC instants to attention rules.
Persistence tests use isolated temporary databases, never the user's application data.
Runtime tests use a transport fixture, clearly distinguished from a live provider run.
Do not transmit real project data during verification.
