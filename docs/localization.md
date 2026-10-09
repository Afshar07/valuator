# Desktop localization

English (`en`) is the deterministic default; Persian (`fa`) uses RTL. The desktop
locale context persists the language in `settings.json` under the app data directory
(the theme preference shares that file under its own key).
The selector updates existing controls live, preserving unsaved edits. Switching is
disabled during agent work to keep the request language fixed for that job.

## Boundaries

- Embedded JSON dictionaries are loaded by `LocalizationService`, never by views.
  Use stable semantic keys and format placeholders, not English text as identifiers.
  Missing keys fall back to English, then a visible `[key]` marker.
- Domain display mapping localizes enums and built-in template labels by stable IDs.
  It does not write translations back into domain objects. User-entered names,
  notes, task titles and custom requirement labels are not translated.
- Window `FlowDirection` controls inherited layout direction. Do not manually
  reverse rows or navigation. Keep paths, protocol previews and technical details
  explicitly LTR; freeform user prose follows the selected locale.
- The date formatter owns display formatting. Dates remain Gregorian `DateTimeOffset`
  instants in SQLite. Persian displays Jalali (Solar Hijri) dates and English
  Gregorian; `JalaliDate` converts at the entry/display boundary only, using the
  framework `PersianCalendar` (no extra dependency). The Persian task and milestone
  dialogs use `JalaliDatePicker`, which accepts typed Jalali or Gregorian dates
  (Persian/Arabic-Indic digits, `/ - .` separators; years below 1700 read as Jalali)
  and shows the Gregorian equivalent. The Calendar page uses Jalali months in Persian.
  `ParseLocalDate` accepts `yyyy-MM-dd HH:mm` and the same text with a Jalali date.
  Avalonia ignores Unicode directional isolates, so number runs inside RTL text use
  left-to-right marks (`JalaliDate.KeepLeftToRight`) and the short date a leading
  right-to-left mark.
- Agent response-language instructions are separate from UI resources. Localized
  predefined action labels use stable action IDs; internal prompts stay stable.
  The model is instructed to localize prose (including proposal titles/descriptions)
  but preserve JSON keys, the `task-proposals` tag and ISO timestamps. No generated
  JSON is translated after generation.

Application services accept an explicit `AgentResponseLanguage` preference; they
do not read desktop resources or ambient thread culture. Runtime adapters forward
the assembled instructions unchanged. A locale switch therefore affects new jobs,
not the meaning or storage format of existing job history.

## Adding labels

Add matching keys and placeholders to both language dictionaries under
`src/ProjectOperations.Desktop/Localization/Resources`. Resolve them through the
localization boundary and the desktop's live control-binding helper. When adding
an enum member, extend its presentation mapping/resources rather than showing
`ToString()` or changing the persisted value. Keep English/Persian resource parity
covered by desktop tests.

Existing domain/template data is not migrated for language changes. Historical
agent prose and user data retain their original language. Operating-system file
dialogs may follow OS language rather than the app's locale.
