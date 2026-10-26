#!/usr/bin/env bash
# Comprobaciones antes de cada commit: compila en Release. Termina en 0 solo si compila.
# No modifica archivos del repo (la salida va a bin/ y obj/, que no se suben).
set -euo pipefail
cd "$(dirname "$0")"

echo "== Compilación"
dotnet build HerramientasSICAR.csproj -c Release -nologo -v quiet

echo "== Todo correcto"
