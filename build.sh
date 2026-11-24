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
  "modid": "chiseltoolsshadowfix",
  "name": "QP's Chisel Tools Shadow Fix",
  "authors": [
    "hutli", "Neef"
  ],
  "description": "Client- and Server-side fix for the pantograph/multigraph Shadow bug in Quentin's Chiseling Tools. Can work client side only, but it will only fix shadows for blocks you edit; not everyone else on a server (not even after a chunk reload or relogging).",
  "version": "$CHISELTOOL_VERSION",
  "side": "Client",
  "requiredOnClient": true,
  "requiredOnServer": false,
  "dependencies": {
    "game": "$VS_VERSION",
    "chiseltools": "$CHISELTOOL_VERSION"
  }
}
EOF

VINTAGE_STORY=/opt/vintagestory VERSION=$CHISELTOOL_VERSION dotnet build -c Release

ls bin/Release/ChiselToolsShadowFix_*