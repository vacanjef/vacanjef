# ADR-005 — Protocolo de streaming de cámaras

- Estado: Aceptado
- Fecha: __/11/2026
- Responsable: Carlos Alfredo Santiz
- Relacionado: TT-001, RF-007, RSK-01, OBS-004

## Contexto
Las cámaras IP entregan video por RTSP, que los navegadores no reproducen. RF-007 exige que la transmisión inicie en menos de 5 segundos, y RN-004 que solo el dueño con reserva activa vea la cámara.

## Decisión
- Las cámaras IP publican en RTSP hacia un servidor MediaMTX en el servidor de VacanJef.
- El navegador recibe el video por **WebRTC** (baja latencia). **HLS** queda como respaldo para navegadores o redes donde WebRTC falle.
- Las direcciones y credenciales de las cámaras (DAT-CAMARA.url_stream) nunca llegan al navegador.
- El acceso se valida en la aplicación (RN-004) y MediaMTX solo entrega el video con un token temporal emitido por VacanJef (se implementa en HU-007).
- Si la cámara no transmite, se muestra la última fotografía (flujo alterno de CU-003).

## Alternativas consideradas
| Alternativa | Motivo para descartarla |
| --- | --- |
| RTSP directo al navegador | Los navegadores no lo soportan |
| Solo HLS | Inicio y retraso mayores (ver informe TT-001) |
| Servicio de video en la nube | Costo mensual, contrario a RST-002 (presupuesto moderado) |

## Consecuencias
- Hay que desplegar MediaMTX junto a la aplicación (se agrega al docker-compose).
- Las pruebas de carga de RNF-004 deben incluir streams concurrentes.

## Evidencia
docs/pruebas/TT-001-prueba-concepto-streaming.md
