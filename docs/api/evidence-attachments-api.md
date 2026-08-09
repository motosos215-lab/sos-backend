# Evidence Attachments API

Evidence Attachments API registra metadata segura de evidencias asociadas a casos de emergencia. En esta etapa no recibe ni entrega archivos reales.

## Alcance

- Coleccion MongoDB: `evidenceAttachments`.
- Metadata only.
- Un registro apunta a exactamente un target principal.
- Targets permitidos: `Incident`, `AlertDispatch`, `EmergencyResolutionReport`.
- Idempotencia por owner Rider, `clientEvidenceId`, `targetType` y `targetId`.
- Soft delete solo para Rider.

## Fuera De Alcance

No implementa carga multipart real, descarga real, almacenamiento binario, GridFS, storage externo, URLs firmadas reales, OCR, procesamiento de imagen/video/audio, ML real, tracking en vivo, mapa en tiempo real, proveedores reales, SDKs externos, pagos ni pairing API de smartwatch.

## Autor Y Propiedad

- `userId` representa al Rider duenio del caso.
- `registeredByUserId` representa al usuario autenticado que registro la evidencia.
- `registeredByRole` puede ser `Rider` o `Monitor`.
- Estos campos no se aceptan desde el body.

## Target Unico

Debe venir uno y solo uno:

- `incidentId`
- `alertDispatchId`
- `emergencyResolutionReportId`

Si vienen cero o mas de uno, devuelve `validation_error`.

## Endpoints

- `POST /api/v1/rider/evidence-attachments`
- `POST /api/v1/monitor/evidence-attachments`
- `GET /api/v1/rider/evidence-attachments`
- `GET /api/v1/rider/evidence-attachments/{id}`
- `POST /api/v1/rider/evidence-attachments/{id}/delete`
- `GET /api/v1/admin/evidence-attachments`
- `GET /api/v1/admin/evidence-attachments/{id}`

## Permisos

- Rider registra, lista, consulta y aplica soft delete solo sobre evidencia propia.
- Monitor registra solo si esta asignado al caso mediante contacto vinculado y delivery attempt relacionado.
- Admin solo consulta.
- Admin no registra ni modifica evidencia en esta etapa.

## Request

```json
{
  "incidentId": "incident-id",
  "clientEvidenceId": "mobile-evidence-001",
  "evidenceType": "Photo",
  "source": "RiderMobileApp",
  "fileName": "incident-photo-001.jpg",
  "contentType": "image/jpeg",
  "sizeBytes": 245120,
  "sha256Hash": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
  "clientStorageReference": "local://evidence/incident-photo-001.jpg",
  "storageProvider": "None",
  "description": "Registered from mobile app. File bytes are not uploaded in this API stage.",
  "capturedAtUtc": "2026-08-09T18:30:00Z",
  "metadata": {
    "camera": "rear",
    "quality": "medium"
  }
}
```

## Validaciones

- `fileName` es requerido, maximo 255, solo nombre de archivo sin ruta.
- `contentType` es requerido, maximo 150, solo metadata.
- `sizeBytes` es requerido, minimo `1`, maximo `50 MB`, solo tamanio declarado por cliente.
- `sha256Hash` es opcional; si viene debe ser hex SHA-256 de 64 caracteres.
- `capturedAtUtc` es requerido y no puede venir mas de 2 minutos en el futuro.
- `description` maximo 1000.
- `clientStorageReference` maximo 500 y no se usa para descargar nada.
- Campos como `base64`, `fileContent`, `imageBytes`, `videoBytes` y `audioBytes` se rechazan.

## Metadata

`metadata` es diccionario string/string. Se sanitizan claves case-insensitive y se descartan claves sensibles o con contenido binario. Valores mayores a 200 caracteres se truncan.

## Auditoria

Acciones best-effort:

- `EvidenceAttachmentRegistered`
- `EvidenceAttachmentDeleted`

Metadata permitida:

- `evidenceAttachmentId`
- `incidentId`
- `alertDispatchId`
- `emergencyResolutionReportId`
- `evidenceType`
- `source`
- `status`
- `sizeBytes`
- `targetType`
- `registeredByRole`

## Pendientes Futuros

- Upload real multipart.
- Storage externo.
- URLs firmadas.
- Antivirus scanning.
- Procesamiento de imagen/video/audio.
- Politicas de retencion.
- Exportacion junto con resolution report.
