# PiPlay — Phase 2 Privacy actions design

**Date:** 2026-05-31
**Status:** Approved-with-conditions (three hardening changes incorporated; awaiting spec review)
**Requirements:** REQ-PRIVACY-01 (Reset app state), REQ-PRIVACY-02 (Clear browser data)
**Spec refs:** §19 (Security and privacy), §24 Phase 2, §12.6, `docs/Data_and_Privacy_Map.md`, `docs/QA_Checklist.md` §6

---

## 1. Goal

Ship the two Phase-2 privacy actions as **separate, clearly-worded, confirmed** actions in the
Source Window:

- **Reset app state** (REQ-PRIVACY-01): clear PiPlay's `settings.json` (app settings, saved
  profiles, window placement) while keeping the WebView2 browser session intact. **The user stays
  signed in to YouTube.**
- **Clear browser data** (REQ-PRIVACY-02): a separate, confirmed action that clears PiPlay's
  WebView2 browsing data and **signs the user out of YouTube**.

### 1.1 Sacred invariant — login persists by default

PiPlay keeps the YouTube/Google session across runs by default. The session lives in the WebView2
user-data folder (`%LOCALAPPDATA%\PiPlay\WebView2UserData\`), never in `settings.json`. Having to
re-authenticate (including Google 2FA) on every launch is exactly the experience we are avoiding.

> **The only code path in the entire app that signs the user out is the explicit, confirmed
> _Clear browser data_ action the user deliberately triggers.** Reset does not. Normal close does
> not. A crash does not. This is enforced by tests (see §6.4).

---

## 2. Decisions (already settled)

| Decision | Choice | Rationale |
|---|---|---|
| How _Clear browser data_ wipes the session | **In-place API clear** via `CoreWebView2.Profile.ClearBrowsingDataAsync(AllProfile)` | Logs the user out immediately, reclaims cookies + cache + site storage, no app restart, no file-lock dance. The shared profile means one call covers both windows. |
| Where the actions live | **Gear button → themed Settings window** with a Privacy section | Room for the distinct "what this does / does not do" wording each action needs; the natural home for later Phase-2 sections (profile edit, `Auto`). |
| Confirmation UI | **Themed dark confirm dialog** (`Prompt.AskConfirm`) | The light system `MessageBox` is jarring for a destructive privacy action and off-brand vs. REQ-UI-01. |
| File layout | `SettingsWindow.xaml` in `src/PiPlay/` root | Matches `MainWindow.xaml` / `PlayerWindow.xaml`; the repo has no `Views/` folder. |

---

## 3. Architecture

Follows the established **pure decision-service + thin WPF/WebView adapter** pattern
(`FadePolicy`, `ReturnPolicy`, `PlacementMath`, `NavigationPolicy`, `ProfileService`).

### 3.1 New files

**`src/PiPlay/Services/PrivacyService.cs`** — the testable seam.
- Holds all **user-facing wording as `const string`** for both actions (labels, descriptions,
  confirm titles/bodies/buttons, done titles/bodies, and the not-ready / failed messages). This is
  what makes "worded separately" (REQ-PRIVACY-02) and "wording states the user stays signed in /
  is signed out" regression-testable.
- Holds the chosen browsing-data scope as a constant: `ClearKinds = CoreWebView2BrowsingDataKinds.AllProfile`.
- Exposes one thin adapter: `Task ClearBrowserDataAsync(CoreWebView2 core)` →
  `core.Profile.ClearBrowsingDataAsync(ClearKinds)`.

**`src/PiPlay/SettingsWindow.xaml` / `.cs`** — themed dark Settings window.
- One **Privacy** section, two visually-separated blocks (Reset, then Clear). All visible text is
  **assigned from `PrivacyService` constants in the constructor** (not hardcoded in XAML), so what
  the user sees is exactly what the tests assert (see §6.1, change 1).
- Constructor takes `bool isBrowserReady`. **Only** the `ClearBrowserDataButton` is disabled when
  the browser is not ready; the `ResetAppStateButton` (and the gear) stay enabled — Reset never
  needs the browser.
- Owns its own confirmation step (so cancelling keeps the user in Settings). On a confirmed action
  it records the choice and closes — it does **not** fire events or run async work across the modal
  boundary (see §5, change 3).
- Result surface for the caller:
  ```csharp
  internal enum PrivacyAction { None, ResetAppState, ClearBrowserData }
  internal PrivacyAction RequestedAction { get; private set; } = PrivacyAction.None;
  ```
- Named controls (for markup + WPF tests): `ResetAppStateButton`, `ResetDescriptionText`,
  `ClearBrowserDataButton`, `ClearDescriptionText`.

### 3.2 Changed files

**`src/PiPlay/Services/SettingsService.cs`** — add `AppSettings Reset()`:
- **Atomically** replaces `settings.json` with freshly sanitized defaults by reusing the existing
  temp-file → `Flush(flushToDisk)` → `File.Replace` writer (refactored into a private
  `AtomicWrite(AppSettings)` shared with `Save`). The live file is always either the previous
  content or the new defaults — never absent or half-written (change 2 = atomic).
- Best-effort removes stale `*.corrupt.*.json` quarantine files and any leftover `.tmp`.
- **Touches only the settings-file path.** It never references `AppPaths.WebView2UserDataDir`,
  never references `AppPaths.LogsDir`, and never creates either (change 2 = cannot recreate the
  WebView2 folder; §6.4 tests this).
- Returns the fresh sanitized `AppSettings`.

**`src/PiPlay/MainWindow.xaml`** — add a `SettingsButton` to the title-bar caption strip:
`[⚙][_][▢][✕]`. `Style="{StaticResource IconButton}"`, gear glyph `&#xE713;`,
`ToolTip="Settings"`. It sits inside the existing `WindowChrome.IsHitTestVisibleInChrome="True"`
strip so it is clickable in the custom caption.

**`src/PiPlay/MainWindow.xaml.cs`**:
- Change `private readonly AppSettings _settings;` → `private AppSettings _settings;` so reset can
  reassign it. (Nothing caches a stale reference; `PlayerWindow` receives copied values, not the
  settings object.)
- `SettingsButton_Click` opens the modal Settings window and dispatches its result (see §5).
- `ApplyResetState()` — the live reset application: `_settings = _settingsService.Reset();`
  `LoadProfilesIntoCombo();` `ApplyTopmost(false);`. **References no `Browser`/WebView2 member and
  queues no navigation.** Internal so a WPF test can call it with a null `CoreWebView2` (§6.4).
- `PerformClearBrowserDataAsync()` — re-checks readiness, closes any open popout, awaits the clear,
  reloads home, reports result (see §5).
- Test accessor: `internal string? PendingUrlForTests => _pendingUrl;` (proves reset queued no
  navigation).

**`src/PiPlay/Prompt.cs`** — add two themed dialogs reusing the dark styles:
- `bool AskConfirm(Window owner, string title, string message, string confirmText, bool danger = false)`
  — dark Yes/No; `danger` styles the confirm button as destructive. Sets `Owner` and matches the
  owner's `Topmost` so a pinned PiPlay does not occlude it.
- `void ShowInfo(Window owner, string title, string message)` — dark OK dialog for the done /
  not-ready / failed messages (same owner/topmost handling).

---

## 4. Data flow

1. User clicks the **gear** → `SettingsWindow` opens modal (`Owner = MainWindow`,
   `Topmost = owner.Topmost`, `WindowStartupLocation = CenterOwner`). The **Clear browser data**
   button is enabled only when `_browserReady && Browser.CoreWebView2 is not null`.
2. **Reset app state…** → themed confirm (keeps-you-signed-in wording) → on confirm, the window
   sets `RequestedAction = ResetAppState` and closes → `MainWindow` runs `ApplyResetState()` →
   shows the done dialog. WebView2 is untouched → still signed in.
3. **Clear browser data…** → sterner themed confirm (`danger`, signs-you-out wording) → on confirm,
   the window sets `RequestedAction = ClearBrowserData` and closes → `MainWindow` runs
   `PerformClearBrowserDataAsync()` → close popout if open → `ClearBrowserDataAsync(AllProfile)` →
   reload home → done dialog. Now signed out.

---

## 5. Hardening of the clear/reset UI flow (change 3)

The Settings window is **result-based, not event/async-based**: it confirms, records
`RequestedAction`, and closes; `MainWindow` performs the work **after** the modal closes. This
removes every "async work under stacked modals" hazard by construction.

- **Double-clicks / re-entrancy:**
  - A single `bool _privacyActionInProgress` guard in `MainWindow` wraps both actions (set true on
    entry, reset in `finally`). The gear handler returns early while it is set.
  - The Settings window's action buttons disable themselves on click before the confirm appears;
    the confirm is itself modal.
  - During the async clear, the gear (`SettingsButton`) is disabled and re-enabled in `finally`,
    so a second Settings window cannot open mid-await.
- **Stale browser readiness:** initial button enablement comes from current readiness, but
  `PerformClearBrowserDataAsync()` **re-checks `_browserReady && Browser.CoreWebView2 is not null`
  at execution time** (not the cached enabled state). If not ready, it shows
  `PrivacyService.ClearBrowserNotReady` and aborts — no exception.
- **Failed async operations:** the clear is wrapped in try/catch. On failure it logs and shows
  `PrivacyService.ClearFailed`; the success dialog is shown **only** on success. The post-clear
  `NavigateInternal(home)` is wrapped separately so a navigation hiccup cannot mask a successful
  clear. State is always restored in `finally`.
- **Modal-owner weirdness:** Settings and both `Prompt` dialogs set `Owner` and match the owner's
  `Topmost`, use `CenterOwner`, and `ShowInTaskbar=false`. No async runs while two modals are
  stacked (the result-based flow runs the action after the window closes).

---

## 6. Testing — layered, matching the existing suite

`dotnet test`; lanes by `[Trait("Category", …)]` = `Markup` / `Logic` / `Wpf`; Layer 4 = manual
smoke. The three approved conditions each get explicit coverage.

### 6.1 Change 1 — visible wording comes from the tested constants
- **Logic** `PrivacyServiceTests`: assert the reset wording (description + confirm body + done body)
  states the user stays signed in and never says "sign out"; the clear wording states the user is
  signed out; and the two actions are worded distinctly (labels, descriptions, and confirm bodies
  all differ) — encodes REQ-PRIVACY-02 "worded separately". Assert `ClearKinds == AllProfile`.
- **Wpf** `SettingsWindow_shows_tested_privacy_wording`: construct the window (never shown) and
  assert `ResetDescriptionText.Text == PrivacyService.ResetDescription`,
  `ClearDescriptionText.Text == PrivacyService.ClearDescription`, and the button contents equal the
  labels. This closes the loop: the pixels the user reads are the strings the Logic tests pin.

### 6.2 Change 2 — atomic reset / no WebView2 recreation
- **Logic** `SettingsServiceTests` (temp root via the existing pattern):
  `Reset_recreates_file_with_defaults` (populate, reset, assert returned + on-disk are defaults);
  `Reset_preserves_logs` (drop `logs/piplay.log`, reset, assert it survives);
  `Reset_does_not_create_WebView2_user_data_dir` (no folder present → reset → still absent).

### 6.3 Change 2 + sacred invariant — reset never logs the user out
- **Logic** `Reset_preserves_WebView2_user_data` (drop a sentinel file under
  `WebView2UserData\`, reset, assert it survives). **This is the login-persistence invariant
  encoded as a test.**

### 6.4 Change 2 — reset has no browser dependency / queues no navigation
- **Wpf** `Reset_applies_without_a_live_browser`: construct `MainWindow` (its `CoreWebView2` is
  null because the window is never shown), call `ApplyResetState()`, assert: no throw,
  `ProfilesCombo.Items` empty, `PinToggle.IsChecked == false`, `PendingUrlForTests == null` (no
  navigation queued), and `Browser.Source == null` (unchanged).

### 6.5 Markup (Layer 1) wiring
- Add `"SettingsButton"` to `MainWindow.xaml` `RequiredNames` and to the
  `Caption_and_toolbar_controls_have_tooltips` list.
- Add a `RequiredNames` entry for `SettingsWindow.xaml`:
  `ResetAppStateButton`, `ResetDescriptionText`, `ClearBrowserDataButton`, `ClearDescriptionText`.
- Add `"SettingsWindow.xaml"` to the `Every_StaticResource_reference_is_defined` file list so its
  resource references are validated and its text meets WCAG contrast via the theme tokens.
- The gear button is automatically covered by `Glyph_controls_use_the_icon_font` for
  `MainWindow.xaml` (it scans every glyph-bearing button and requires an icon-font style).
- Add `SettingsWindow_is_not_transparent`: assert `SettingsWindow.xaml` has no
  `AllowsTransparency="True"` (consistency; it does not host WebView2).

### 6.6 Wpf (Layer 3) construction + enablement
- `SettingsWindow_constructs_without_throwing`.
- `SettingsWindow_disables_only_clear_when_browser_not_ready`:
  `new SettingsWindow(isBrowserReady: false)` → `ResetAppStateButton.IsEnabled` true,
  `ClearBrowserDataButton.IsEnabled` false; `isBrowserReady: true` → both enabled.
- `MainWindow_exposes_settings_button`: `FindName("SettingsButton")` is a `Button`.

### 6.7 Layer 4 — manual smoke + QA checklist
- Update `docs/QA_Checklist.md` §6 rows for REQ-PRIVACY-01/02 into concrete steps, and add a UIA
  smoke to `scripts/Test-UiSmoke.ps1`: open Settings → Reset (verify still signed in) → Clear
  browser data (verify signed out) → confirm the on-screen wording.

---

## 7. Error handling

File I/O (`SettingsService.Reset`) and the WebView2 call (`ClearBrowserDataAsync`) are wrapped,
logged, and never crash (Q-6). Clear is unreachable when the browser is not ready (button disabled
+ execution-time re-check). Reset always applies defaults in memory even if the disk write fails.

---

## 8. Scope / YAGNI

- No "clear logs" toggle — the data map keeps logs through reset by default.
- No restart-and-delete flow — the in-place API clear is sufficient and robust.
- No profile edit / `Auto` / export here — Settings window is Privacy-only but structured so those
  Phase-2 sections drop in later.
- No telemetry, no network calls.

---

## 9. Affected files (summary)

| File | Change |
|---|---|
| `src/PiPlay/Services/PrivacyService.cs` | **New** — wording constants, `ClearKinds`, `ClearBrowserDataAsync`. |
| `src/PiPlay/SettingsWindow.xaml` / `.cs` | **New** — themed Settings window, Privacy section, `PrivacyAction` result. |
| `src/PiPlay/Services/SettingsService.cs` | Add atomic `Reset()`; extract shared `AtomicWrite`. |
| `src/PiPlay/MainWindow.xaml` | Add `SettingsButton` gear to the caption strip. |
| `src/PiPlay/MainWindow.xaml.cs` | Gear handler, `ApplyResetState()`, `PerformClearBrowserDataAsync()`, `_settings` non-readonly, `PendingUrlForTests`. |
| `src/PiPlay/Prompt.cs` | Add `AskConfirm` and `ShowInfo` themed dialogs. |
| `tests/PiPlay.Tests/PrivacyServiceTests.cs` | **New** — wording + `ClearKinds` (Logic). |
| `tests/PiPlay.Tests/SettingsServiceTests.cs` | Add reset / preservation tests (Logic). |
| `tests/PiPlay.Tests/Ui/XamlInvariantTests.cs` | Add `SettingsButton`, `SettingsWindow.xaml` wiring (Markup). |
| `tests/PiPlay.Tests/Ui/WpfRuntimeTests.cs` | Add Settings-window + reset-without-browser tests (Wpf). |
| `docs/QA_Checklist.md`, `scripts/Test-UiSmoke.ps1` | Phase-2 privacy smoke (Layer 4). |
| `docs/CHANGELOG.md`, `VERSION`, `BUILD_NUMBER` | Release notes + version bump on ship. |
