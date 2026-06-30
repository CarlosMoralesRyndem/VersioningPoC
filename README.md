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
- [Validación en servidor](#validación-en-servidor)
- [Worker Service](#worker-service)
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
| Servidor de validación | `http://poctest.runasp.net/version` (runasp.net, WebDeploy) |

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
│   └── Versioning.Core/          # Librería compartida: BuildInfo (Version, Branch, Commit)
│       ├── BuildInfo.cs
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
MAJOR.MINOR.PATCH+{hash}           ← public release  (main, hotfix/*, <ID>-main)
MAJOR.MINOR.PATCH+{hash}           ← non-public      (feature/*, debt/*, warranty/*, otras) — AssemblyInformationalVersion
MAJOR.MINOR.PATCH-g{hash}          ← non-public      (feature/*, debt/*, warranty/*, otras) — NuGetPackageVersion
```

> **Alternativa documentada (no estándar NBGV):** El CI puede sobrescribir la versión en PRs hacia ambientes (`dev`, `qa`, `uat`) para incluir el número de PR como trazabilidad:
> ```
> MAJOR.MINOR.PATCH-pr{N}            ← PR a ambiente   (dev, qa, uat)
> MAJOR.FORK.PATCH-pr{N}             ← PR a ambiente   (con fork configurado)
> ```
> Esta lógica es custom y está habilitada en el workflow, pero no aplica al deploy (que ocurre post-merge).

Ejemplos:
- `main` → `NuGetPackageVersion: 1.0.31` / `AssemblyInformationalVersion: 1.0.31+39d7fece7d`
- `feature/login` → `NuGetPackageVersion: 1.0.11-gd0e49296a5` / `AssemblyInformationalVersion: 1.0.11+d0e49296a5`
- `R14-main` → `NuGetPackageVersion: 1.0.10` (public release del fork)
- PR #42 → `dev` (sin fork) → `1.0.10-pr42` *(alternativa custom)*
- PR #42 → `dev` (fork R14) → `1.14.10-pr42` *(alternativa custom)*

> `AssemblyInformationalVersion` usa siempre `+{hash}` como separador (tanto public como non-public). El sufijo `-g{hash}` aparece únicamente en `NuGetPackageVersion` para ramas non-public.

### Atributos de ensamblado generados automáticamente

| Escenario | `NuGetPackageVersion` | `AssemblyInformationalVersion` |
|---|---|---|
| Public release (`main`) | `1.0.31` | `1.0.31+39d7fece7d` |
| Non-public (`feature/*`) | `1.0.11-gd0e49296a5` | `1.0.11+d0e49296a5` |
| PR a ambiente (sin fork) | `1.0.10-pr42` | `1.0.10-pr42` |
| PR a ambiente (fork R14) | `1.14.10-pr42` | `1.14.10-pr42` |

---

## Estrategia por rama

NBGV distingue dos tipos de rama: **public release** y **non-public release**, controlado por `publicReleaseRefSpec`.

| Trigger | Rama | Tipo | Versión generada | Deploy |
|---|---|---|---|---|
| push | `main` | Public release | `1.0.31` | No |
| push | `hotfix/*` | Public release | `1.0.31` | No |
| push | `<ID>-main` (ej. `R14-main`) | Public release (fork) | `1.0.31` | No |
| push | `feature/*`, `debt/*`, `warranty/*`, otras | Non-public | `1.0.31-g1a2b3c4` | No |
| push (post-merge) | `dev` | Ambiente CI | `1.0.31+{hash}` | **Sí** |
| push (post-merge) | `qa` | Ambiente CI | `1.0.31+{hash}` | **Sí** |
| push (post-merge) | `uat` | Ambiente CI | `1.0.31+{hash}` | **Sí** |

- **Public release**: versión limpia sin sufijo, lista para producción.
- **Non-public**: versión con `-g{hash}` del commit, identifica builds de desarrollo.
- **Ambientes**: el deploy se dispara en el push **post-merge** (no al abrir el PR). Cada merge a `dev`/`qa`/`uat` sobreescribe el servidor.

> Para labels semánticos por rama (`preview`, `rc`, incremento de MINOR automático), la herramienta indicada es **GitVersion**, no NBGV. NBGV prioriza simplicidad: el patch crece con la altura de commits y el hash identifica el origen.

---

## Versionado por PR y Forks

### PR como identificador de versión en ambientes

El CI calcula una versión con el número de PR para los builds de pull request hacia `dev`, `qa` o `uat`. Esta versión aparece en los artefactos del PR pero **no es la que se deploya** — el deploy ocurre cuando el PR se mergea:

```
Sin fork:   1.0.{height}-pr{PR_NUMBER}     →  1.0.15-pr42
Con fork:   1.{fork}.{height}-pr{PR_NUMBER} →  1.14.15-pr42
```

Esto permite:
- Identificar en los artefactos de CI exactamente qué PR fue buildeado.
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

### `src/Versioning.Core/BuildInfo.cs`

Librería compartida que resuelve en runtime los tres valores de trazabilidad. Tanto la API como el Worker la consumen:

| Propiedad | Lógica de resolución |
|---|---|
| `Version` | Lee `ci-branch.txt` si existe; si no, usa `AssemblyInformationalVersionAttribute` vía reflection |
| `Branch` | Lee `ci-branch.txt` → `GITHUB_REF_NAME` → `GIT_BRANCH` → `git rev-parse --abbrev-ref HEAD` → `"unknown"` |
| `Commit` | Extrae el hash tras `+` o `-g` de la versión; `"unknown"` si no hay separador |

El archivo `ci-branch.txt` es escrito por el CI en el output de publish (`publish/api/` y `publish/worker/`), por lo que el artifact deployado siempre conoce su rama de origen.

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
  "version": "1.0.32+d6f63b5a58",
  "branch": "dev",
  "commit": "d6f63b5a58",
  "buildDate": "2026-06-30T22:17:21Z"
}
```

> En builds de PR el campo `commit` devuelve `"unknown"` porque la versión `1.0.15-pr42` no incluye hash; el número de PR ya identifica el origen de forma trazable.

| Campo | Origen |
|---|---|
| `version` | `ThisAssembly.AssemblyInformationalVersion` (NBGV) |
| `branch` | `ci-branch.txt` (escrito por CI) → `GITHUB_REF_NAME` → `GIT_BRANCH` → `git rev-parse --abbrev-ref HEAD` (local) → `"unknown"` |
| `commit` | Extraído tras el separador `+` (o `-g` en formato legacy); `"unknown"` en PR builds |
| `buildDate` | `DateTime.UtcNow` en el momento de la request |

El campo `branch` se resuelve correctamente en producción gracias a `ci-branch.txt`: el CI escribe el nombre de la rama (`github.head_ref || github.ref_name`) en el output de publish, y `BuildInfo` lo lee al arrancar la aplicación.

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
| Build | `dotnet build --configuration Release` |
| Test | `dotnet test` con recolección de cobertura |
| Publish API | `dotnet publish` de la API |
| Publish Worker | `dotnet publish` del Worker |
| Write CI version file | Escribe `ci-version.txt` en el artifact (solo en PR builds a ambientes) |
| Write CI branch file | Escribe `ci-branch.txt` en el artifact (siempre; permite resolver `branch` en producción) |
| Upload artifacts | Artefactos nombrados `versioning-api-{version}` y `versioning-worker-{version}` |
| Summary | Tabla Branch / SemVer / NuGet en la UI de GitHub Actions |

### Lógica de versión en el CI

```
┌─ ¿Es pull_request a dev, qa o uat?
│
├── SÍ → Lee fork de version.json
│         ├── fork = null  →  {SimpleVersion}-pr{N}         (ej. 1.0.15-pr42)
│         └── fork = "R14" →  {Major}.{14}.{Height}-pr{N}  (ej. 1.14.15-pr42)
│
└── NO  → NBGV calcula la versión normalmente
           ├── public release  →  1.0.31
           └── non-public      →  1.0.31-g1a2b3c4
```

### Lógica de deploy

El job `deploy` solo se ejecuta en push a `dev`, `qa` o `uat` — es decir, **después de que un PR es mergeado**, no al abrirlo:

```
push a dev / qa / uat
    └── build + test + publish
    └── write ci-branch.txt  (con nombre de la rama)
    └── upload artifact
    └── deploy → WebDeploy → poctest.runasp.net
```

Push a `main`, `feature/*`, `hotfix/*`, etc. solo ejecuta build y tests; no deploya.

### Triggers

| Trigger | Ramas | Deploy |
|---|---|---|
| push | `main`, `feature/**`, `hotfix/**`, `warranty/**`, `debt/**`, `sonar`, `R*-main` | No |
| push | `dev`, `qa`, `uat` | **Sí** |
| pull_request | todas las ramas | No |

---

## Casos de prueba del versionado

| Trigger | Rama / Destino PR | fork en version.json | Versión generada |
|---|---|---|---|
| push | `main` | cualquiera | `1.0.31` |
| push | `hotfix/bug` | cualquiera | `1.0.31` |
| push | `R14-main` | cualquiera | `1.0.31` |
| push | `feature/algo` | cualquiera | `1.0.31-g1a2b3c4` |
| push | `debt/algo`, `warranty/algo` | cualquiera | `1.0.31-g1a2b3c4` |
| push (post-merge) | `dev` | cualquiera | `1.0.31+{hash}` ✓ deploya |
| push (post-merge) | `qa` | cualquiera | `1.0.31+{hash}` ✓ deploya |
| push (post-merge) | `uat` | cualquiera | `1.0.32+{hash}` ✓ deploya |
| pull_request | → `dev` | `null` | `1.0.15-pr42` *(artefacto CI, no deploya)* |
| pull_request | → `qa` | `null` | `1.0.15-pr7` *(artefacto CI, no deploya)* |
| pull_request | → `uat` | `"R14"` | `1.14.15-pr3` *(artefacto CI, no deploya)* |
| pull_request | → `main` | cualquiera | `1.0.31-g1a2b3c4` (NBGV, sin cambio) |

---

## Validación en servidor

Validado en `http://poctest.runasp.net/version` el 2026-06-30 con el flujo completo `dev` → `qa` → `uat` → `main`.

### Flujo de ambientes

| Merge a | `branch` | `version` | `commit` | Deploy |
|---|---|---|---|---|
| `dev` | `"dev"` | `1.0.34+beca95a44f` | `beca95a44f` | ✓ |
| `qa` | `"qa"` | `1.0.31+39d7fece7d` | `39d7fece7d` | ✓ |
| `uat` | `"uat"` | `1.0.32+3ed33fa37d` | `3ed33fa37d` | ✓ |

### Resultado en `main`

| Rama | `NuGetPackageVersion` | Deploy |
|---|---|---|
| `main` | `1.0.35` | No (solo build + test) |

### Respuesta final del endpoint

```json
{
  "version": "1.0.34+beca95a44f",
  "branch": "dev",
  "commit": "beca95a44f",
  "buildDate": "2026-06-30T23:21:50Z"
}
```

### Comportamiento confirmado

- Cada push post-merge a `dev`/`qa`/`uat` dispara build + deploy automáticamente.
- El servidor se sobreescribe con el artifact de la rama mergeada.
- El campo `branch` refleja correctamente la rama de ambiente (resuelto vía `ci-branch.txt`).
- El campo `environment` fue eliminado — no aportaba valor en un servidor con una sola instancia.
- `main` genera versión limpia (`1.0.35`, sin hash en NuGet) y no deploya al servidor.

---

## Worker Service

El proyecto `Versioning.Worker` demuestra que **el mismo mecanismo de versionado funciona para cualquier tipo de proyecto .NET**, no solo APIs.

### Cómo expone la versión

Al iniciar, `VersionLoggerService` emite un log estructurado:

```
info: VersionLoggerService[0]
      Worker started | Version=1.0.32+d6f63b5a58 | Branch=dev | Commit=d6f63b5a58
```

| Campo | Origen |
|---|---|
| `Version` | `ThisAssembly.AssemblyInformationalVersion` (NBGV) |
| `Branch` | `ci-branch.txt` → `GITHUB_REF_NAME` → `GIT_BRANCH` |
| `Commit` | Extraído del sufijo de versión; `"unknown"` en PR builds |

### Diferencia clave con la API

| | API | Worker |
|---|---|---|
| SDK | `Microsoft.NET.Sdk.Web` | `Microsoft.NET.Sdk.Worker` |
| Expone versión vía | Endpoint `GET /version` | Log al arranque |
| `ThisAssembly` | Si | Si |
| `version.json` | Si (compartido) | Si (compartido) |

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
