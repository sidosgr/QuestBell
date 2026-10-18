# Full-Screen Reminders — Product Specification

Status: implemented as QuestBell 1.5.0. This document preserves the planning decisions; README.md and the bilingual HTML guide describe the delivered behavior. Source code, installer, reports and screenshots are in this workspace.

## Purpose

A lightweight Windows 10 and 11 task reminder app for obligations such as paying bills and cancelling subscriptions. A reminder fills the screen and remains pending until Done, Snooze, or an explicit secondary action. The experience should be unusually fun and visually impressive while remaining dependable and easy to use.

## Platform and defaults

- Windows 10 and Windows 11. Exact minimum builds remain to be selected.
- Dark mode by default.
- English by default; Greek selectable in Settings.
- Translate interface text, built-in phrases, templates, and playful supporting messages. Preserve user-written text when switching language.
- Works offline with local storage; no account required for the first version.
- Export/import for backups. File format and merge/replace behavior remain to be specified.
- Start automatically with Windows, quietly in the system tray; configurable in Settings.
- Closing the management window leaves the app running in the tray.
- Design for low memory use and near-zero idle CPU. Numeric performance budgets require a prototype and measurement.

## Main interface

- Clean, modern management interface with pixel-art accents.
- Task list grouped into Overdue, Today, and Upcoming. Exact grouping rules for separate due dates remain to be specified.
- History records completion timestamps and skipped occurrences, and supports recovery from accidental completion.
- Settings covers language, appearance, sound, startup, monitor selection, and reminder behavior.
- Full-screen reminder screens carry the richest artwork and animation.

## Minimal-typing reminder composer

- Assemble reminders from ready-made dropdown phrases or selectable chips.
- Example: Pay + Phone + Bill.
- Example: Cancel + Phone + Account.
- Suggest sensible combinations as selections change.
- Update the matching illustration as the title is assembled.
- Offer complete templates such as Pay electricity bill and Cancel subscription.
- Show recently used template choices first.
- Allow custom phrase entries and fully custom reminder text.
- Save custom entries automatically for reuse; allow rename and delete.
- Present the assembled reminder as a playful quest card.

## Reminder information

- Title and reminder date/time.
- Optional recurrence.
- Optional due date, separate from the reminder time.
- Optional amount, website link, and short note; hidden behind optional detail controls until needed.
- Automatically selected artwork, with manual alternatives and an option to pin a favorite.
- Optional per-task automatic follow-ups, off by default. Exact follow-up intervals and their interaction with the visible reminder need specification.

## Scheduling

- One-time and recurring tasks.
- Date shortcuts: Today, Tomorrow, Next week, Pick a date.
- Time presets and a custom time picker. Preset clock times remain to be chosen.
- Relative scheduling uses grouped options:
  - Minutes: 5, 10, 15, 30, 45.
  - Hours: 1, 2, 3, 6, 12.
  - Days: 1, 2, 3, 5.
  - Weeks: 1, 2, 3.
  - Months: 1, 3, 6, 12.
  - Custom number and unit, or an exact date/time.
- Recurrence: daily, weekly, monthly, yearly, selected weekdays, and every X days/weeks/months.
- Monthly schedules on the 29th–31st use the last available day in shorter months, returning to the original date in months where it exists.
- Recurrence follows the original schedule by default.
- Optional Repeat after completion schedules the next occurrence from completion instead.
- Calendar arithmetic, leap-day handling, timezone changes, and daylight-saving rules need explicit implementation rules.

## Full-screen behavior

- Large readable task title, optional details, one main illustration, and prominent Done and Snooze buttons.
- No automatic timeout of an ordinary visible reminder.
- Default display: primary monitor.
- Settings also allows one selected monitor or all connected monitors.
- When displayed on multiple monitors, an action on one closes the other views of that occurrence.
- Show waiting reminders one at a time, oldest first, with a small count of additional waiting reminders.
- Short protection against accidental actions from input already in progress is a proposed implementation detail, to validate in a prototype.

### Existing full-screen applications

- If a video or application is full-screen when a reminder is due, launch the reminder in the background without stealing focus.
- Keep it silent while waiting in the background.
- Bring it forward only after that same application leaves full-screen mode.
- Switching to another application alone must not release the deferred reminder.
- Detection across multiple monitors, application termination, and a second full-screen application need explicit rules and testing.
- A desktop app cannot guarantee foreground takeover in every Windows or game context; verify visibility separately from keyboard focus.

## Reminder actions

### Done

- Complete the current occurrence immediately.
- Provide a brief Undo action and later recovery through History.
- For recurring tasks, preserve the recurrence according to the chosen schedule mode.
- Play a brief satisfying pixel animation and optional playful supporting text such as Quest complete!

### Snooze

- Keep the occurrence pending and postpone its next appearance.
- Offer the same relative-time range as reminder creation and a custom date/time.
- Show short options first, with longer intervals grouped below.
- Escape snoozes using the preferred duration; default is 10 minutes.
- Preserve the snoozed time across restarts.
- Snoozing an occurrence preserves its underlying recurrence. Handling overlap with a later occurrence needs an explicit rule.
- Animate the illustration going to sleep.

### Skip this time

- Secondary action for recurring tasks.
- Skip the current occurrence without marking it complete or stopping future reminders.
- Record the occurrence as skipped in History.

### Open website

- Open the optional website link in the browser.
- Hide the full-screen reminder so the user can interact with the browser; task remains pending.
- Provide Return to reminder from the tray.
- Reappear after 10 minutes by default; delay configurable in Settings.
- Respect the full-screen application deferral rule when returning.
- Opening a link never marks a task Done.

## Sound

- One gentle chime when a reminder becomes visible.
- Global mute and per-reminder sound options.
- No chime while deferred behind an existing full-screen application.

## Missed reminders and background operation

- Show reminders missed during sleep or shutdown after the user returns and the app is running.
- Present them oldest first.
- Combine repeated missed occurrences of the same task into one reminder instead of flooding the user.
- Tray supports opening the app, creating a reminder, viewing the next reminder, pausing, returning to a hidden reminder, and exiting.
- Pause presets proposed earlier: 30 minutes, 1 hour, and until tomorrow. Exact pause/resume handling remains to be specified.
- Clearly communicate that exiting the app stops reminder delivery while it is closed.
- Persist task state and changes so crashes or restarts do not silently lose pending work.

## Pixel-art direction

- One visually striking main illustration per reminder.
- Detailed shading, strong silhouettes, rich colors, crisp pixel scaling, and subtle animation.
- Mix cozy/charming and dramatic fantasy/game art according to the task, using a consistent palette and visual language.
- Choose relevant art automatically from a bundled offline collection, based on built-in phrases, categories, and task text.
- Include items, animals, and characters as appropriate; they are reminder artwork, not a separate desktop pet.
- Provide many variations per category across subject, scene, color, and animation.
- Avoid recent repeats where suitable alternatives exist.
- Recurring tasks receive fresh relevant artwork for each occurrence unless the user pins a favorite.
- Offer a small selection of relevant alternatives and access to the wider collection.
- Exact category list, asset counts, animation budgets, and art production approach remain open.

Examples of art direction:

- Bills: a coin-filled treasure chest or jeweled phone.
- Cancellation: enchanted scissors cutting a ribbon.
- Deliveries: an expressive parcel or messenger bird.
- General tasks: a friendly animal or character.

## Playful experience

- The app should feel fun and different throughout, while making obligations clear.
- Playful supporting text with conventional action labels such as Done and Snooze.
- Brief celebrations on completion and sleep animations on snooze.
- Optional collection of pixel-art keepsakes unlocked by completing tasks.
- No streak penalties or guilt-based messaging.
- Collection unlock rules remain to be designed.
- Keep essential text readable and actions consistent across themes.
- Reduced-motion support is a proposed accessibility requirement.

## Proposed implementation direction

The initial recommendation is C# with WPF, a tray icon, local persistence, and an event-driven scheduler. This is a proposal, not a finalized technology decision. Prove scheduling, full-screen application deferral, multi-monitor behavior, and idle resource use before producing a large artwork library.

## Remaining design work

- Delivered: seven categories, 25 templates, editable phrase vocabulary, and 76 illustrations.
- Full-screen deferral tracks existing window handles, retains minimized blockers, releases on normal bounds or termination, and also respects newly detected full-screen windows. Shell/desktop windows and captioned maximized windows are excluded.
- A recurring task has one pending occurrence. Snooze can span repeats; Done/Skip advances to the next future occurrence. Due dates preserve their offset to the occurrence when advancing.
- Optional automatic follow-ups repeat a chime while the reminder is visible; no duplicate windows are created.
- Calendar arithmetic and daylight-saving behavior are documented in the guide. Overdue includes reminders scheduled before today or with a due date before today.
- Pause retains pending tasks. Import validates and replaces after confirmation, preserving the previous data.
- Target: x64 Windows 10 22H2 and Windows 11, .NET 10 bundled in the installer. Cold scheduler-only idle measurements and verification reports are in artifacts.
- Further release work: code signing, Windows 10 hardware validation, and broader multi-monitor/exclusive-game checks.
