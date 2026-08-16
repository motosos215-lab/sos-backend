# Evidence Binary Storage

El almacenamiento binario de evidencias usa una abstraccion `IEvidenceFileStorageProvider` y una implementacion `DigitalOceanSpacesEvidenceStorageProvider` compatible con S3.

## Flujo

- La API recibe `multipart/form-data`.
- Valida permisos sobre el incidente.
- Valida nombre, extension, content type y tamaño.
- Calcula SHA256 server-side.
- Genera `objectKey` seguro con `BasePath`, ambiente, `incidentId`, `evidenceAttachmentId` y filename sanitizado.
- Sube el stream a DigitalOcean Spaces.
- Guarda metadata en MongoDB.
- Descarga leyendo desde storage y devolviendo stream por la API.

## Seguridad

- No Base64.
- No URLs externas.
- No bytes en MongoDB.
- No archivos permanentes en disco local.
- No bucket/object key/credenciales en responses.
- No URLs firmadas en este cambio.
- Errores controlados con wrapper JSON.

## DigitalOcean Spaces

Configurar:

```text
EvidenceStorage__Enabled=true
EvidenceStorage__Provider=DigitalOceanSpaces
EvidenceStorage__Bucket=
EvidenceStorage__Region=
EvidenceStorage__ServiceUrl=https://REGION.digitaloceanspaces.com
EvidenceStorage__AccessKey=
EvidenceStorage__SecretKey=
EvidenceStorage__BasePath=evidence/production
EvidenceStorage__UsePathStyle=false
EvidenceStorage__MaxFileSizeBytes=10485760
```

`AccessKey` y `SecretKey` deben ser secrets de DigitalOcean App Platform.
