#!/usr/bin/env bash
#
# Build script for macOS and Linux.
#
#   ./build.sh                          restore, build (Release) and run the tests
#   ./build.sh --publish                also package a self-contained zip
#   ./build.sh --publish -r linux-x64   cross-target another runtime
#   ./build.sh --no-test -c Debug
#
# Options:
#   -c, --configuration <name>   Debug or Release (default: Release)
#   -r, --runtime <rid>          e.g. osx-arm64, linux-x64, win-x64 (default: this host)
#   -v, --version <version>      overrides the version in Directory.Build.props
#       --publish                publish and zip into artifacts/
#       --no-test                skip the test run
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIGURATION="Release"
RUNTIME=""
VERSION=""
PUBLISH=0
RUN_TESTS=1

while [[ $# -gt 0 ]]; do
  case "$1" in
    -c|--configuration) CONFIGURATION="$2"; shift 2 ;;
    -r|--runtime)       RUNTIME="$2";       shift 2 ;;
    -v|--version)       VERSION="$2";       shift 2 ;;
    --publish)          PUBLISH=1;          shift ;;
    --no-test)          RUN_TESTS=0;        shift ;;
    -h|--help)          sed -n '3,18p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) echo "Unknown option: $1" >&2; exit 1 ;;
  esac
done

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: the .NET SDK is not on PATH — install .NET 10 from https://dotnet.microsoft.com/download" >&2
  exit 1
fi

# Work out a sensible default runtime identifier from the host.
if [[ -z "$RUNTIME" ]]; then
  case "$(uname -s)" in
    Darwin) [[ "$(uname -m)" == "arm64" ]] && RUNTIME="osx-arm64" || RUNTIME="osx-x64" ;;
    Linux)
      case "$(uname -m)" in
        aarch64|arm64) RUNTIME="linux-arm64" ;;
        *)             RUNTIME="linux-x64" ;;
      esac ;;
    *) echo "error: unsupported host; pass --runtime explicitly" >&2; exit 1 ;;
  esac
fi

if [[ -z "$VERSION" ]]; then
  VERSION="$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' "$ROOT/Directory.Build.props" | head -1)"
  VERSION="${VERSION:-1.0.0}"
fi

echo "==> Configuration : $CONFIGURATION"
echo "==> Runtime       : $RUNTIME"
echo "==> Version       : $VERSION"

dotnet restore "$ROOT/Markdowner.sln"
dotnet build "$ROOT/Markdowner.sln" -c "$CONFIGURATION" --no-restore

if [[ "$RUN_TESTS" -eq 1 ]]; then
  echo "==> Running tests"
  dotnet test "$ROOT/Markdowner.sln" -c "$CONFIGURATION" --no-build
fi

[[ "$PUBLISH" -eq 1 ]] || exit 0

STAGING="$ROOT/artifacts/staging/$RUNTIME"
PACKAGE="$ROOT/artifacts/Markdowner-$VERSION-$RUNTIME.zip"

echo "==> Publishing"
rm -rf "$STAGING"
mkdir -p "$STAGING"

dotnet publish "$ROOT/src/Markdowner/Markdowner.csproj" \
  -c "$CONFIGURATION" \
  -r "$RUNTIME" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none \
  -p:Version="$VERSION" \
  -o "$STAGING"

# Debug symbols are not part of a release drop.
find "$STAGING" -name '*.pdb' -delete
cp "$ROOT/README.md" "$STAGING/" 2>/dev/null || true

echo "==> Packaging $PACKAGE"
rm -f "$PACKAGE"

if command -v zip >/dev/null 2>&1; then
  # Zip from inside the staging directory so paths in the archive stay relative.
  (cd "$STAGING" && zip -qr "$PACKAGE" .)
elif command -v python3 >/dev/null 2>&1; then
  python3 -c "import shutil,sys; shutil.make_archive(sys.argv[1], 'zip', sys.argv[2])" \
    "${PACKAGE%.zip}" "$STAGING"
else
  echo "error: neither 'zip' nor 'python3' is available to build the archive" >&2
  exit 1
fi

# The staging tree has served its purpose; leave only the archive behind.
rm -rf "$ROOT/artifacts/staging"

echo "==> Done: $PACKAGE ($(du -h "$PACKAGE" | cut -f1 | tr -d ' '))"
