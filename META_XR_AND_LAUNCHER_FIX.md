# Meta XR & Android launcher fixes

## 1. "Single GameActivity application entry" (Unity 2023.2+)

**Warning:** *Always specify single "GameActivity" application entry on Unity 2023.2+.*

**Fix in Unity Editor:**  
**Edit → Project Settings → Meta XR** (or **Meta > Tools > Project Setup Tool**).  
Use **Fix All** or ensure the task that says to use a single GameActivity application entry is applied. That configures the project so the built app correctly uses one GameActivity entry.

---

## 2. "No activity in the manifest with action MAIN and category LAUNCHER"

**Cause:** With Meta XR’s **Remove Gradle Manifest** enabled, the default launcher manifest was removed, so the final APK had no activity with `MAIN` and `LAUNCHER`.

**Changes made:**

- **Custom Main Manifest** and **Custom Launcher Manifest** are enabled in **Project Settings → Player → Android → Publishing Settings** (`useCustomMainManifest: 1`, `useCustomLauncherManifest: 1`).
- **Remove Gradle Manifest** in Meta XR is off (`Assets/Oculus/OculusProjectConfig.asset` → `removeGradleManifest: 0`).
- **Library (main) manifest:** `Assets/Plugins/Android/AndroidManifest.xml` — full manifest (theme, VR category, meta-data, permissions).
- **Launcher manifest:** `Assets/Plugins/Android/launcherTemplate.xml` — declares the MAIN/LAUNCHER activity so the launcher module has a valid entry point.

If Unity does not pick up the launcher template (e.g. it expects a different filename), enable **Custom Launcher Manifest** once in **Edit → Project Settings → Player → Android → Publishing Settings** so Unity creates the template file, then replace its contents with the activity block from `launcherTemplate.xml`.

---

## 3. Other messages

- **Samples directory:** “Could not find Samples directory (Assets/Samples)” — Safe to ignore unless you use package samples. You can create an empty `Assets/Samples` folder to clear the message.
- **Debug symbols:** If you want full crash reports, in **Edit → Project Settings → Player → Android → Other Settings**, set **Debug Symbols** to **Full**.
