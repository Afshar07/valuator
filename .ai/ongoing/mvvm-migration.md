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
| 2 | Pilot: Tasks tab, task and milestone dialogs; move urgency buckets to Core. **Gate: user review before phase 3** | done |
| 3a | Projects list, Dashboard, Calendar, Documents | done |
| 3b | Settings, stage editor, update panel, Google Calendar panel | done |
| 3c | Project detail, Overview, Requirements, remaining project dialogs | done |
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
- Pages (3a), `Desktop/Features/{Projects,Dashboard,Calendar,Documents}/`; copy this for 3b and for pages that are not project screens:
  - Seam: `PageServices` (`PresentationContext.Page`) is `ProjectScreenServices` for top-level pages: projects, strings, dialogs, `INavigator`, `IPageHost`
    (`RunAsync`, `ShowError`, `ToggleAssistant`, `ShowWizard`, `RefreshShellAsync`), `IFileLauncher` (open file / folder) and the active `IExternalCalendarSource`
    (so the sample workspace gets none). `services.GoAsync(route)` is navigation under the busy state. `MainWindow` implements `IPageHost` and `IFileLauncher`.
  - Each page has `static LoadAsync(PageServices)` that reads the data and returns the view-model; `MainWindow.ShowPageAsync` hosts it as `ContentControl { Content = vm }`.
    Rows are view-models shown by the locator; commands are `[RelayCommand]` and wrap their work in `Host.RunAsync` (or `GoAsync`).
    Calendar's opt-in Google overlay starts with `CalendarViewModel.StartAsync()` after the page is shown; `Refreshing` is the latest fetch, for tests.
  - `ViewModelBase.OnLanguageChanged()` (virtual) runs before the all-properties notification, for state that depends on the language itself (the calendar's Gregorian/Jalali months).
  - New shared controls in `Common/`, all code-built (no template, so tests still find their text): `PageHeader` (title, subtitle, trailing content), `Pill` + `PillKind`,
    `StagePill`, `ProgressTrack`, `InitialTile`, `StatCard`, `TableGrid` (a grid whose columns drop when the inherited `TableGrid.IsCompact` is set; `CompactColumn` = -1 hides a
    cell), `EqualColumnsPanel` (equal columns that ellipsize instead of widening). `Styles.axaml` gained `Border.card`, `Border.rule` (+ `.first`) and `Border.dot` (+ `.milestone`,
    `.overdue`, `.external`). Styles do apply to `Run`, so a coloured part of a line is `<Run Classes="when" Classes.late="{Binding IsLate}"/>`. Write `<Run/><Run/>` on one line: whitespace between them renders as a space.
  - Names the UI tests rely on are kept: `NewProjectButton`, `ProjectRow`, `DeleteProjectButton`, `ConfirmDeleteProjectButton`, `PriorityItems`, `PriorityRow`, `AgendaCard`, `Dashboard*Stat`,
    `StatValue`, `CalendarEvent`, `CalendarGoogleEvent`, `CalendarGoogleLegend`, `CalendarNotice`, `DocumentRow`, `DocumentPath`.
  - Tests: `Mvvm/{Projects,Dashboard,Calendar,Documents}FeatureTests` over `Mvvm/PageScenario`. `PresentationTests` presses the delete buttons with `UiWait.Click` because they are bound commands now.
  - Checked against the old screens with `PROJECTOPS_UI_CAPTURE_DIR` (English/Persian, light/dark, 800 and 1440 wide): same layout. Not checked on Windows.
  - Still code-built and still reached through `IShell`: the project detail, the assistant panel, onboarding. `ListCard`, `TableHeader`, `SectionCard` and `ListRow` stay in `Components.cs` for them until phase 6.
- Settings (3b), `Desktop/Features/Settings/`; copy this for 3c and for anything with several independent sections:
  - Seam: `SettingsServices` (`PresentationContext.Settings`) is `PageServices` plus `LocaleContext`, `AppearanceContext`, `DesktopEnvironment` and `UpdateController`. `IPageHost` gained
    `ShowWelcome()` and `IsAgentRunning`. `SettingsViewModel.LoadAsync` reads the stages and the Google connection first, so the page opens complete.
  - Layout: `SettingsView` holds appearance, assistant and data inline; the other cards are child view-models shown by the locator, each view owning its own card and heading:
    `UpdatePanel`, `GoogleCalendarPanel` (the property is null when the source is unavailable, so the card is absent, not hidden) and `StageEditor` with `StageRow` and `StageColor` views.
  - Page lifetime: a page view-model may be `IDisposable`. `MainWindow.Show(control, model)` disposes the previous page's model (and the current one on close). Settings uses it to abandon a pending Google
    sign-in, and to stop listening to `UpdateController` and the theme. This replaces `AttachedToLogicalTree`/`DetachedFromLogicalTree` hooks; new page view-models with a subscription or a pending operation should do the same.
  - Segmented controls in XAML: `Border.segmented` and `Button.segment(.selected)` in `Styles.axaml`, an `ItemsControl` over `ChipGroupViewModel<string>.Options`, and a small item template kept in `SettingsView`
    and `StageEditorView`. The code-built `Segmented` stays for the sidebar and onboarding until phase 4. `ChipGroupViewModel.Selected` now marks chips from the final selection, so a listener that refuses a choice
    and reverts it (a failed theme or language save) leaves the right chip lit.
  - `Common/ColorSwatch` draws a user colour (a stage colour, not a theme token). The stage colour picker is a `Popup` bound to the row's `IsColorOpen`. A title commits on Enter (`KeyBinding`) and on focus loss
    (a one-line handler in `StageRowView.axaml.cs`); the old commit flag was set even for a no-op, so a later edit in the same box was silently dropped, which is fixed.
  - Behaviour kept: Connect does not use the page lock, Add/Move/Delete do (`Host.RunAsync`), rename and colour are guarded and reload on failure, the update buttons never lock the page and progress ticks only
    move the bar. Small changes: the theme chips here now follow a change made in the sidebar; the Open-folder button goes through `IFileLauncher`; the Delete tooltip explaining why it is disabled survives a language switch.
  - Tests: `Mvvm/SettingsFeatureTests` (plain xUnit over `PageScenario`, which gained `Appearance`, `Environment`, `FakeUpdater`, `SettingsServices(...)` and a richer `FakeCalendar`) and `SettingsPageUiTests`.
    Update/Google UI tests now treat a hidden control as absent (`IsEffectivelyVisible`) and read a command-disabled button with `IsEffectivelyEnabled`.
  - Checked against the old screens with `PROJECTOPS_UI_CAPTURE_DIR` from a worktree of the previous commit: Settings (English light, English dark at 800, Persian dark) and the Google-connected, update-offered and
    download-in-progress states in English light and Persian dark are pixel-identical. Not checked on Windows.
- Project detail (3c), `Desktop/Features/{ProjectDetail,Overview,Requirements}/`; copy this for the assistant (phase 5):
  - Seam: `ProjectScreenServices` now also holds `INavigator`, `IFilePicker` (`PickAsync(title)` returns `PickedFile(Name, LocalPath?)`; `MainWindow` implements it over the storage provider) and `UiState`.
    `IProjectHost` gained `ShowError`, `ShowToast`, `ToggleAssistant`, `OpenAssistant` and `SelectTab` (records the picked tab so a reload returns to it).
  - `ProjectDetailViewModel` (`LoadAsync(project, tab, services, agents, delegation)`) owns the header and four tab view-models; `ProjectDetailView.axaml` is a `TabControl` whose
    `SelectedIndex` is two-way bound (choosing Delegate calls `OpenAssistant`). The run lock reaches the tabs through `IsLocked` (`MainWindow.ApplyLock` sets it); the header's two
    responsive tricks (title ellipsis width, readiness dropping below when narrow) stay in `ProjectDetailView.axaml.cs`.
  - The Delegate tab is still code-built: `DelegationTabViewModel`/`DelegationTabView` carry `PresentationContext` and host `DelegationView`. Phase 5 deletes both.
  - `EditProjectDialogViewModel`: blank name sets `ShowNameError` (cleared on the next edit) and saves nothing; save is under `Host.RunAsync` and ends with `RefreshProjectAsync`.
  - Overview: `OverviewViewModel` (facts, `MissingItemViewModel` rows) plus two `StateListViewModel` cards (add line is `IsAdding`, `NewText`, `Save`/`Cancel`).
  - Requirements: `RequirementsViewModel` → `RequirementGroupViewModel` → `RequirementRowViewModel` → `RequirementDetailViewModel` (editor: value draft, linked files, status chips
    `RequirementStatusChipViewModel`, `FollowUpFormViewModel`). Expanded groups, the open item, the dismissed tip and a pending follow-up still live in `UiState` (until phase 4), so a
    reload restores them. Opening an item builds its editor from scratch and closing drops it, which is what discarded typed drafts before. Language switches keep the draft (it is a property).
  - Hidden-but-present: the add lines and the value/document branches use `IsVisible`, so they stay in the logical tree. A tab the user visited also stays in the logical tree after leaving it,
    detached from the visual tree. `UiWait.Click` therefore skips a button unless it and every logical ancestor are visible (`UiWait.IsShown`).
  - Behaviour changes: choosing the status that is already selected no longer re-saves (it used to refresh `LastReviewedAt`); the add-line input now takes focus visibly; the follow-up chips are
    `ChipOptionViewModel`s styled to the old 26 px height.
  - Tests: `Mvvm/{ProjectDetail,Overview,Requirements}FeatureTests` over `Mvvm/ProjectScenario` (a `PageScenario` plus `FakeProjectHost`, `FakePicker` and a fresh `UiState`);
    `ProjectScreensUiTests` for the XAML wiring (edit dialog, focus, RTL indent, follow-up, add requirement). Existing window tests keep passing unchanged.
  - Checked against the old screens with `PROJECTOPS_UI_CAPTURE_DIR` from a worktree of the previous commit: Overview, Requirements, Tasks, Delegate and the dashboard (English/Persian, 800 and
    1440 wide) differ by at most 5/255 in shadow pixels; the edit dialog, its error, the open document row, the follow-up form and the add line match, except the dialog title sits 4 px lower
    (the `DialogChrome` header is 4 px taller than the old frame; not compared against the task dialogs). Not checked on Windows or in dark theme.

