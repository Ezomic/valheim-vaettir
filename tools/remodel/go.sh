#!/bin/sh
# usage: go.sh post_variants.py
cd "$(dirname "$0")/../.."
"/c/Program Files/Blender Foundation/Blender 4.5/blender.exe" --background --python "tools/remodel/$1" 2>&1 | grep -E 'REMODEL|Error|Traceback|File "|line ' 
