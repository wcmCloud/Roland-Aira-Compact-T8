# Mac App Store version (free)

**Status:** planned · **Effort:** about a day of code and packaging, plus Apple's review

## Is it allowed?

Yes in principle: Avalonia/.NET apps are accepted, and editing your own device's patterns over USB/MIDI is a
normal app category (synth editors are on the store). No in-app purchases are needed for a free app. The
dependencies (Avalonia, DryWetMIDI, CommunityToolkit.Mvvm) are MIT-licensed, which is fine for App Store
distribution, and the editor itself can go out under its MIT option (it's dual-licensed MIT OR GPL-3.0). The editor has no auto-updater, downloads no code and needs no admin rights, all of which the
Mac App Store forbids.

## What would get it rejected today

### 1. The name and Roland's trademarks (guidelines 5.2.1 / 5.2.2)

"Roland Aira Compact T-8 Editor" leads with Roland's trademarks, which reviewers treat as implying Roland made
or endorsed it unless we have Roland's written permission.

- [ ] Choose a store name that leads with our own name and only *describes* compatibility, e.g.
      "Pattern Editor for T-8" or "<OurName> – editor for the AIRA Compact T-8".
- [ ] Description and About: "Not made or endorsed by Roland Corporation. Roland, AIRA and T-8 are trademarks of
      Roland Corporation." (About already says this.)
- [ ] No Roland logos or artwork anywhere (icon, screenshots).
- [ ] Optional: ask Roland for permission, which would allow their names more freely.

### 2. App Sandbox: the T-8 drive

Mac App Store apps must be sandboxed. Inside the sandbox the app can only open files and folders the user picks
in an Open/Save dialog (plus its own container).

Works as is: USB MIDI through CoreMIDI, Open…/Save as… (Avalonia uses the system panels), song files, settings
(stored in the app's container).

Blocked: **Load from T-8** and **Write to T-8**, which go straight to `/Volumes/T-8`.

- [ ] Change both to ask for the T-8 drive in a folder picker the first time ("Choose the T-8 drive"), check it
      looks like a T-8 (`BACKUP/BASS`, `BACKUP/RHYTHM`), and remember the permission as a security-scoped
      bookmark so later loads/writes don't ask again (re-ask if the bookmark is stale or the drive isn't mounted).
- [ ] Keep the current direct access for the non-store builds (a build flag, e.g. `APPSTORE`).

### 3. Opening the website from About

`Process.Start` with `UseShellExecute` launches `open`, and the sandbox doesn't allow spawning processes.

- [ ] Use Avalonia's `TopLevel.Launcher.LaunchUriAsync` instead (also fine outside the store).

## Packaging

- [ ] **Apple Developer Program** membership ($99/year, also for free apps): Apple Distribution certificate,
      Mac Installer Distribution certificate, an App ID, and the app record in App Store Connect.
- [ ] **Bundle ID** for the store build (e.g. `com.wcmcloud.t8editor`), `LSApplicationCategoryType`
      (`public.app-category.music`), `LSMinimumSystemVersion` (12.0 is fine).
- [ ] **Entitlements** (`scripts/appstore.entitlements`):
      `com.apple.security.app-sandbox`, `com.apple.security.files.user-selected.read-write`,
      `com.apple.security.files.bookmarks.app-scope`, `com.apple.security.cs.allow-jit` (for .NET),
      and any others .NET/Avalonia need (check against Avalonia's macOS deployment docs; test in the sandbox).
- [ ] **App icon**: an `.icns` (1024 px master, our own artwork, no Roland logo).
- [ ] **Privacy manifest** (`PrivacyInfo.xcprivacy`) declaring the "required reason" APIs .NET uses
      (e.g. file timestamps), and "Data Not Collected" for the privacy label.
- [ ] **Architectures**: simplest is Apple Silicon only (`osx-arm64`). Intel too means merging two .NET builds into
      a universal app with `lipo` (apphost and every native library).
- [ ] **Script** `scripts/publish-appstore.sh`: `dotnet publish -r osx-arm64 -p:DefineConstants=APPSTORE`, wrap in
      the `.app`, sign every Mach-O and the bundle with the Apple Distribution identity and the entitlements,
      `productbuild --component … --sign "3rd Party Mac Developer Installer: …"` to make the `.pkg`, then upload
      with Transporter or `xcrun altool`/`notarytool` as App Store Connect requires.
- [ ] **Test** the sandboxed build: MIDI out/in to the T-8, Detect, Test, songs, Open/Save, Load/Write to T-8 with
      the folder picker and the remembered bookmark.

## Store listing

- [ ] Name, subtitle, description (with the not-affiliated note), keywords, support URL (GitHub), privacy policy URL
      (a short page: no data collected), screenshots (we can render them from `DocsScreenshots`), age rating.
- [ ] Review notes: explain the app needs a Roland T-8 connected over USB, and that it works with the user's own
      patterns; a short video helps reviewers who don't have the device.

## Related housekeeping (not App Store specific)

- The public repo contains Roland's owner's manual (`docs/T-8_eng01_W.pdf`), which is Roland's copyrighted
  document. Consider removing it and linking to Roland's download page.
- The project is dual-licensed MIT OR GPL-3.0-only. GPL-3.0 alone is widely considered incompatible with the App
  Store's terms, but the MIT option lets the store build be distributed under MIT, so the dual licence is fine.

## Open decisions

- Store name.
- Apple Silicon only, or universal?
- Keep the GitHub (direct download) builds as they are alongside the store build? (Recommended: yes.)
