# Estructura de MotoSOS.API

Este documento describe la estructura base de carpetas de MotoSOS.API. El objetivo es permitir crecimiento modular sin introducir dependencias prematuras ni logica falsa.

## Raiz del proyecto API

- `Common`: componentes transversales sin dependencia de infraestructura.
- `Configuration`: opciones y extension methods para registrar servicios.
- `Middleware`: middleware y pipeline HTTP de la API.
- `Security`: contratos y servicios de seguridad, autenticacion, autorizacion, hashing y tokens.
- `Infrastructure`: adaptadores tecnicos como persistencia MongoDB, logging, auditoria y servicios externos.
- `Modules`: modulos funcionales de negocio.
- `Observability`: health checks, metricas y tracing.
- `OpenApi`: documentacion y ejemplos para OpenAPI.
- `DevSecOps`: convenciones internas relacionadas con seguridad, pruebas y pipelines.

## Common

- `Abstractions`: contratos transversales como reloj del sistema, identificadores o servicios base.
- `Constants`: constantes compartidas de la API.
- `Errors`: modelos de errores normalizados.
- `Exceptions`: excepciones controladas de aplicacion.
- `Extensions`: extension methods generales.
- `Results`: modelos de resultado para respuestas internas.

## Configuration

- `Options`: clases tipadas para configuracion, sin secretos hardcoded.
- `DependencyInjection`: extension methods para registrar servicios por responsabilidad.

Los extension methods base son:

- `AddApiConfiguration`
- `AddApplicationServices`
- `AddInfrastructureServices`
- `AddSecurityServices`
- `UseApiMiddleware`

## Middleware

- `ExceptionHandling`: manejo centralizado de errores.
- `RequestLogging`: logging de solicitudes sin informacion sensible.
- `SecurityHeaders`: cabeceras HTTP de seguridad.

La logica completa se agregara cuando existan requerimientos concretos y pruebas asociadas.

## Infrastructure

- `Persistence/MongoDb`: integracion futura con MongoDB.
- `Collections`: nombres y convenciones de colecciones.
- `Indexes`: definicion de indices MongoDB.
- `Repositories`: implementaciones de repositorios MongoDB.
- `DateTime`: servicios de fecha y hora.
- `Logging`: adaptadores de logging.
- `Auditing`: auditoria tecnica y funcional.
- `ExternalServices`: integraciones externas.

La API no debe usar Entity Framework Core ni conectarse a SQL Server, PostgreSQL o SQLite.

## Modules

Cada modulo funcional sigue la misma estructura:

- `Contracts`: requests, responses y DTOs publicos del modulo.
- `Domain`: entidades, value objects y reglas del dominio.
- `Application`: casos de uso, validaciones y orquestacion.
- `Infrastructure`: persistencia o adaptadores propios del modulo.
- `Endpoints`: endpoints HTTP o mapeos de rutas.

Modulos iniciales:

- `Auth`
- `Users`
- `Profiles`
- `Vehicles`
- `EmergencyContacts`
- `Devices`
- `Trips`
- `Incidents`
- `Plans`
- `Notifications`
- `Analytics`

## Crecimiento esperado

- Agregar logica solo dentro del modulo correspondiente.
- Evitar dependencias directas entre modulos cuando sea posible.
- Promover contratos compartidos a `Common` solo si realmente son reutilizables.
- Crear pruebas al agregar comportamiento.
- Mantener `Program.cs` delgado y delegar configuracion en extension methods.
