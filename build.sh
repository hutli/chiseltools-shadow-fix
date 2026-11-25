#!/bin/sh

set -e

CHISELTOOL_VERSION=1.17.9
MOD_VERSION=$CHISELTOOL_VERSION
VS_VERSION=1.22.7

mkdir -p ./lib
tmp=$(mktemp -d)
trap "rm -rf \"$tmp\"" EXIT

curl -sSL https://mods.vintagestory.at/download/127670/ChiselTools$CHISELTOOL_VERSION.zip -o "$tmp/chiseltools.zip"
unzip -p "$tmp/chiseltools.zip" >lib/chiseltools.dll

cat >./modinfo.json <<EOF
{
  "type": "code",
  "modid": "chiseltoolsshadowfix",
  "name": "QP's Chisel Tools Shadow Fix",
  "authors": [
    "hutli", "Neef"
  ],
  "description": "Server-side fix for the pantograph/multigraph Shadow (absorption) bug in Quentin's Chiseling Tools.",
  "version": "$MOD_VERSION",
  "side": "Server",
  "requiredOnClient": false,
  "requiredOnServer": true,
  "dependencies": {
    "game": "$VS_VERSION",
    "chiseltools": "$CHISELTOOL_VERSION"
  }
}
EOF

VINTAGE_STORY=/opt/vintagestory VERSION=$MOD_VERSION dotnet build -c Release

ls bin/Release/ChiselToolsShadowFix_*
