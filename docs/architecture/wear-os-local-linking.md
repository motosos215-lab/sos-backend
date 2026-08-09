# Wear OS Local Linking Decision

## Decision Funcional Final

La vinculacion del smartwatch Wear OS sera unicamente local entre la app movil Android y Wear OS mediante Wear OS Data Layer.

La web y MotoSOS.API no administran QR, codigos, pairing, registro remoto del reloj, nodeId, Bluetooth, Wear OS Data Layer ni estado Connected/Disconnected del reloj. El telefono Android es el unico gateway hacia la API.

El telefono recibe senales del smartwatch, las combina con las senales del movil, procesa localmente y envia a la API solo datos resumidos usando la sesion autenticada del Rider.

## Motivo De La Decision

Esta decision reduce alcance del backend, evita almacenar identificadores internos del reloj y mantiene la privacidad del usuario. Tambien simplifica operacion offline: si el reloj se desconecta, la app movil sigue operando con sensores del telefono; cuando el reloj se reconecta, sus datos se reincorporan localmente sin requerir reconciliacion de pairing en la API.

La API conserva el rol de punto central para datos operativos remotos, pero no se convierte en administrador de dispositivos Wear OS.

## Responsabilidad De Android Movil

- Iniciar y finalizar viajes usando la sesion del Rider.
- Administrar localmente el pairing con Wear OS.
- Administrar localmente Wear OS Data Layer.
- Recibir senales del smartwatch.
- Combinar senales del smartwatch con sensores del telefono.
- Procesar localmente deteccion, scoring, filtros, ventanas de sensor y eventos menores.
- Continuar operando con sensores del telefono si el reloj se desconecta.
- Reincorporar localmente datos del reloj cuando se reconecta.
- Enviar a MotoSOS.API batches resumidos, eventos menores, incidentes, solicitudes de alerta y ubicacion compartida.
- Usar siempre la identidad remota del Rider autenticado y el `mobileDeviceId` remoto del telefono.

## Responsabilidad De Wear OS

- Capturar senales locales disponibles en el smartwatch.
- Enviar esas senales al telefono mediante Wear OS Data Layer.
- Mantener la interaccion local con Android movil.
- No comunicarse directamente con MotoSOS.API.
- No autenticarse directamente contra MotoSOS.API.

## Responsabilidad De La API

- Soportar inicio de viaje y generacion de `tripId` remoto mediante Trips API.
- Recibir batches resumidos mediante Offline Ingestion API.
- Recibir eventos menores como items de ingestion offline.
- Recibir incidentes mediante Incidents API.
- Recibir solicitudes de alerta mediante Alert Dispatch API.
- Finalizar viajes mediante Trips API.
- Confirmar de forma idempotente la recepcion de datos operativos.
- Recibir ubicacion compartida desde el telefono como gateway mediante Location Sharing API.
- Derivar identidad desde JWT Bearer del Rider, no desde campos enviados por el cliente.
- Mantener `MobileApp` como dispositivo remoto principal para operacion.

## Que No Debe Implementar La API

- No debe administrar pairing de smartwatch.
- No debe exponer endpoints de pairing de smartwatch.
- No debe crear `SmartwatchPairingSession`.
- No debe crear codigos de emparejamiento de smartwatch.
- No debe administrar QR para smartwatch.
- No debe guardar `nodeId`.
- No debe administrar Bluetooth.
- No debe administrar Wear OS Data Layer.
- No debe registrar estado Connected/Disconnected del reloj.
- No debe registrar estado online/offline propio del smartwatch como fuente de verdad remota.
- No debe requerir que Wear OS se autentique directamente contra la API.
- No debe recibir telemetria cruda continua del reloj.

## Origen De Datos En Batches, Eventos E Incidentes

La API recibe datos resumidos ya procesados por el telefono. El origen debe representarse como resultado del gateway movil, no como identidad remota independiente del smartwatch.

Lineamientos:

- `mobileDeviceId` identifica el telefono vinculado en la API.
- `tripId` identifica el viaje remoto activo o sincronizado.
- Los payloads pueden indicar origen funcional o resumen de evidencia, por ejemplo `MobileDetection`, `ManualSos`, `watchBatteryPercent`, `sensorWindowSeconds` o versiones de reglas.
- La presencia de senales del smartwatch se representa como evidencia resumida dentro del batch/evento/incidente.
- La API no debe almacenar `nodeId`, identificadores locales de Wear OS ni estado de pairing.
- La API no debe asumir que un incidente con evidencia de smartwatch implica reloj conectado en tiempo real.

## Relacion Con Trips API

Trips API sigue siendo el punto remoto para iniciar y finalizar viajes. El telefono inicia el viaje y recibe el `tripId`; ese `tripId` se usa despues para batches, eventos, incidentes, alertas y ubicacion compartida.

El smartwatch no inicia viajes directamente en la API. Si participa en la experiencia de usuario, esa interaccion se resuelve localmente con el telefono.

## Relacion Con Offline Ingestion API

Offline Ingestion API recibe batches resumidos desde el telefono. Los items pueden contener eventos menores o incidentes locales procesados por Android movil con o sin senales de smartwatch.

La API devuelve ACK durable e idempotente por item, pero no sincroniza Wear OS Data Layer ni administra colas propias del smartwatch.

## Relacion Con Incidents API

Incidents API registra incidentes remotos creados desde la sesion del Rider. La evidencia puede incluir campos resumidos de sensores del telefono y del smartwatch, pero el incidente pertenece al Rider, al viaje y al telefono gateway.

La API no valida pairing de smartwatch para aceptar un incidente. La validacion remota se centra en ownership del Rider, `tripId`, idempotencia y reglas de negocio del incidente.

## Relacion Con Alert Dispatch API

Alert Dispatch API prepara solicitudes de alerta asociadas a incidentes existentes. La solicitud se origina desde el telefono usando la sesion del Rider.

La API no necesita conocer si el disparo original vino de una accion local en Wear OS, de sensores del telefono o de una combinacion local. Ese detalle puede viajar como evidencia resumida o metadata funcional, no como estado remoto de pairing.

## Relacion Con Location Sharing API

Location Sharing API recibe la ultima ubicacion conocida desde el telefono como gateway. Aunque el telefono haya usado senales del smartwatch para mejorar contexto local, la API solo persiste la ubicacion compartida por incidente abierto.

La desconexion del reloj no debe bloquear ubicacion compartida: el telefono continua enviando ubicacion con sus propios sensores si corresponde.

## Propuesta Anterior Reemplazada

La propuesta anterior donde MotoSOS.API administraba smartwatch pairing, codigos de seis digitos, QR, registro remoto del reloj y estado Connected/Disconnected queda reemplazada por esta decision.

Esa propuesta puede permanecer como contexto historico en documentacion existente, pero no debe guiar nuevas implementaciones.

## Pendientes Futuros

- Definir contrato movil interno para resumir evidencia de smartwatch sin exponer identificadores locales.
- Definir convenciones de payload para indicar si una ventana de sensor incluyo datos de telefono, smartwatch o ambos.
- Evaluar UX local de reconexion Wear OS sin dependencia del backend.
- Evaluar pruebas end-to-end Android + Wear OS fuera del alcance de MotoSOS.API.
- Revisar si campos historicos como `smartwatchDeviceId` deben mantenerse, deprecarse o eliminarse en una migracion futura.
- Mantener documentada cualquier compatibilidad temporal como legado, sin crear nuevas APIs de pairing de smartwatch.
