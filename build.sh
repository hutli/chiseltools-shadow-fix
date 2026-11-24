#!/bin/sh

set -e 

CHISELTOOL_VERSION=1.17.9
VS_VERSION=1.22.7

mkdir -p ./lib
tmp=$(mktemp -d)
trap "rm -rf \"$tmp\"" EXIT

curl -sSL https://mods.vintagestory.at/download/127670/ChiselTools$CHISELTOOL_VERSION.zip -o "$tmp/chiseltools.zip"
unzip -p "$tmp/chiseltools.zip" > lib/chiseltools.dll

cat > ./modinfo.json<< EOF
{
  "type": "code",
  "modid": "chiseltools-lighting-fix",
  "name": "QP's Chisel Tools Lighting Fix",
  "authors": [
    "hutli", "Neef"
  ],
  "description": "Client-side fix for the paste lighting bug in Quentin's Chiseling Tools'",
  "version": "$CHISELTOOL_VERSION",
  "side": "Client",
  "requiredOnClient": false,
  "requiredOnServer": false,
  "dependencies": {
    "game": "$VS_VERSION",
    "chiseltools": "$CHISELTOOL_VERSION"
  }
}
EOF

VINTAGE_STORY=/opt/vintagestory VERSION=$CHISELTOOL_VERSION dotnet build -c Release

ls bin/Release/ChiselToolsLightingFix_*