#!/usr/bin/env bash
#
# OAS — one-shot Android APK builder (Linux / macOS / WSL / Git-Bash)
#
#   ./scripts/build-apk.sh            # debug APK (installable, no signing needed)
#   ./scripts/build-apk.sh release    # unsigned release APK
#   CAP_LIVE_RELOAD=1 ./scripts/build-apk.sh   # APK that loads the Lovable preview URL
#
# The script is self-healing: it installs missing npm packages, missing
# Capacitor platform files, missing Android SDK components and missing icons.
# The only things it cannot install for you are Node.js and a JDK 17+.
set -euo pipefail

cd "$(dirname "$0")/.."
ROOT="$(pwd)"
VARIANT="${1:-debug}"
case "$VARIANT" in debug|release) ;; *) echo "Usage: $0 [debug|release]"; exit 2 ;; esac

info()  { printf '\033[36m[INFO]\033[0m %s\n' "$*"; }
ok()    { printf '\033[32m[ OK ]\033[0m %s\n' "$*"; }
warn()  { printf '\033[33m[WARN]\033[0m %s\n' "$*"; }
die()   { printf '\033[31m[FAIL]\033[0m %s\n' "$*" >&2; exit 1; }

trap 'code=$?; [ $code -ne 0 ] && printf "\033[31m[FAIL]\033[0m build aborted (exit %s) at line %s\n" "$code" "$LINENO" >&2; exit $code' ERR

# ── 0a. Node ────────────────────────────────────────────────────────────────
command -v node >/dev/null || die "Node.js 18+ is required — https://nodejs.org"
NODE_MAJOR="$(node -p 'process.versions.node.split(".")[0]')"
[ "$NODE_MAJOR" -ge 18 ] || die "Node 18+ required, found $(node -v)."
command -v npm >/dev/null || die "npm is required (ships with Node.js)."

# ── 0b. Java (Gradle 8.14 supports JDK 17-24; Java 25 = class file 69 fails) ─
jmajor() { [ -x "$1/bin/java" ] || { echo 0; return; }
  "$1/bin/java" -version 2>&1 | head -1 | sed -E 's/.*version "1\.([0-9]+).*/\1/; s/.*version "([0-9]+).*/\1/; s/[^0-9]//g' | head -1; }

find_java() {
  local cands=() c m
  [ -n "${JAVA_HOME:-}" ] && cands+=("$JAVA_HOME")
  cands+=("$HOME/.oas-jdk/current" \
    "/Applications/Android Studio.app/Contents/jbr/Contents/Home" \
    "$HOME/Applications/Android Studio.app/Contents/jbr/Contents/Home" \
    "/opt/android-studio/jbr" "/usr/local/android-studio/jbr" \
    "/usr/lib/jvm/java-21-openjdk-amd64" "/usr/lib/jvm/java-17-openjdk-amd64" \
    "/usr/lib/jvm/default-java" "$HOME/.sdkman/candidates/java/current")
  for c in /usr/lib/jvm/*; do [ -d "$c" ] && cands+=("$c"); done
  command -v java >/dev/null && cands+=("$(dirname "$(dirname "$(readlink -f "$(command -v java)")")")")
  for c in "${cands[@]}"; do
    m="$(jmajor "$c" 2>/dev/null || echo 0)"
    if [ "${m:-0}" -ge 17 ] 2>/dev/null && [ "${m:-0}" -le 24 ]; then echo "$c"; return; fi
  done
}
JAVA_HOME_DETECTED="$(find_java || true)"
if [ -z "$JAVA_HOME_DETECTED" ]; then
  # No compatible JDK: fetch a private Temurin 21 (no root needed).
  TARGET="$HOME/.oas-jdk/current"
  info "No Gradle-compatible JDK (17-24) found — downloading Temurin 21 into $TARGET…"
  OS="linux"; [ "$(uname -s)" = "Darwin" ] && OS="mac"
  ARCH="x64"; case "$(uname -m)" in aarch64|arm64) ARCH="aarch64";; esac
  mkdir -p "$HOME/.oas-jdk/_x"
  curl -fL "https://api.adoptium.net/v3/binary/latest/21/ga/${OS}/${ARCH}/jdk/hotspot/normal/eclipse" \
    -o "$HOME/.oas-jdk/jdk.tar.gz" || die "JDK download failed. Install Temurin 21 (https://adoptium.net) and re-run."
  tar -xzf "$HOME/.oas-jdk/jdk.tar.gz" -C "$HOME/.oas-jdk/_x"
  rm -rf "$TARGET"; mv "$(find "$HOME/.oas-jdk/_x" -maxdepth 1 -mindepth 1 -type d | head -1)" "$TARGET"
  rm -rf "$HOME/.oas-jdk/_x" "$HOME/.oas-jdk/jdk.tar.gz"
  [ -d "$TARGET/Contents/Home" ] && TARGET="$TARGET/Contents/Home"
  JAVA_HOME_DETECTED="$TARGET"
fi
[ -x "$JAVA_HOME_DETECTED/bin/java" ] || die "JDK 17-24 not found. Install Temurin 21 (https://adoptium.net) and re-run."
export JAVA_HOME="$JAVA_HOME_DETECTED"
export PATH="$JAVA_HOME/bin:$PATH"
export GRADLE_OPTS="${GRADLE_OPTS:-} -Dorg.gradle.java.home=$JAVA_HOME"
JAVA_MAJOR="$(jmajor "$JAVA_HOME")"
[ "${JAVA_MAJOR:-0}" -ge 17 ] || die "JDK 17+ required, found Java ${JAVA_MAJOR} at $JAVA_HOME."


# ── 0c. Android SDK ─────────────────────────────────────────────────────────
SDK="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
if [ -z "$SDK" ]; then
  for c in "$HOME/Android/Sdk" "$HOME/Library/Android/sdk" "/usr/lib/android-sdk" "$LOCALAPPDATA/Android/Sdk"; do
    [ -d "$c" ] && { SDK="$c"; break; }
  done
fi
[ -n "$SDK" ] && [ -d "$SDK" ] || die "Android SDK not found. Install Android Studio (or cmdline-tools) and set ANDROID_HOME."
export ANDROID_HOME="$SDK" ANDROID_SDK_ROOT="$SDK"
export PATH="$SDK/platform-tools:$SDK/cmdline-tools/latest/bin:$SDK/tools/bin:$PATH"
ok "Node $(node -v) · Java ${JAVA_MAJOR} ($JAVA_HOME) · SDK $SDK"

# Required SDK packages, derived from android/variables.gradle when present.
COMPILE_SDK="$(sed -nE 's/.*compileSdkVersion *= *([0-9]+).*/\1/p' android/variables.gradle 2>/dev/null | head -1)"
COMPILE_SDK="${COMPILE_SDK:-36}"
SDKMANAGER="$(command -v sdkmanager || true)"
[ -z "$SDKMANAGER" ] && [ -x "$SDK/cmdline-tools/latest/bin/sdkmanager" ] && SDKMANAGER="$SDK/cmdline-tools/latest/bin/sdkmanager"
need_sdk_pkg=0
[ -d "$SDK/platforms/android-$COMPILE_SDK" ] || need_sdk_pkg=1
[ -d "$SDK/platform-tools" ] || need_sdk_pkg=1
ls -d "$SDK"/build-tools/* >/dev/null 2>&1 || need_sdk_pkg=1
if [ "$need_sdk_pkg" = "1" ]; then
  if [ -n "$SDKMANAGER" ]; then
    info "Installing missing Android SDK components (platform $COMPILE_SDK, build-tools, platform-tools)…"
    yes | "$SDKMANAGER" --licenses >/dev/null 2>&1 || true
    "$SDKMANAGER" "platform-tools" "platforms;android-$COMPILE_SDK" "build-tools;$COMPILE_SDK.0.0" \
      || warn "sdkmanager could not install every component — Gradle will try to resolve them."
  else
    warn "sdkmanager not found; if Gradle complains about a missing platform, install 'Android SDK Platform $COMPILE_SDK' in Android Studio → SDK Manager."
  fi
fi

# ── 1. npm dependencies (install / repair) ──────────────────────────────────
need_install=0
[ -d node_modules ] || need_install=1
for p in @capacitor/core @capacitor/cli @capacitor/android; do
  [ -d "node_modules/$p" ] || need_install=1
done
if [ "$need_install" = "1" ]; then
  info "Installing npm dependencies…"
  if [ -f package-lock.json ]; then npm ci || npm install; else npm install; fi
fi
for p in @capacitor/core @capacitor/cli @capacitor/android; do
  [ -d "node_modules/$p" ] || die "$p is still missing after npm install."
done
CAP="npx --no-install cap"

# ── 2. App icons + splash (adaptive Android icons from resources/) ──────────
if [ -f resources/icon-only.png ]; then
  info "Generating Android launcher icons and splash screens…"
  npx --yes @capacitor/assets@3 generate --android \
    --iconBackgroundColor '#0b1220' --iconBackgroundColorDark '#0b1220' \
    --splashBackgroundColor '#0b1220' --splashBackgroundColorDark '#0b1220' \
    || warn "Icon generation skipped (keeping existing/default icons)."
else
  warn "resources/icon-only.png missing — default Capacitor icon will be used."
fi

# ── 3. Web build ────────────────────────────────────────────────────────────
info "Building web assets (vite build → dist/)…"
npm run build
[ -f dist/index.html ] || die "dist/index.html missing — the web build failed."

# ── 4. Capacitor android project + sync ─────────────────────────────────────
if [ -d android ] && [ ! -f android/gradlew ]; then
  warn "android/ exists but is incomplete — recreating it."
  rm -rf android
fi
if [ ! -d android ]; then
  info "Creating the native Android project…"
  $CAP add android
fi
info "Syncing web assets + plugins into android/…"
$CAP sync android
[ -f android/app/src/main/assets/public/index.html ] || die "Capacitor sync did not copy dist/ into android/."

# Ionic's native barcode engine requires Android 8.0+ (API 26). Capacitor 8
# currently generates API 24, so enforce the plugin's requirement after the
# platform is created/synced (also covers a freshly generated android/ folder).
VARIABLES_GRADLE="android/variables.gradle"
[ -f "$VARIABLES_GRADLE" ] || die "$VARIABLES_GRADLE is missing after cap sync."
grep -Eq 'minSdkVersion[[:space:]]*=[[:space:]]*[0-9]+' "$VARIABLES_GRADLE" \
  || die "minSdkVersion was not found in $VARIABLES_GRADLE."
sed -E -i.bak 's/minSdkVersion[[:space:]]*=[[:space:]]*[0-9]+/minSdkVersion = 26/' "$VARIABLES_GRADLE"
rm -f "$VARIABLES_GRADLE.bak"
info "Android minimum SDK set to 26 (required by ionbarcode-android)."

# Gradle finds the SDK through local.properties (more reliable than env vars).
printf 'sdk.dir=%s\n' "$SDK" > android/local.properties

# ── 5. Gradle build ─────────────────────────────────────────────────────────
cd "$ROOT/android"
chmod +x gradlew 2>/dev/null || true
GRADLE_TASK="assembleDebug"; OUT_DIR="app/build/outputs/apk/debug"
if [ "$VARIANT" = "release" ]; then GRADLE_TASK="assembleRelease"; OUT_DIR="app/build/outputs/apk/release"; fi
info "Building $(echo "$VARIANT" | tr a-z A-Z) APK (gradle $GRADLE_TASK)…"
./gradlew --no-daemon --stacktrace clean "$GRADLE_TASK"
cd "$ROOT"

APK="$(find "android/$OUT_DIR" -maxdepth 1 -name '*.apk' -print -quit 2>/dev/null || true)"
[ -n "$APK" ] && [ -f "$APK" ] || die "Gradle finished but no APK was found in android/$OUT_DIR."

OUT="$ROOT/dist-apk"
mkdir -p "$OUT"
NAME="oas-production-${VARIANT}.apk"
cp "$APK" "$OUT/$NAME"
ok "APK ready: $OUT/$NAME ($(du -h "$OUT/$NAME" | cut -f1))"
echo
echo "Install on a connected device:"
echo "  adb install -r \"$OUT/$NAME\""
[ "$VARIANT" = "release" ] && echo "Note: the release APK is unsigned — sign it with apksigner before publishing."
exit 0
