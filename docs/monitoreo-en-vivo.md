# Monitoreo en vivo (HU-007)

Cómo funciona la cámara en VacanJef y cómo prepararla para una demostración.
Historia: HU-007 · RF-007, RNF-004, RNF-006 · RN-004 · Depende de HU-014 (cámara asignada) y TT-001 (prueba de concepto y ADR-005).

## 1. Cómo funciona

```
Webcam ──FFmpeg──► MediaMTX (RTSP :8554, WebRTC :8889)
                         ▲  2. VacanJef reenvía la oferta (WHEP)
                         │
Navegador ──1. /Monitoreo/Ver/{reserva}──► VacanJef (autoriza, audita, token de 5 min)
    │      ──3. POST /Monitoreo/Senal?token=… (oferta SDP)──► VacanJef ──► MediaMTX
    ◄══════════ 4. video WebRTC directo desde MediaMTX ══════════
```

1. El dueño abre **Monitoreo** y elige la estadía. VacanJef revisa:
   - que la reserva sea suya;
   - que esté **Activa**;
   - que tenga una cámara activa asignada (HU-014).

   Cada acceso queda en **Auditoría** con la acción `AccesoCamara`, permitido o denegado.
2. VacanJef emite un **token temporal de 5 minutos** (Data Protection), válido solo para ese usuario y esa reserva (RN-004).
3. El navegador envía su oferta WebRTC a VacanJef con el token. VacanJef vuelve a validar y la reenvía al endpoint WHEP de MediaMTX (`{MediaMTX}/{ruta}/whep`).
4. El video viaja directo de MediaMTX al navegador.

La dirección de la cámara (`UrlStream`) **nunca llega al navegador**: VacanJef solo usa su ruta (`rtsp://localhost:8554/camara1` → `camara1`).

**Si la cámara no transmite**, MediaMTX no responde a la oferta o no llega el video en 10 s. La página muestra entonces el mensaje de indisponibilidad y la **última fotografía** de la mascota (CA-HU-007-02).

**Quién puede ver:**
- El dueño ve solo sus mascotas con estadía activa.
- El cuidador y el administrador ven todas las estadías activas, sin foto de respaldo, porque la foto solo se entrega al dueño (HU-004).

## 2. Configuración

| Clave | Por defecto | Para qué |
| --- | --- | --- |
| `Monitoreo__MediaMtxWebRtc` | `http://localhost:8889` | Dirección del servidor WebRTC de MediaMTX, vista **desde el servidor de VacanJef** |

## 3. Preparar la demostración (PC con la webcam)

VacanJef y MediaMTX corren **en el mismo PC**, el que tiene la cámara.

1. **Ventana 1, MediaMTX:**

   ```bash
   cd herramientas/bin/mediamtx && ./mediamtx.exe
   ```

2. **Ventana 2, la cámara:**

   ```bash
   bash herramientas/streaming/iniciar-camara-prueba.sh webcam "USB Camera"
   ```

   Debe decir `is publishing to path 'camara1'`. Sin webcam, `bash herramientas/streaming/iniciar-camara-prueba.sh` publica un patrón de prueba.

3. **Ventana 3, VacanJef:**

   ```bash
   dotnet run --project src/VacanJef.Web --launch-profile https
   ```

4. Como administrador, en **Cámaras**, registrar "Patio 1" con `rtsp://localhost:8554/camara1`.
5. Como dueño, reservar desde hoy. Como cuidador o administrador, en **Estadías**, confirmar el ingreso con la cámara "Patio 1".
6. Como dueño, abrir **Monitoreo → Ver en vivo**. Debe aparecer "En vivo. La transmisión inició en X s".

**Desde un celular en la misma red Wi-Fi:** abrir `https://<IP-del-PC>:<puerto>` en lugar de `localhost` y aceptar el aviso del certificado de desarrollo. El video llega por WebRTC desde el mismo PC.

## 4. Seguridad recomendada en MediaMTX

Por defecto, MediaMTX deja leer las cámaras a cualquiera que llegue a su puerto. Como VacanJef hace la negociación desde el mismo PC, conviene permitir la lectura **solo desde ese equipo**. En `herramientas/bin/mediamtx/mediamtx.yml`, en `authInternalUsers`, cambiar `ips: []` del usuario `any` que tiene las acciones `publish`, `read` y `playback` por:

```yaml
  ips: ['127.0.0.1', '::1']
```

Después hay que reiniciar MediaMTX. Revisar el formato en el `mediamtx.yml` de la versión instalada (probado con v1.21.4).

## 5. Limitaciones conocidas

- **En Azure no hay cámara:** el App Service no alcanza el MediaMTX del PC. La página muestra el respaldo (mensaje y foto). El video en vivo se demuestra con VacanJef corriendo en el PC de la cámara.
- La "última fotografía" es la foto de la mascota (HU-004). Tomar fotos periódicas desde la cámara queda para V1.1.
- La lista de estadías no se actualiza sola; hay que recargar.

## 6. Pruebas

`tests/VacanJef.Tests/Web/MonitoreoTests.cs` cubre:
- autorización del dueño, de otro dueño, de una reserva no activa, sin cámara y del personal;
- auditoría permitida y denegada;
- token temporal (válido, de otro usuario y alterado);
- extracción de la ruta de MediaMTX;
- reenvío de la oferta a MediaMTX con un servidor simulado.
