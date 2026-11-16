# Streaming de cámaras (TT-001)

Simula una cámara IP para probar la transmisión sin hardware.

## Requisitos
- FFmpeg: `winget install Gyan.FFmpeg`
- MediaMTX: descargar de https://github.com/bluenviron/mediamtx/releases y descomprimir en `herramientas/bin/mediamtx/` (no se sube al repositorio).

## Uso
1. Terminal 1, iniciar el servidor de video:
   `cd herramientas/bin/mediamtx && ./mediamtx.exe`
2. Terminal 2, publicar la cámara de prueba:
   `bash herramientas/streaming/iniciar-camara-prueba.sh`
   Con webcam (ver nombres con `ffmpeg -list_devices true -f dshow -i dummy`):
   `bash herramientas/streaming/iniciar-camara-prueba.sh webcam "Nombre de la webcam"`
3. Abrir en el navegador:
   - WebRTC: http://localhost:8889/camara1
   - HLS: http://localhost:8888/camara1

## Puertos de MediaMTX
| Protocolo | Puerto | Uso |
| --- | --- | --- |
| RTSP | 8554 | Entrada de la cámara IP |
| HLS | 8888 | Salida al navegador (compatible, más latencia) |
| WebRTC | 8889 | Salida al navegador (baja latencia) |
