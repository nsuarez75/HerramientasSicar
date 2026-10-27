#!/usr/bin/env bash
# Comprobaciones antes de cada commit: compila en Release (avisos = errores) y pasa los tests.
# Termina en 0 solo si todo está bien. No modifica archivos del repo (la salida va a bin/ y obj/, que no se suben).
set -euo pipefail
cd "$(dirname "$0")"

echo "== Compilación"
dotnet build HerramientasSICAR.sln -c Release -nologo -v quiet -warnaserror

echo "== Tests"
dotnet test tests/HerramientasSICAR.Tests/HerramientasSICAR.Tests.csproj -c Release --no-build -nologo -v quiet

echo "== Todo correcto"
