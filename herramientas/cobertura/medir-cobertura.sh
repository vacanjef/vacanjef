#!/usr/bin/env bash
# TT-005: mide la cobertura de pruebas en local y genera un reporte HTML.
# Uso (desde la raíz del repo): bash herramientas/cobertura/medir-cobertura.sh
# Requiere una vez: dotnet tool install --global dotnet-reportgenerator-globaltool
set -e

if [ ! -f VacanJef.slnx ]; then
  echo "ERROR: ejecuta este script desde la raíz del repositorio."
  exit 1
fi

rm -rf coverage
dotnet test --settings coverlet.runsettings --collect:"XPlat Code Coverage" --results-directory coverage

if command -v reportgenerator > /dev/null 2>&1; then
  reportgenerator -reports:"coverage/**/coverage.cobertura.xml" -targetdir:"coverage/reporte" -reporttypes:"HtmlInline;TextSummary"
  echo ""
  cat coverage/reporte/Summary.txt
  echo ""
  echo "Reporte HTML: coverage/reporte/index.html"
else
  echo ""
  echo "Cobertura medida en coverage/ (archivo coverage.cobertura.xml)."
  echo "Para ver el reporte HTML instala una vez:"
  echo "  dotnet tool install --global dotnet-reportgenerator-globaltool"
fi
