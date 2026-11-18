# TT-001 — Prueba de concepto de streaming de cámaras

| Campo | Valor |
| 16/11/2026 | 16/11/2026
| Responsable | Carlos Alfredo Santiz |
| Fecha | 16/11/2026|
| Riesgo asociado | RSK-01 (integración del streaming de cámaras) |
| Requisitos | RF-007, RNF-004, RNF-006, RN-004 |
| Criterio | La transmisión inicia en menos de 5 segundos |

## Montaje
Cámara simulada con FFmpeg (patrón de prueba y webcam) → RTSP → MediaMTX → navegador por WebRTC y por HLS.
Equipo: __ (procesador, RAM) · Navegador: __ · Red: __

## Resultados
| Prueba | WebRTC | HLS |
| --- | --- | --- |
| Inicio, intento 1 (s) | 1.2 | 6.8 |
| Inicio, intento 2 (s) | 1.4| 6.9 |
| Inicio, intento 3 (s) | 1.6| 7.0 |
| Retraso aproximado con webcam (s) | __ | __ |
| Funciona en Chrome / Edge | __ | __ |
| Funciona en celular (misma red) | __ | __ |

## Conclusión
__ (qué protocolo cumple el criterio de menos de 5 s y por qué)

## Evidencias
Capturas en la carpeta del Hito 3: __
