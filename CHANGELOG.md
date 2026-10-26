# Changes

## 1.5.1

- Centered the sidebar tagline beneath the QuestBell branding in English and Greek.
- Centered the reminder time section between Date and Repeat, aligned both relative-delay buttons on the left as Remind me in…, and added a centered OR label when more timing choices are expanded.
- Made reminder and snooze date fields narrower than one third of the timing row, with equal date shortcuts spanning the input width and clock shortcuts directly below the time input.
- Changed default clock shortcuts to 09:00, 12:00, 17:00 and 21:00. Existing unmodified defaults upgrade automatically; customized shortcut sets stay as configured.
- Moved the relative-delay action to the left of its dropdown and added English/Greek help icons with hover, click and keyboard hints.
- Widened the snooze dialog to keep date controls readable and fixed its initial date when the suggested time falls after midnight.

## 1.5.0

- Added Update now in Settings and the update banner: download directly from GitHub, verify size and SHA-256, install, and reopen QuestBell.
- Added download progress, cancellation, safe retry, bounded trusted redirects and cleanup of partial/stale downloads. Unverifiable installers cannot run.
- Added installer update mode with automatic progress and restart, preserving reminder data, language and the user's Windows startup choice.
- Updated English/Greek controls, guide and download, installer and UI regression coverage.
## 1.4.0

- Added Settings → Your quick choices to add/remove reusable custom reminders with Personal/Work mode, an artwork family and a default repeat. They appear in step 1 of the reminder wizard; created reminders remain independent of their shortcut.
- Added customizable quick date, clock-time and relative-delay buttons. Users can remove any default, add new choices or restore default time buttons. Removing all buttons keeps the normal date/time controls available.
- Quick choices save immediately, remain local to each PC and are included in backup/restore. Older saved data receives the existing default buttons automatically.
- Added English/Greek settings, validation, persistence, actual add/remove/create tests and updated guide instructions.

## 1.3.1

- Added a GitHub update checker targeting sidosgr/QuestBell stable releases, with numeric version comparison and handling for no public release, network failure and rate limits.
- Added English/Greek update settings, a manual check button, an optional daily automatic check and a quiet in-app update banner. The release page opens for user-controlled download and installation.
- Update requests send no task data. Release links are restricted to the configured repository; drafts, prereleases and older/equal versions are ignored.
- Added fixture-based HTTP/version/link validation tests and GitHub publishing instructions. Generated build/test artifacts are excluded from source uploads.

## 1.3.0

- Added 24 impressive work illustrations: 12 objects and 12 companion pets across email, shipping, blog writing, newsletters, social content and office tasks. The full library now contains 100 illustrations.
- All 26 work templates automatically choose their dedicated task artwork. Custom English/Greek work titles also recognize common work subjects. All new choices have bilingual names, rotation and pinning support.
- Preserved existing artwork IDs and collection order. The original transparent PNGs remain unchanged; assets load on demand using the existing bounded image cache.
- Expanded the offline guide gallery to 25 atlases, with matching framing and updated English/Greek descriptions.
- Added work artwork selection, custom-text matching, stable ID and asset validation checks.

## 1.2.0

- Added optional Work mode and 26 ready-made bilingual work reminders, with client/order/product/topic references and editable final wording in the existing three-step wizard.
- Covers client contact, invoices, order review/shipping, products/supplies, blog posts, weekly newsletters, social media and project routines. Weekly templates offer editable weekly recurrence; shipping uses the real scheduling controls.
- Added dashboard All / Work / Personal filters and work labels. The default composer mode is local to each PC.
- Work reminders retain template metadata for English/Greek display, backup and sync, with relevant existing task artwork. Sync format 2 prevents older peers from discarding work fields; all connected PCs should update to 1.2.0 or later.
- Added work template, localization, artwork, persistence, custom editing, scheduling and sync tests, and updated the bilingual guide.

## 1.1.0

- Added optional cloud-folder sync and automatic dated backups, both off by default, with independent switches and English/Greek settings.
- Syncs reminders, completion, snooze, history and custom phrases through an existing cloud desktop client. Display/startup/pause preferences remain local. No QuestBell cloud account is required.
- Uses per-device documents, logical record versions, deterministic merging and deletion markers. Simultaneous edits select a whole record; previous local data is preserved before remote changes.
- Keeps 30 cloud backups per PC and 30 local recovery copies. Restore pauses cloud activity and retains the existing before-import recovery copy.
- Adds folder status, Check now, safe retries after unavailable/partial files, and protection against applying changes while a composer is open or local data changes during a read.
- Updated the bilingual guide and added multi-device merge, backup retention, invalid data, folder exchange, offline reconnect and disabled-mode tests.

## 1.0.6

- Replaced the single reminder form with a three-step What → When → Review wizard, in English and Greek, for creation and editing.
- Kept templates, custom phrases/titles, all timing and repeat options, due dates, amounts, links, notes, artwork and sound. Detailed options expand within their relevant step.
- Added a schedule review, Back/Next navigation that preserves input, and step validation. Invalid optional details expand for correction; nothing is saved before the final action.
- Renamed the dashboard reminder action to Mark as done (Σήμανση ως ολοκληρωμένη). Full-screen Done/Snooze labels are unchanged.
- Updated the offline bilingual guide and added navigation, validation, creation, edit preservation and cancellation checks.

## 1.0.5

- Simplify the new/edit reminder form: keep task wording, date/time, everyday date shortcuts and basic recurrence visible, with a more compact artwork preview.
- Place timing presets/custom delays, detailed repeat rules, optional details, and artwork/sound under one **More options** disclosure. Show configured settings in its collapsed summary and preserve all values when toggling or saving.
- Automatically expand the detailed section when selected weekdays need choosing. Custom-title mode hides unused phrase controls and restores their choices when switched back.
- Keep the Create/Save and Cancel buttons visible in a fixed footer. Verify the simple layout at standard and compact sizes in English and Greek, and save/reload a configured reminder with details collapsed.
- Update the bilingual offline guide and installer.

## 1.0.4

- Add 48 detailed pixel illustrations: 24 task objects and 24 pets, bringing the offline library to 76. Phones, utilities, rent/tax, internet/cloud, streaming/software, gym/self-care, groceries/deliveries, doctor/dentist, cars/driving, filters/plants, passports/documents, and family events now have specific artwork.
- Match automatic artwork to the actual reminder subject, including accented Greek and English custom titles. Rotate related illustrations without immediate repeats and show relevant thumbnails before the first reminder fires.
- Replace numbered variations with named English/Greek artwork choices, with task matches listed first. Existing pins and backups remain compatible; selecting a new template restores automatic matching.
- Frame new sprites to exclude neighboring atlas fragments without modifying the original PNGs. Load sheets on demand with a six-sheet cache and page the optional collection, preserving the original 28 unlock positions.
- Expand the bilingual offline guide with all 19 artwork sheets, and verify matching for every template, new artwork backup round trips, transparency, and WPF previews in both languages.

## 1.0.3

- Replace shortcut creation through reflected Windows Script Host calls with the native Unicode Windows Shell Link API.
- Run the installer's background work on a dedicated STA thread so Shell objects use the appropriate COM apartment.
- Treat unavailable desktop/Start-menu shortcuts, denied optional Windows integration and old-version cleanup as logged notes. These no longer fail an otherwise installed app.
- Keep essential extraction/data failures fatal and prepare writable data before replacing the previous app.
- Always log setup steps and complete exceptions to `%TEMP%\QuestBell-setup.log`. Unwrap invocation/task exceptions in fatal error messages and provide an in-installer View setup details link.
- Add 27 installer regression checks exercising real native shortcut creation/readback and save failure, Greek and redirected paths, isolated Windows registration, upgrades, simulated policy/access restrictions, archive validation and data preservation. Earlier package-only checks did not exercise shortcut creation.

## 1.0.2

- Replace the default Windows context-menu frame, which painted a light strip over the left side of the reminder action menu.
- Add visible vector icons for Edit, Preview, Snooze, Skip, Disable and Delete, with a dedicated icon column and a contrasting delete action.
- Size the popup to its full English or Greek labels and anchor it below the more-actions button. Add a localized tooltip and accessible button name.
- Render and inspect the actual menu layout at 100%, 150% and 200% scale in both languages, checking icon/text separation and the former light gutter.

## 1.0.1

- Fix the first due reminder failing with a Windows callback marshaling error. The full-screen scan now uses a non-generic Win32 delegate with the correct calling convention.
- Scan before changing delivery state, and clear partial presentation state after errors, so an unsuccessful attempt can be retried.
- Apply the shared dark window style explicitly to derived window classes, removing the light client-area frame around padded content. Use dark native frame/caption colours where Windows supports them.
- Enlarge the main window from 1220×850 to 1440×980 and the reminder composer from 820×850 to 1100×1000. Fit each to its monitor's usable area and DPI so controls remain reachable. Enlarge the snooze picker as well.
- Test the real production window-enumeration path, scan-failure recovery, window backgrounds, and screen-aware dimensions. Include window backgrounds in screenshots rather than only rendering inner content.
- An in-place install retains existing reminders, history and preferences.

## 1.0.0

Initial offline English/Greek reminder app and bundled pixel artwork.
