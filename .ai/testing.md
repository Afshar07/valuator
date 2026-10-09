# MVP verification

This repository is a native C#/.NET Avalonia desktop application. Use xUnit for
domain/application and SQLite integration tests, and Avalonia Headless for desktop
interaction tests where practical. Vitest and Playwright do not execute this native
application; do not add a JavaScript stack just to run tests.

Desktop UI tests drive the real window headlessly. Projects without open tasks are not on the dashboard, so tests reach them through All projects; an empty database shows the welcome overlay (`OnboardingOverlay`) and hides Needs attention, Calendar and Documents. Task, milestone and project editing is in dialogs (`TaskDialog`, `MilestoneDialog`, `EditProjectDialog`); the checklist edits in place (`RequirementDetail`). Click with `UiWait.Click`: it invokes the button as assistive technology does, so bound `Command`s run (a raw `ClickEvent` would not). Tasks-tab logic is covered by plain-xUnit view-model tests (`Mvvm/TasksFeatureTests`); urgency rules by `TaskUrgencyTests`. Set `PROJECTOPS_UI_CAPTURE_DIR` to save screenshots from the screen-level tests (`JalaliDateTests` captures the Persian date picker). Jalali conversion, parsing and the Persian dialog flow are covered in `JalaliDateTests`; `TextBox.TextChanged` is raised on the dispatcher, so call `Dispatcher.UIThread.RunJobs()` after setting `Text` in tests. Avalonia ignores Unicode directional isolates: assert and render RTL number runs with LRM/RLM marks (`JalaliDate.KeepLeftToRight`), and review RTL date text in a captured frame, not only by string assertion. Wait for dialogs that open after an awaited query (`UntilAsync`) rather than assuming one dispatcher pass. The solution targets `net10.0`; a .NET 10 SDK is required. `dotnet format` reports end-of-line findings on Windows checkouts with CRLF; those are not code issues.

Keep tests deterministic by supplying explicit UTC instants to attention rules.
Persistence tests use isolated temporary databases, never the user's application data.
Runtime tests use a transport fixture, clearly distinguished from a live provider run.
Do not transmit real project data during verification.

Google Calendar overlay tests never contact Google. `GoogleCalendarSourceTests` (core) drive `GoogleCalendarSource` through a fake HTTP handler and a real loopback listener; `GoogleCalendarUiTests` (desktop) drive the real window with a fake `IExternalCalendarSource`; `ExternalCalendarBoundaryTests` reflects over agent, domain, application and runtime types and fails if any depends on external calendar types (keep that rule when adding agent context). A real Google sign-in and Windows DPAPI storage are not covered by automated tests (see `technical-debt/google-calendar-followups.md`). In UI tests the Calendar nav is hidden until something is dated or Google is connected, and `Assert.DoesNotContain` is required over `Assert.Empty(...Where(...))` (xUnit2029 is an error).
