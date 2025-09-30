#!/bin/bash

set -euo pipefail

# Read the version from the build script
VERSION=$(cat scripts/common.cake | grep ' SemVer = "' | sed -nre 's/.*([0-9]+\.[0-9]+\.[0-9]+).*$/\1/p')
echo "Sonar project version is '${VERSION}'"

# Determine if this is a pull request
if [ "${GITHUB_EVENT_NAME}" == "pull_request" ]; then
    export PULL_REQUEST="${GITHUB_REF##refs/pull/}"
    export PULL_REQUEST="${PULL_REQUEST%/merge}"
    export GITHUB_BRANCH="${GITHUB_HEAD_REF}"
    export GITHUB_BASE_BRANCH="${GITHUB_BASE_REF}"
else
    export PULL_REQUEST="false"
    export GITHUB_BRANCH="${GITHUB_REF_NAME}"
fi

echo "Environment variables set:"
echo "GITHUB_SHA: $GITHUB_SHA"
echo "PULL_REQUEST: $PULL_REQUEST"
echo "GITHUB_RUN_ID: $GITHUB_RUN_ID"
echo "GITHUB_BRANCH: $GITHUB_BRANCH"
echo "GITHUB_BASE_BRANCH: ${GITHUB_BASE_REF:-}"

SONAR_PARAMS=(
  -v:"${VERSION}"
  -k:"SonarSource_omnisharp-roslyn"
  -o:"sonarsource"
  -d:sonar.host.url="${SONAR_URL}"
  -d:sonar.token="${SONAR_TOKEN}"
  -d:sonar.analysis.pipeline="$GITHUB_RUN_ID"
  -d:sonar.analysis.sha1="${GITHUB_SHA}"
  -d:sonar.scanner.scanAll=false
)

if [ "$GITHUB_BRANCH" == "master" ] && [ "$PULL_REQUEST" == "false" ]; then
  echo '======= Analyze master branch'
  dotnet sonarscanner begin "${SONAR_PARAMS[@]}"

elif [ "$PULL_REQUEST" != "false" ]; then
  echo '======= Analyze pull request'
  dotnet sonarscanner begin "${SONAR_PARAMS[@]}" \
    -d:sonar.pullrequest.key="${PULL_REQUEST}" \
    -d:sonar.pullrequest.branch="${GITHUB_BRANCH}" \
    -d:sonar.pullrequest.base="${GITHUB_BASE_BRANCH}"

else
    echo '======= Analyze branch'
    dotnet sonarscanner begin "${SONAR_PARAMS[@]}" -d:sonar.branch.name="${GITHUB_BRANCH}"
fi

# Build (ideally should also run .NET unit tests but they are failing)
powershell -File build.ps1 -target Build -configuration Release

# Finish SonarQube scan
dotnet sonarscanner end \
  -d:sonar.token=${SONAR_TOKEN}