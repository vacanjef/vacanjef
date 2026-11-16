#!/usr/bin/env bash
# TT-001: publica una cámara de prueba en MediaMTX (rtsp://localhost:8554/camara1)
# Uso:
#   bash herramientas/streaming/iniciar-camara-prueba.sh              -> patrón de prueba
#   bash herramientas/streaming/iniciar-camara-prueba.sh webcam "Nombre de la webcam"
set -e

DESTINO="rtsp://localhost:8554/${CAMARA:-camara1}"
# H.264 sin B-frames y con keyframe cada segundo: arranque rápido y compatible con WebRTC
CODIFICACION="-c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p -g 30 -bf 0"

if [ "$1" = "webcam" ]; then
  ffmpeg -f dshow -i video="$2" $CODIFICACION -f rtsp "$DESTINO"
else
  ffmpeg -re -f lavfi -i testsrc=size=1280x720:rate=30 $CODIFICACION -f rtsp "$DESTINO"
fi
