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
| 0 | Upgrade Avalonia and Headless to 12 on the current code; fix the C# binding API changes; tests green | done |
| 1a | Foundation: DI composition root, `ViewModelBase`, view locator, bindable localization, date converters | done |
| 1b | Extract from `MainWindow`: navigator with typed routes, dialog/toast/error service, agent run lock, workspace session (real/sample); move `ParseLocalDate` to Localization | done |
| 2 | Pilot: Tasks tab, task and milestone dialogs; move urgency buckets to Core. **Gate: user review before phase 3** | done (awaiting review) |
| 3a | Projects list, Dashboard, Calendar, Documents | todo |
| 3b | Settings, stage editor, update panel, Google Calendar panel | todo |
| 3c | Project detail, Overview, Requirements, remaining project dialogs | todo |
| 4 | Shell: `MainWindowViewModel` + XAML, sidebar policy, getting started, onboarding, sample workspace; remove `IShell`, `PresentationContext`, and `UiState` where possible | todo |
| 5 | Assistant: port its tests to view-model tests first, then `AssistantPanel` and `DelegationView` | todo |
| 6 | Cleanup: replace `Components.cs` with styles and controls; update `architecture.md` and `testing.md`; delete this file and its index entry | todo |

## Notes

- Avalonia 12.1.3 (Headless.XUnit 12 requires xunit.v3, so the desktop test project uses `xunit.v3` 3.2.2; `xUnit1051` is silenced there). Not visually checked on Windows.
- Foundation (1a), how to use it:
  - `AppServices.Create` (Desktop root) is the composition root; `App` resolves `MainWindow` from it and disposes the provider on exit.
    Tests pass `overrides` to swap platform-bound services (`IAppUpdater` needs Velopack started).
  - `Common/ViewModelBase` (CommunityToolkit `ObservableObject`); `Common/ViewLocator` maps `XViewModel` → `XView` in the same
    namespace and is registered in `App` and the test `TestApplication`.
  - Localization in XAML: view-models expose `LocalizedStrings L`; bind `{Binding L[key]}`, `L.FlowDirection`. A language switch
    raises one all-properties change, so text updates in place and form input survives.
  - Dates in XAML: `MultiBinding` of the date and `L.Dates` with `common:DateConverters.Display|Edit|ShortDate|MonthYear`;
    null dates convert to "" (the view-model decides the "no date" text).
  - Compiled bindings are the default in Desktop and Desktop.Tests. `Desktop.Tests/Mvvm` holds a probe view proving the pipeline.
- Shell services (1b), in `Desktop/Shell/`:
  - `Navigator` + `Route` (`PageRoute(AppPage, NewProject)`, `ProjectRoute(id, ProjectTab)`); a route the host cannot show leaves the previous one current.
    `INavigator` is the interface view-models will take; `MainWindow` supplies the show callback until phase 4.
  - `ShellMessages` (error key, toast key, `GuardAsync`, `LastFailure`) is rendered by `ErrorBanner`/`ToastView`; `DialogHost` is the modal layer (`IDialogService`).
  - `AgentRunLock` holds the UI through the job and its reload; closing waits for the job and skips the reload.
  - `WorkspaceSession` owns real vs sample services (sample: own database, assistant "not configured", no external calendar).
    `PresentationContext.Projects/Agents/Environment/Calendar` now read from it. `PresentationContext.Strings` is the `LocalizedStrings` for hosted XAML views.
  - `LocalDateInput.Parse` (Localization) replaces `MainWindow.ParseLocalDate`.
  - Old code-built views still use `IShell`; it is removed in phase 4.
- Pilot (2), `Desktop/Features/Tasks/`; copy this for phase 3:
  - Layout: `TasksView.axaml` + `TasksViewModel`, row/group views (`TaskRowView`, `TaskGroupView`, `MilestoneRowView`), `TaskDialogView`, `MilestoneDialogView`.
    View-models are `internal sealed` and take `ProjectScreenServices` (`PresentationContext.ProjectScreen`: projects, strings, dialogs, host, clock).
    A nested view-model in a bound property or an `ItemsControl` with no template is shown by the locator; `ProjectDetailView` hosts the tab as `ContentControl { Content = vm }`.
  - Shell seam: `IProjectHost` (`RefreshProjectAsync`, `ReviewInAssistantAsync`, `RunAsync`) is what screens need from `MainWindow`; `RunAsync` is `ActAsync` without a button
    (locks nav and page, shows failures in the banner). Dialogs: `IDialogService.Show(vm)` / `Close()`; commands wrap their work in `host.RunAsync`.
  - Text: view-models compute text from `L` and call `RefreshOnLanguageChange(L)` once; `LocalizedStrings` holds them weakly and prunes itself.
    `L` also has `Enum`, `Requirement`, `Number`, `Due`, `ShortDate` and `Relative`; `PresentationContext` delegates to them.
  - XAML styling: `Classes="Caption"` etc. (all `PresentationTheme` type styles; they wrap, start-align, default to `TextPrimary`). A local `Foreground=` beats every class
    style, so state colours use a more specific selector (`TextBlock.title.inactive`) instead. Icons: `c:IconGlyph` (`Icon`, `Weight`, `Size`, `Token`; no token inherits the foreground).
    Dialogs: `c:DialogChrome` (title, close, content) with a `Border.dialogFooter` for the buttons; the theme is `Common/Styles.axaml`, included by `PresentationTheme.Apply`.
  - Shared parts in `Common/`: `ChipGroupViewModel<T>` + `ChipOptionView`, `DateFieldViewModel` + `DateFieldView` (`Existing`/`Ahead` factories; the Jalali picker is now
    `JalaliDatePicker` with `Strings` and a two-way `SelectedDate`), `IconGlyph`, `DialogChrome`.
  - Urgency lives in Core: `TaskUrgency` (`BucketOf`, `Group`); `ProjectSummaries` uses it too.
  - Tests: `Mvvm/TasksFeatureTests` (plain xUnit, fixed clock). `UiWait.Click` now invokes the button through its automation peer, because a raw `ClickEvent` does not run a bound
    `Command`. Controls inside a `ControlTemplate` are not in the logical tree, so tests cannot find them there: keep buttons the tests need in content, not in a template.
  - One deliberate behaviour change: a new task or milestone now defaults to end of day (23:59), as `architecture.md` says; the old dialog kept the current clock time of day.
- Open questions: user review of the pilot (gate before 3a).
