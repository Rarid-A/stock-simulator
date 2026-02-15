# Stock Simulator (MAUI)

A .NET MAUI stock simulation app with local SQLite persistence.

## Prerequisites

- Windows 10/11
- .NET SDK with MAUI workload
- Android SDK + platform tools (for Android runs)
- ADB USB debugging enabled on device (for physical Android device)

## 1) Setup

From the repo root:

```powershell
dotnet workload install maui
```

Restore/build once:

```powershell
dotnet restore "stock simulator.sln"
dotnet build "stock simulator.sln"
```

---

## 2) Run on Windows

Run the Windows target:

```powershell
dotnet build "StockSimulator/StockSimulator.csproj" -t:Run -f net10.0-windows10.0.19041.0
```

If your machine has multiple architectures, you can specify one:

```powershell
dotnet build "StockSimulator/StockSimulator.csproj" -t:Run -f net10.0-windows10.0.19041.0 -p:RuntimeIdentifier=win-x64
```

---

## 3) Run on Android (emulator or physical device)

### Option A: Run target directly

```powershell
dotnet build "StockSimulator/StockSimulator.csproj" -t:Run -f net10.0-android
```

### Option B: Build + install APK with ADB (reliable fallback)

Build Android app:

```powershell
dotnet build "StockSimulator/StockSimulator.csproj" -f net10.0-android
```

If `adb` is in your PATH:

```powershell
adb devices
adb install -r "StockSimulator/bin/Debug/net10.0-android/com.companyname.stocksimulator-Signed.apk"
adb shell monkey -p com.companyname.stocksimulator -c android.intent.category.LAUNCHER 1
```

If `adb` is **not** in PATH, use default SDK location:

```powershell
$adb = Join-Path $env:LOCALAPPDATA "Android\Sdk\platform-tools\adb.exe"
& $adb devices
& $adb install -r "StockSimulator/bin/Debug/net10.0-android/com.companyname.stocksimulator-Signed.apk"
& $adb shell monkey -p com.companyname.stocksimulator -c android.intent.category.LAUNCHER 1
```

---

## 4) Common issues

- **`adb` not recognized**
  - Add `%LOCALAPPDATA%\Android\Sdk\platform-tools` to PATH, or use the full adb path above.
- **No device shown in `adb devices`**
  - Enable Developer Options + USB Debugging on phone.
  - Confirm USB mode allows data transfer.
  - Accept the RSA prompt on device.
- **MAUI workload missing**
  - Run: `dotnet workload install maui`
- **Android SDK not configured**
  - Open Visual Studio Installer and install MAUI + Android SDK components.

---

## 5) Project targets

Defined in `StockSimulator/StockSimulator.csproj`:

- `net10.0-windows10.0.19041.0`
- `net10.0-android`
