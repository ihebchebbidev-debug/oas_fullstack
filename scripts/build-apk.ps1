<#
OAS — one-shot Android APK builder (Windows PowerShell)

  npm run android:apk:win              # debug APK
  npm run android:apk:win -- release   # unsigned release APK

Self-healing: installs missing npm packages, missing Capacitor platform files,
missing Android SDK components and missing icons. You only need Node 18+ and
a JDK 17+ (Android Studio's bundled JBR is auto-detected).
Gradle home is redirected off OneDrive to avoid cache corruption.
#>
param([ValidateSet("debug","release")][string]$Variant = "debug")

$ErrorActionPreference = "Stop"
function Info($m) { Write-Host "[INFO] $m" -ForegroundColor Cyan }
function Ok($m)   { Write-Host "[ OK ] $m" -ForegroundColor Green }
function Warn($m) { Write-Host "[WARN] $m" -ForegroundColor Yellow }
function Die($m)  { Write-Host "[FAIL] $m" -ForegroundColor Red; exit 1 }

Set-Location (Join-Path $PSScriptRoot "..")
$root = (Get-Location).Path

# ── 0a. Node ─────────────────────────────────────────────────────────────────
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { Die "Node.js 18+ is required - https://nodejs.org" }
$nodeMajor = [int](& node -p "process.versions.node.split('.')[0]")
if ($nodeMajor -lt 18) { Die "Node 18+ required, found $(node -v)." }

# ── 0b. Java (Gradle 8.14 supports JDK 17-24 only; Java 25 = class file 69 fails)
function Resolve-JavaHome($dir) {
  # Accepts a JDK home or its bin folder; returns the home if java.exe exists.
  if (-not $dir) { return $null }
  if (Test-Path (Join-Path $dir "bin\java.exe")) { return (Resolve-Path $dir).Path }
  if (Test-Path (Join-Path $dir "java.exe"))     { return (Resolve-Path (Join-Path $dir "..")).Path }
  return $null
}

function Get-JavaMajor($javaDir) {
  $exe = Join-Path $javaDir "bin\java.exe"
  if (-not (Test-Path $exe)) { return 0 }
  # `java -version` writes to stderr; capture through cmd.exe so ErrorActionPreference=Stop doesn't throw.
  $line = (cmd /c "`"$exe`" -version 2>&1" | Select-Object -First 1)
  if ("$line" -match 'version "1\.(\d+)') { return [int]$Matches[1] }
  if ("$line" -match 'version "(\d+)')    { return [int]$Matches[1] }
  if ("$line" -match '(\d+)')             { return [int]$Matches[1] }
  return 0
}

$MIN_JDK = 17; $MAX_JDK = 24   # Gradle 8.14.x compatibility window

$candidates = @()
if ($env:JAVA_HOME) { $candidates += $env:JAVA_HOME }
$candidates += @(
  "$env:USERPROFILE\.oas-jdk\current",
  "$env:ProgramFiles\Android\Android Studio\jbr",
  "$env:ProgramFiles\Android\Android Studio\jre",
  "${env:ProgramFiles(x86)}\Android\Android Studio\jbr",
  "$env:LOCALAPPDATA\Programs\Android Studio\jbr",
  "$env:LOCALAPPDATA\Programs\Android Studio\jre"
)
foreach ($base in @("$env:ProgramFiles\Eclipse Adoptium", "$env:ProgramFiles\Java",
                    "$env:ProgramFiles\Microsoft\jdk", "$env:ProgramFiles\Amazon Corretto",
                    "$env:ProgramFiles\Zulu", "$env:LOCALAPPDATA\Programs\Eclipse Adoptium",
                    "$env:USERPROFILE\.oas-jdk")) {
  if (Test-Path $base) {
    $candidates += (Get-ChildItem $base -Directory -ErrorAction SilentlyContinue |
                    Sort-Object Name -Descending | ForEach-Object { $_.FullName })
  }
}
$j = Get-Command java -ErrorAction SilentlyContinue
if ($j) {
  $src = $j.Source
  try { $item = Get-Item $src -ErrorAction SilentlyContinue
        if ($item -and $item.LinkType -and $item.Target) { $src = @($item.Target)[0] } } catch {}
  if (Test-Path $src) { $candidates += (Split-Path $src) }
}

$javaHome = $null; $javaMajor = 0; $rejected = @()
foreach ($c in $candidates) {
  $h = Resolve-JavaHome $c
  if (-not $h) { continue }
  $m = Get-JavaMajor $h
  if ($m -ge $MIN_JDK -and $m -le $MAX_JDK) { $javaHome = $h; $javaMajor = $m; break }
  if ($m -gt 0) { $rejected += "Java $m ($h)" }
}

# No compatible JDK? Download a private Temurin 21 (no admin rights needed).
if (-not $javaHome) {
  if ($rejected.Count) { Warn ("Ignoring incompatible JDK(s): " + ($rejected -join ", ") + " - Gradle supports $MIN_JDK-$MAX_JDK.") }
  $jdkRoot = Join-Path $env:USERPROFILE ".oas-jdk"
  $target  = Join-Path $jdkRoot "current"
  Info "Downloading a private Temurin 21 JDK into $target (one-time, ~190 MB)..."
  New-Item -ItemType Directory -Path $jdkRoot -Force | Out-Null
  $zip = Join-Path $jdkRoot "temurin21.zip"
  $url = "https://api.adoptium.net/v3/binary/latest/21/ga/windows/x64/jdk/hotspot/normal/eclipse?project=jdk"
  try {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $ProgressPreference = 'SilentlyContinue'
    Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing
    if (Test-Path $target) { Remove-Item $target -Recurse -Force }
    $tmp = Join-Path $jdkRoot "_extract"
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    $inner = Get-ChildItem $tmp -Directory | Select-Object -First 1
    Move-Item $inner.FullName $target
    Remove-Item $tmp -Recurse -Force; Remove-Item $zip -Force
  } catch {
    Die @"
No Gradle-compatible JDK ($MIN_JDK-$MAX_JDK) found and the automatic download failed: $($_.Exception.Message)
Install Temurin 21 manually from https://adoptium.net/temurin/releases/?version=21 then re-run.
"@
  }
  $javaHome = Resolve-JavaHome $target
  $javaMajor = Get-JavaMajor $javaHome
  if (-not $javaHome -or $javaMajor -lt $MIN_JDK) { Die "Downloaded JDK is unusable at $target." }
  Ok "Installed Temurin $javaMajor at $target"
}

$env:JAVA_HOME = $javaHome
$env:PATH = (Join-Path $javaHome "bin") + ";" + $env:PATH
$env:GRADLE_OPTS = "-Dorg.gradle.java.home=`"$javaHome`""



# ── 0c. Android SDK ──────────────────────────────────────────────────────────
$sdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } elseif ($env:ANDROID_SDK_ROOT) { $env:ANDROID_SDK_ROOT } else { $null }
if (-not $sdk) {
  foreach ($c in @("$env:LOCALAPPDATA\Android\Sdk", "$env:USERPROFILE\AppData\Local\Android\Sdk")) {
    if (Test-Path $c) { $sdk = $c; break }
  }
}
if (-not ($sdk -and (Test-Path $sdk))) { Die "Android SDK not found. Install Android Studio and set ANDROID_HOME." }
$env:ANDROID_HOME = $sdk; $env:ANDROID_SDK_ROOT = $sdk
$env:PATH = "$sdk\platform-tools;$sdk\cmdline-tools\latest\bin;" + $env:PATH

# Keep the Gradle cache off OneDrive (file locking corrupts the jar cache).
$gradleHome = Join-Path $env:USERPROFILE "gradle_home"
if (-not (Test-Path $gradleHome)) { New-Item -ItemType Directory -Path $gradleHome -Force | Out-Null }
$env:GRADLE_USER_HOME = $gradleHome
Ok "Node $(node -v) | Java $javaMajor ($javaHome) | SDK $sdk | GRADLE_USER_HOME $gradleHome"

# Required SDK packages, derived from android/variables.gradle when present.
$compileSdk = "36"
if (Test-Path android/variables.gradle) {
  $m = Select-String -Path android/variables.gradle -Pattern 'compileSdkVersion\s*=\s*(\d+)' | Select-Object -First 1
  if ($m) { $compileSdk = $m.Matches[0].Groups[1].Value }
}
$needSdkPkg = -not (Test-Path "$sdk\platforms\android-$compileSdk") -or
              -not (Test-Path "$sdk\platform-tools") -or
              -not (Test-Path "$sdk\build-tools\*")
if ($needSdkPkg) {
  $sdkmanager = "$sdk\cmdline-tools\latest\bin\sdkmanager.bat"
  if (Test-Path $sdkmanager) {
    Info "Installing missing Android SDK components (platform $compileSdk, build-tools, platform-tools)..."
    cmd /c "echo y| `"$sdkmanager`" --licenses" | Out-Null
    & $sdkmanager "platform-tools" "platforms;android-$compileSdk" "build-tools;$compileSdk.0.0"
    if ($LASTEXITCODE -ne 0) { Warn "sdkmanager could not install every component - Gradle will try to resolve them." }
  } else {
    Warn "sdkmanager not found; install 'Android SDK Platform $compileSdk' via Android Studio > SDK Manager if Gradle complains."
  }
}

# ── 1. npm dependencies (install / repair) ───────────────────────────────────
$pkgs = @("@capacitor/core", "@capacitor/cli", "@capacitor/android")
$needInstall = -not (Test-Path node_modules)
foreach ($p in $pkgs) { if (-not (Test-Path "node_modules/$p")) { $needInstall = $true } }
if ($needInstall) {
  Info "Installing npm dependencies..."
  if (Test-Path package-lock.json) { npm ci; if ($LASTEXITCODE -ne 0) { npm install } } else { npm install }
  if ($LASTEXITCODE -ne 0) { Die "npm install failed." }
}
foreach ($p in $pkgs) { if (-not (Test-Path "node_modules/$p")) { Die "$p is still missing after npm install." } }

# ── 2. Icons + splash ────────────────────────────────────────────────────────
if (Test-Path resources/icon-only.png) {
  Info "Generating Android launcher icons and splash screens..."
  npx --yes @capacitor/assets@3 generate --android `
    --iconBackgroundColor '#0b1220' --iconBackgroundColorDark '#0b1220' `
    --splashBackgroundColor '#0b1220' --splashBackgroundColorDark '#0b1220'
  if ($LASTEXITCODE -ne 0) { Warn "Icon generation skipped (keeping existing/default icons)." }
} else { Warn "resources/icon-only.png missing - default Capacitor icon will be used." }

# ── 3. Web build ─────────────────────────────────────────────────────────────
Info "Building web assets (vite build -> dist/)..."
npm run build
if ($LASTEXITCODE -ne 0) { Die "Web build failed." }
if (-not (Test-Path dist/index.html)) { Die "dist/index.html missing - the web build failed." }

# ── 4. Capacitor ─────────────────────────────────────────────────────────────
if ((Test-Path android) -and -not (Test-Path android/gradlew.bat)) {
  Warn "android/ exists but is incomplete - recreating it."
  Remove-Item -Recurse -Force android
}
if (-not (Test-Path android)) {
  Info "Creating native Android project..."
  npx --no-install cap add android
  if ($LASTEXITCODE -ne 0) { Die "cap add android failed." }
}
Info "Syncing web assets + plugins into android/..."
npx --no-install cap sync android
if ($LASTEXITCODE -ne 0) { Die "cap sync failed." }
if (-not (Test-Path android/app/src/main/assets/public/index.html)) { Die "Capacitor sync did not copy dist/ into android/." }

# Ionic's native barcode engine requires Android 8.0+ (API 26). Capacitor 8
# currently generates API 24, so enforce the plugin's requirement after the
# platform is created/synced (also covers a freshly generated android/ folder).
$variablesGradle = "android/variables.gradle"
if (-not (Test-Path $variablesGradle)) { Die "$variablesGradle is missing after cap sync." }
$variablesText = Get-Content $variablesGradle -Raw
if ($variablesText -notmatch 'minSdkVersion\s*=\s*\d+') { Die "minSdkVersion was not found in $variablesGradle." }
$variablesText = $variablesText -replace 'minSdkVersion\s*=\s*\d+', 'minSdkVersion = 26'
# Strip any leading BOM and write WITHOUT one: Windows PowerShell's -Encoding utf8
# emits a BOM, which Gradle's Groovy parser rejects ("Unexpected character: '\uFEFF'").
$variablesText = $variablesText -replace "^\uFEFF", ""
[System.IO.File]::WriteAllText((Resolve-Path $variablesGradle).Path, $variablesText, (New-Object System.Text.UTF8Encoding($false)))
Info "Android minimum SDK set to 26 (required by ionbarcode-android)."


# Gradle finds the SDK through local.properties (more reliable than env vars).
"sdk.dir=" + ($sdk -replace '\\', '\\\\') | Set-Content -Encoding ascii android/local.properties

# ── 5. Gradle ────────────────────────────────────────────────────────────────
$task = if ($Variant -eq "release") { "assembleRelease" } else { "assembleDebug" }
Info "Building $($Variant.ToUpper()) APK (gradle $task)..."
Push-Location android
& ./gradlew.bat --no-daemon --stacktrace clean $task
$code = $LASTEXITCODE
Pop-Location
if ($code -ne 0) { Die "Gradle build failed ($code)." }

$apk = Get-ChildItem "android/app/build/outputs/apk/$Variant" -Filter *.apk -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $apk) { Die "Gradle finished but no APK was found in android/app/build/outputs/apk/$Variant." }
New-Item -ItemType Directory -Path "dist-apk" -Force | Out-Null
$name = "oas-production-$Variant.apk"
Copy-Item $apk.FullName (Join-Path "dist-apk" $name) -Force
Ok ("APK ready: dist-apk/{0} ({1:N1} MB)" -f $name, ($apk.Length / 1MB))
Write-Host "Install with: adb install -r dist-apk/$name"
if ($Variant -eq "release") { Write-Host "Note: the release APK is unsigned - sign it with apksigner before publishing." }
