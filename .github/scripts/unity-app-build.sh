#!/usr/bin/env bash
# Builds one app with Unity inside a GameCI image (added Nov 2026). Used by apps.yml.
#   unity-app-build.sh <image> <target> <output dir>
# Activates the Personal license, builds, and always returns the license seat afterwards.
set -u
image="$1"; target="$2"; out="$3"
# Free the disk for the next image, whatever happens.
trap 'docker rmi -f "$image" >/dev/null 2>&1 || true' EXIT
mkdir -p unity-logs "$out"
docker run --rm \
  -e UNITY_SERIAL -e UNITY_EMAIL -e UNITY_PASSWORD -e UNITY_LICENSE \
  -e PROJECT_PATH -e HOST_UID="$(id -u)" -e HOST_GID="$(id -g)" \
  -e FROGS_APP_TARGET="$target" -e FROGS_APP_PATH="/work/$out" \
  -e FROGS_VERSION -e FROGS_BUILD_NUMBER -e TARGET_NAME="$target" \
  -v "$GITHUB_WORKSPACE":/work -w /work \
  "$image" \
  bash -c '
    mkdir -p /BlankProject/Assets /root/.local/share/unity3d/Unity
    printf "%s" "$UNITY_LICENSE" > /root/.local/share/unity3d/Unity/Unity_lic.ulf
    act=1
    if [ -n "${UNITY_SERIAL:-}" ]; then
      unity-editor -batchmode -nographics -quit -serial "$UNITY_SERIAL" \
        -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
        -projectPath /BlankProject -logFile /work/unity-logs/activate-$TARGET_NAME.log
      act=$?
    fi
    if [ $act -ne 0 ]; then
      unity-editor -batchmode -nographics -quit \
        -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
        -projectPath /BlankProject -logFile /work/unity-logs/activate-$TARGET_NAME.log
    fi
    echo "== Building $TARGET_NAME"
    timeout --signal=KILL 3600 unity-editor -batchmode -nographics -accept-apiupdate \
      -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
      -projectPath "/work/$PROJECT_PATH" \
      -executeMethod FrogsNLogs.EditorTools.AppBuilds.Build \
      -logFile /work/unity-logs/build-$TARGET_NAME.log
    code=$?
    echo "build exit code: $code"
    echo "$code" > /work/unity-logs/build-$TARGET_NAME.code
    unity-editor -batchmode -nographics -quit -returnlicense \
      -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" \
      -logFile /work/unity-logs/return-$TARGET_NAME.log
    rm -f /root/.local/share/unity3d/Unity/Unity_lic.ulf
    chown -R "$HOST_UID:$HOST_GID" /work
  '
code=$(cat "unity-logs/build-$target.code" 2>/dev/null || echo 98)
if [ "$code" != "0" ]; then
  msg=$(grep -E "error CS[0-9]+|\[FrogsApp\]|Error building|BuildFailedException|Exception:|Failed to|Build Finished|licens|Gradle|FAILURE|il2cpp" \
        "unity-logs/build-$target.log" 2>/dev/null | sort -u | head -120 | sed 's/%/%25/g' | awk '{printf "%s%%0A", $0}')
  echo "::error title=$target build failed (exit $code)::${msg:0:60000}"
  exit 1
fi
grep "\[FrogsApp\]" "unity-logs/build-$target.log" || true
