# VersioningPoC — Versionado automático para soluciones .NET

Prueba de concepto (PoC) de una estrategia completa de versionado basada en **Semantic Versioning (SemVer)** y **Nerdbank.GitVersioning (NBGV)**, lista para producción y reutilizable entre distintas soluciones .NET.

---

## Tabla de contenidos

- [Resumen](#resumen)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Cómo funciona el versionado](#cómo-funciona-el-versionado)
- [Estrategia por rama](#estrategia-por-rama)
- [Versionado por PR y Forks](#versionado-por-pr-y-forks)
- [Archivos clave](#archivos-clave)
- [Endpoint /version](#endpoint-version)
- [Inicio rápido](#inicio-rápido)
- [Pipeline CI/CD](#pipeline-cicd)
- [Casos de prueba del versionado](#casos-de-prueba-del-versionado)
- [Reutilización en otras soluciones](#reutilización-en-otras-soluciones)

---

## Resumen

| Característica | Detalle |
|---|---|
| Framework | .NET 10 |
| Estrategia | Semantic Versioning (SemVer 2.0) |
| Herramienta | Nerdbank.GitVersioning 3.8.x |
| Versionado automático | Basado en historial Git |
| Tipos de proyecto | API Web + Worker Service |
| Endpoint REST | `GET /version` (API) |
| Worker | Log de versión al arranque |
| CI/CD | GitHub Actions |

---

## Estructura del proyecto

```
VersioningPoC/
├── VersioningPoC.sln
│
├── version.json                  # Configuración central de NBGV + fork ID
├── Directory.Build.props         # Propiedades MSBuild compartidas
├── Directory.Packages.props      # Gestión centralizada de paquetes NuGet
├── .gitignore
│
├── src/
│   ├── Versioning.Api/           # API Web que expone GET /version
│   │   ├── Program.cs
│   │   └── Versioning.Api.csproj
│   │
│   ├── Versioning.Worker/        # Worker Service que loguea la versión al iniciar
│   │   ├── Program.cs
│   │   ├── VersionLoggerService.cs
│   │   └── Versioning.Worker.csproj
│   │
│   └── Versioning.Core/          # Librería de dominio (reutilizable)
│       └── Versioning.Core.csproj
│
├── tests/
│   └── Versioning.Tests/         # Tests de integración del endpoint
│       ├── VersionEndpointTests.cs
│       └── Versioning.Tests.csproj
│
└── .github/
    └── workflows/
        └── dotnet-ci.yml         # Pipeline CI/CD completo (API + Worker)
```

---

## Cómo funciona el versionado

NBGV calcula la versión automáticamente combinando:

1. **Versión base** definida en `version.json` (ej. `1.0`)
2. **Altura de commits** (número de commits desde la versión base)
3. **Hash del commit** actual
4. **Estrategia de rama** (sufijo de pre-release según el tipo de rama)

### Formato generado

```
MAJOR.MINOR.PATCH+{hash}           ← public release  (main, release/*, hotfix/*)
MAJOR.MINOR.PATCH+{hash}           ← non-public      (feature/* y otras) — AssemblyInformationalVersion
MAJOR.MINOR.PATCH-g{hash}          ← non-public      (feature/* y otras) — NuGetPackageVersion
MAJOR.MINOR.PATCH-pr{N}            ← PR a ambiente   (develop, qa, uat)
MAJOR.FORK.PATCH-pr{N}             ← PR a ambiente   (con fork configurado)
```

Ejemplos:
- `main` → `NuGetPackageVersion: 1.0.10` / `AssemblyInformationalVersion: 1.0.10+5d54999cf4`
- `feature/login` → `NuGetPackageVersion: 1.0.11-gd0e49296a5` / `AssemblyInformationalVersion: 1.0.11+d0e49296a5`
- `release/1.1` → `NuGetPackageVersion: 1.0.10`
- PR #42 → `develop` (sin fork) → `1.0.10-pr42`
- PR #42 → `develop` (fork R14) → `1.14.10-pr42`

> `AssemblyInformationalVersion` usa siempre `+{hash}` como separador (tanto public como non-public). El sufijo `-g{hash}` aparece únicamente en `NuGetPackageVersion` para ramas non-public.

### Atributos de ensamblado generados automáticamente

| Escenario | `NuGetPackageVersion` | `AssemblyInformationalVersion` |
|---|---|---|
| Public release (`main`) | `1.0.10` | `1.0.10+5d54999cf4` |
| Non-public (`feature/*`) | `1.0.11-gd0e49296a5` | `1.0.11+d0e49296a5` |
| PR a ambiente (sin fork) | `1.0.10-pr42` | `1.0.10-pr42` |
| PR a ambiente (fork R14) | `1.14.10-pr42` | `1.14.10-pr42` |

---

## Estrategia por rama

NBGV distingue dos tipos de rama: **public release** y **non-public release**, controlado por `publicReleaseRefSpec`. El CI extiende esto con versiones de PR para las ramas de ambiente.

| Trigger | Rama / Destino | Tipo | Ejemplo de versión |
|---|---|---|---|
| push | `main` | Public release | `1.0.15` |
| push | `release/*` | Public release | `1.0.15` |
| push | `hotfix/*` | Public release | `1.0.15` |
| push | `feature/*` u otras | Non-public | `1.0.15-g1a2b3c4` |
| pull_request | → `develop` | Ambiente CI | `1.0.15-pr42` |
| pull_request | → `qa` | Ambiente CI | `1.0.15-pr42` |
| pull_request | → `uat` | Ambiente CI | `1.0.15-pr42` |

- **Public release**: versión limpia sin sufijo, lista para producción.
- **Non-public**: versión con `-g{hash}` del commit, identifica builds de desarrollo.
- **PR a ambiente**: versión con `-pr{N}` donde N es el número de PR de GitHub; si hay fork configurado, el MINOR refleja el número de fork.

> Para labels semánticos por rama (`preview`, `rc`, incremento de MINOR automático), la herramienta indicada es **GitVersion**, no NBGV. NBGV prioriza simplicidad: el patch crece con la altura de commits y el hash identifica el origen.

---

## Versionado por PR y Forks

### PR como identificador de versión en ambientes

Cuando se abre un PR hacia `develop`, `qa` o `uat`, el CI calcula una versión que usa el número de PR en lugar del hash del commit:

```
Sin fork:   1.0.{height}-pr{PR_NUMBER}     →  1.0.15-pr42
Con fork:   1.{fork}.{height}-pr{PR_NUMBER} →  1.14.15-pr42
```

Esto permite:
- Trazar exactamente qué PR está desplegado en cada ambiente.
- Nombrar los artefactos de forma legible: `versioning-api-1.14.15-pr42`.
- Ordenar versiones cronológicamente (el height sigue siendo el contador de commits).

### Forks de repositorio

Algunos proyectos mantienen forks numerados del repositorio (ej. `R14`, `R7`) para clientes o líneas de producto independientes. El campo `fork` en `version.json` permite reflejar ese número en el MINOR de la versión sin modificar el workflow de CI.

**Cómo funciona:**

| `version.json` fork | Número extraído | Versión en PR #42 |
|---|---|---|
| `null` | — | `1.0.15-pr42` |
| `"R14"` | `14` | `1.14.15-pr42` |
| `"R7"` | `7` | `1.7.15-pr42` |

El CI extrae el número con `tr -d '[:alpha:]'`, por lo que el formato del fork puede ser `R14`, `Fork14`, `v14`, etc.

**Para configurar un fork**, editar `version.json`:

```json
{
  "version": "1.0",
  "publicReleaseRefSpec": [
    "^refs/heads/main$",
    "^refs/heads/release/.*$",
    "^refs/heads/hotfix/.*$"
  ],
  "fork": "R14"
}
```

---

## Archivos clave

### `version.json`

Controla la versión base, las ramas public release y el identificador de fork:

```json
{
  "$schema": "https://raw.githubusercontent.com/dotnet/Nerdbank.GitVersioning/main/src/NerdBank.GitVersioning/version.schema.json",
  "version": "1.0",
  "publicReleaseRefSpec": [
    "^refs/heads/main$",
    "^refs/heads/release/.*$",
    "^refs/heads/hotfix/.*$"
  ],
  "fork": null
}
```

| Campo | Descripción |
|---|---|
| `version` | MAJOR.MINOR base; NBGV auto-incrementa el PATCH |
| `publicReleaseRefSpec` | Expresiones regulares que identifican ramas de release público |
| `fork` | Identificador de fork (ej. `"R14"`); `null` en el repositorio principal |

### `Directory.Build.props`

Propiedades MSBuild compartidas por todos los proyectos de la solución:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

### `Directory.Packages.props`

Versiones de paquetes NuGet centralizadas (un único lugar para actualizarlas):

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="Nerdbank.GitVersioning" Version="3.8.118" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageVersion Include="xunit" Version="2.9.2" />
    ...
  </ItemGroup>
</Project>
```

---

## Endpoint `/version`

### Request

```
GET /version
```

### Response

```json
{
  "version": "1.0.15-pr42",
  "environment": "Production",
  "branch": "develop",
  "commit": "unknown",
  "buildDate": "2026-06-29T10:30:00Z"
}
```

> En builds de PR el campo `commit` devuelve `"unknown"` porque la versión `1.0.15-pr42` no incluye hash; el número de PR ya identifica el origen de forma trazable.

| Campo | Origen |
|---|---|
| `version` | `ThisAssembly.AssemblyInformationalVersion` (NBGV o CI override) |
| `environment` | `IHostEnvironment.EnvironmentName` |
| `branch` | Variable de entorno `GITHUB_REF_NAME` / `GIT_BRANCH` |
| `commit` | Extraído tras el separador `+` (o `-g` en formato legacy); `"unknown"` en PR builds |
| `buildDate` | `DateTime.UtcNow` en el momento de la request |

---

## Inicio rápido

### Requisitos previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git (con al menos un commit en el repositorio)

### Clonar y ejecutar

```powershell
# Clonar con historial completo (necesario para NBGV)
git clone https://github.com/CarlosMoralesRyndem/VersioningPoC.git
cd VersioningPoC

# Restaurar dependencias
dotnet restore

# Ejecutar la API
dotnet run --project src/Versioning.Api

# Probar el endpoint
curl http://localhost:5000/version
```

### Ejecutar los tests

```powershell
dotnet test
```

### Verificar la versión detectada (requiere NBGV CLI)

```powershell
# Instalar la herramienta una vez
dotnet tool install --global nbgv

# Ver la versión calculada para la rama actual
nbgv get-version
```

---

## Pipeline CI/CD

El archivo `.github/workflows/dotnet-ci.yml` ejecuta automáticamente en cada push y pull request.

### Pasos del pipeline

| Paso | Descripción |
|---|---|
| Checkout | `fetch-depth: 0` para que NBGV acceda al historial completo |
| Setup .NET 10 | Instala el SDK |
| Install NBGV CLI | `dotnet tool install --global nbgv` |
| Detect version | Calcula la versión según el trigger (ver lógica abajo) |
| Restore | `dotnet restore` |
| Build | `dotnet build --configuration Release` con override de versión si aplica |
| Test | `dotnet test` con recolección de cobertura |
| Publish API | `dotnet publish` de la API |
| Publish Worker | `dotnet publish` del Worker |
| Upload artifacts | Artefactos nombrados `versioning-api-{version}` y `versioning-worker-{version}` |
| Summary | Tabla Branch / SemVer / NuGet en la UI de GitHub Actions |

### Lógica de versión en el CI

```
┌─ ¿Es pull_request a develop, qa o uat?
│
├── SÍ → Lee fork de version.json
│         ├── fork = null  →  {SimpleVersion}-pr{N}         (ej. 1.0.15-pr42)
│         └── fork = "R14" →  {Major}.{14}.{Height}-pr{N}  (ej. 1.14.15-pr42)
│
└── NO  → NBGV calcula la versión normalmente
           ├── public release  →  1.0.15
           └── non-public      →  1.0.15-g1a2b3c4
```

### Triggers

| Trigger | Ramas |
|---|---|
| push | `main`, `feature/**`, `release/**`, `hotfix/**` |
| pull_request | todas las ramas (lógica de versión PR aplica solo a `develop`, `qa`, `uat`) |

---

## Casos de prueba del versionado

| Trigger | Rama / Destino PR | fork en version.json | Versión generada |
|---|---|---|---|
| push | `main` | cualquiera | `1.0.15` |
| push | `release/1.1` | cualquiera | `1.0.15` |
| push | `hotfix/bug` | cualquiera | `1.0.15` |
| push | `feature/algo` | cualquiera | `1.0.15-g1a2b3c4` |
| pull_request | → `develop` | `null` | `1.0.15-pr42` |
| pull_request | → `qa` | `null` | `1.0.15-pr7` |
| pull_request | → `uat` | `"R14"` | `1.14.15-pr3` |
| pull_request | → `main` | cualquiera | `1.0.15-g1a2b3c4` (NBGV, sin cambio) |

---

## Worker Service

El proyecto `Versioning.Worker` demuestra que **el mismo mecanismo de versionado funciona para cualquier tipo de proyecto .NET**, no solo APIs.

### Cómo expone la versión

Al iniciar, `VersionLoggerService` emite un log estructurado:

```
info: VersionLoggerService[0]
      Worker started | Version=1.14.15-pr42 | Branch=develop | Commit=unknown | Environment=Production
```

| Campo | Origen |
|---|---|
| `Version` | `ThisAssembly.AssemblyInformationalVersion` (NBGV o CI override) |
| `Branch` | Variable de entorno `GITHUB_REF_NAME` / `GIT_BRANCH` |
| `Commit` | Extraído del sufijo de versión; `"unknown"` en PR builds |
| `Environment` | `IHostEnvironment.EnvironmentName` |

### Diferencia clave con la API

| | API | Worker |
|---|---|---|
| SDK | `Microsoft.NET.Sdk.Web` | `Microsoft.NET.Sdk.Worker` |
| Expone versión vía | Endpoint `GET /version` | Log al arranque |
| `ThisAssembly` | ✅ | ✅ |
| `version.json` | ✅ (compartido) | ✅ (compartido) |

---

## Reutilización en otras soluciones

Para adoptar esta estrategia en un proyecto existente:

1. **Copiar** `version.json`, `Directory.Build.props` y `Directory.Packages.props` a la raíz de la solución.
2. Ajustar el campo `"fork"` en `version.json`:
   - `null` para el repositorio principal.
   - `"R14"` (o el identificador que corresponda) para forks numerados.
3. **Agregar** la referencia a NBGV en cada `.csproj`:
   ```xml
   <PackageReference Include="Nerdbank.GitVersioning" PrivateAssets="All" />
   ```
4. Según el tipo de proyecto, exponer la versión:
   - **API**: agregar el endpoint `GET /version`.
   - **Worker**: loguear `ThisAssembly.AssemblyInformationalVersion` en `StartAsync` o en el `BackgroundService`.
5. **Copiar** el workflow de GitHub Actions y agregar un step de publish por cada ejecutable.
6. Confirmar que el repositorio tiene **al menos un commit** antes de compilar (NBGV lo requiere para calcular la versión).

---

## Licencia

MIT — libre para uso y adaptación en proyectos propios.
