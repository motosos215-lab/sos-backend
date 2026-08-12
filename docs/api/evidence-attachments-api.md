# Evidence Attachments API

Evidence Attachments API registra metadata segura de evidencias y ahora soporta carga/descarga binaria real para evidencias asociadas a incidentes.

## Alcance

- Coleccion MongoDB: `evidenceAttachments`.
- Endpoints metadata-only existentes siguen disponibles y compatibles.
- Upload binario usa `multipart/form-data`; no acepta Base64 ni URLs externas.
- Los bytes se guardan en storage externo compatible S3; no se guardan en MongoDB ni en disco local permanente.
- Descarga segura pasa por la API y devuelve stream binario; no se devuelven URLs firmadas.

## Storage

Proveedor implementado: `DigitalOceanSpaces` mediante API S3 compatible.

Variables:

```text
EvidenceStorage__Enabled=true
EvidenceStorage__Provider=DigitalOceanSpaces
EvidenceStorage__Bucket=
EvidenceStorage__Region=
EvidenceStorage__ServiceUrl=https://REGION.digitaloceanspaces.com
EvidenceStorage__AccessKey=
EvidenceStorage__SecretKey=
EvidenceStorage__BasePath=evidence
EvidenceStorage__UsePathStyle=false
EvidenceStorage__MaxFileSizeBytes=10485760
```

`AccessKey` y `SecretKey` deben ser secret variables. La API no expone bucket, object key, rutas internas, credenciales ni URLs firmadas en respuestas publicas.

## Endpoints

Metadata-only:

- `POST /api/v1/rider/evidence-attachments`
- `POST /api/v1/monitor/evidence-attachments`
- `GET /api/v1/rider/evidence-attachments`
- `GET /api/v1/rider/evidence-attachments/{id}`
- `POST /api/v1/rider/evidence-attachments/{id}/delete`
- `GET /api/v1/admin/evidence-attachments`
- `GET /api/v1/admin/evidence-attachments/{id}`

Binarios:

- `POST /api/v1/rider/evidence-attachments/upload`
- `GET /api/v1/rider/evidence-attachments/{id}/download`
- `POST /api/v1/monitor/evidence-attachments/upload`
- `GET /api/v1/monitor/evidence-attachments/{id}/download`
- `GET /api/v1/admin/evidence-attachments/{id}/download`

## Upload Multipart

Content type: `multipart/form-data`.

Campos:

- `file`: requerido.
- `incidentId`: requerido.
- `description`: opcional.
- `evidenceType`, `type` o `category`: opcional; default `Photo`.
- `clientEvidenceId`: opcional para idempotencia movil.

Ejemplo:

```text
POST /api/v1/rider/evidence-attachments/upload
Content-Type: multipart/form-data

file=@photo.jpg
incidentId=incident-id
description=Foto del incidente
evidenceType=Photo
clientEvidenceId=98a9dd9a-0db6-45af-9f37-1abdadce1111
```

Respuesta exitosa usa wrapper JSON:

```json
{
  "success": true,
  "data": {
    "evidenceAttachment": {
      "id": "...",
      "incidentId": "incident-id",
      "fileName": "photo.jpg",
      "contentType": "image/jpeg",
      "sizeBytes": 12345,
      "sha256Hash": "...",
      "storageProvider": "DigitalOceanSpaces"
    },
    "isDuplicate": false
  }
}
```

## Idempotencia Multipart

- `clientEvidenceId` es opcional.
- Si viene, se valida como string seguro y se guarda en metadata.
- La llave logica es `userId + incidentId + clientEvidenceId`.
- Si llega el mismo `clientEvidenceId` con el mismo archivo, responde success con la evidencia existente e `isDuplicate=true`.
- Si llega el mismo `clientEvidenceId` con archivo diferente, responde `409` con `code = evidence_upload_conflict`.
- Si no viene `clientEvidenceId`, cada upload crea una evidencia nueva.

## Validaciones De Archivo

- Archivo requerido.
- Tamaño mayor a `0`.
- Tamaño menor o igual a `EvidenceStorage__MaxFileSizeBytes`.
- Content types permitidos: `image/jpeg`, `image/png`, `image/webp`, `application/pdf`, `text/plain`.
- Extensiones permitidas: `.jpg`, `.jpeg`, `.png`, `.webp`, `.pdf`, `.txt`.
- Se rechazan ejecutables, scripts, HTML, SVG, comprimidos, `application/octet-stream`, archivos sin extension segura y nombres con path traversal.
- Se calcula SHA256 server-side.

## Descarga

La descarga valida permisos antes de abrir storage y devuelve archivo binario normal.

Headers relevantes:

- `Content-Type`: content type validado original.
- `Content-Disposition`: attachment con filename sanitizado.
- `Cache-Control: no-store, no-cache`.

Errores usan wrapper JSON. Archivo faltante en storage responde error controlado `evidence_file_not_available`.

## Permisos

- Rider sube y descarga solo evidencias de sus propios incidentes.
- Monitor sube y descarga solo si tiene relacion con el incidente mediante contacto vinculado y notification delivery attempt relacionado.
- Admin consulta y descarga evidencias.
- No se exponen evidencias de otros incidentes.

## Metadata

Mongo guarda metadata, no bytes:

- `StorageProvider`
- `OriginalFileName`
- `StoredFileName`
- `ContentType`
- `SizeBytes`
- `Sha256Hash`
- `UploadedAtUtc`
- `UploadedByUserId`
- `UploadedByRole`
- `DownloadCount`
- `LastDownloadedAtUtc`

`Bucket` y `StorageObjectKey` pueden existir internamente para operar storage, pero no se exponen en respuestas publicas.

## Auditoria

Acciones best-effort:

- `EvidenceAttachmentRegistered`
- `EvidenceAttachmentDeleted`
- `EvidenceAttachmentUploaded`
- `EvidenceAttachmentUploadFailed`
- `EvidenceAttachmentDownloaded`
- `EvidenceAttachmentDownloadDenied`

No se auditan bytes, body, AccessKey, SecretKey, object key completo, bucket, rutas internas ni URLs firmadas.

## Pendientes Futuros

- URLs firmadas para cliente.
- CDN.
- Antivirus scanning.
- OCR.
- Procesamiento de imagen/video/audio.
- Miniaturas.
- Politicas de retencion fisica por storage.
