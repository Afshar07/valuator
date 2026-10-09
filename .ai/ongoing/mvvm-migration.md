# MVVM migration and Avalonia 12

**Keep this file current.** Whenever a task changes state, update its row in the
same commit. Record only decisions and state that other sessions need; keep it short.

## Goal

Move the desktop UI from code-built views to MVVM: XAML views and testable view-models.
Upgrade to Avalonia 12 first. Users are mainly on Windows. Linux stays the headless test host.

## Decisions

- Avalonia 12.x (latest stable when the upgrade lands; record the exact version below).
- CommunityToolkit.Mvvm, `Microsoft.Extensions.DependencyInjection`, a view locator,
  and compiled bindings (`x:DataType`) by default.
- Feature folders: `Desktop/Features/<Feature>/` with `<Name>View.axaml` and `<Name>ViewModel.cs`.
  Shared styles and controls go in `Desktop/Common/`.
- View-models reference no Avalonia controls. Test them with plain xUnit; keep headless
  tests only for layout, RTL and smoke checks.
- Domain rules (urgency, readiness, attention) belong in Core, never in view-models.
- Localization becomes a bindable string source that notifies on locale change.
  Theme tokens stay `DynamicResource`.
- Migrated XAML views are hosted inside the old code-built shell until phase 4.
  Every commit builds and passes tests.
- Keep the safety rules from `mvp.md`/`architecture.md` unchanged: preview, consent,
  Stop, proposal approval, and the external-calendar boundary.

## Tasks

State: `todo` → `in progress` → `done`.

| # | Task | State |
| --- | --- | --- |
| 0 | Upgrade Avalonia and Headless to 12 on the current code; fix the C# binding API changes; tests green | todo |
| 1a | Foundation: DI composition root, `ViewModelBase`, view locator, bindable localization, date converters | todo |
| 1b | Extract from `MainWindow`: navigator with typed routes, dialog/toast/error service, agent run lock, workspace session (real/sample); move `ParseLocalDate` to Localization | todo |
| 2 | Pilot: Tasks tab, task and milestone dialogs; move urgency buckets to Core. **Gate: user review before phase 3** | todo |
| 3a | Projects list, Dashboard, Calendar, Documents | todo |
| 3b | Settings, stage editor, update panel, Google Calendar panel | todo |
| 3c | Project detail, Overview, Requirements, remaining project dialogs | todo |
| 4 | Shell: `MainWindowViewModel` + XAML, sidebar policy, getting started, onboarding, sample workspace; remove `IShell`, `PresentationContext`, and `UiState` where possible | todo |
| 5 | Assistant: port its tests to view-model tests first, then `AssistantPanel` and `DelegationView` | todo |
| 6 | Cleanup: replace `Components.cs` with styles and controls; update `architecture.md` and `testing.md`; delete this file and its index entry | todo |

## Notes

- Exact Avalonia version: —
- Open questions: —
