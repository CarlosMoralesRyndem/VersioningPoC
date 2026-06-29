# VersioningPoC — Versionado automático para soluciones .NET

Prueba de concepto (PoC) de una estrategia completa de versionado basada en **Semantic Versioning (SemVer)** y **Nerdbank.GitVersioning (NBGV)**, lista para producción y reutilizable entre distintas soluciones .NET.

---

## Tabla de contenidos

- [Resumen](#resumen)
- [Estructura del proyecto](#estructura-del-proyecto)
- [Cómo funciona el versionado](#cómo-funciona-el-versionado)
- [Estrategia por rama](#estrategia-por-rama)
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
├── version.json                  # Configuración central de NBGV
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
MAJOR.MINOR.PATCH[-prerelease][+metadata]
```

Ejemplos:
- `main` → `1.0.15` (release público, sin sufijo)
- `feature/login` → `1.1.3-preview.0+g1a2b3c4`
- `release/1.1` → `1.1.0-rc.5`
- `hotfix/error-pago` → `1.0.2`

### Atributos de ensamblado generados automáticamente

| Atributo | Ejemplo |
|---|---|
| `AssemblyVersion` | `1.0.0.0` |
| `AssemblyFileVersion` | `1.0.15.0` |
| `AssemblyInformationalVersion` | `1.0.15-g1a2b3c4` |

---

## Estrategia por rama

| Rama | Incremento | Sufijo pre-release | Ejemplo |
|---|---|---|---|
| `main` | — | ninguno (release público) | `1.0.15` |
| `feature/*` | `minor` | `preview` | `1.1.0-preview.3` |
| `release/*` | — | `rc` | `1.1.0-rc.1` |
| `hotfix/*` | `patch` | ninguno | `1.0.2` |

Esta estrategia se configura en `version.json` mediante `branchesVersioningScheme`.

---

## Archivos clave

### `version.json`

Controla la versión base y las reglas por rama:

```json
{
  "version": "1.0",
  "publicReleaseRefSpec": [
    "^refs/heads/main$",
    "^refs/heads/release/.*$"
  ],
  "branchesVersioningScheme": [
    { "pattern": "^refs/heads/feature/", "versionIncrement": "minor", "tag": "preview" },
    { "pattern": "^refs/heads/release/", "versionIncrement": "none",  "tag": "rc" },
    { "pattern": "^refs/heads/hotfix/",  "versionIncrement": "patch", "tag": "" }
  ]
}
```

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
  "version": "1.0.15-g1a2b3c4",
  "environment": "Development",
  "branch": "main",
  "commit": "1a2b3c4",
  "buildDate": "2026-01-15T10:30:00Z"
}
```

| Campo | Origen |
|---|---|
| `version` | `ThisAssembly.AssemblyInformationalVersion` (NBGV) |
| `environment` | `IHostEnvironment.EnvironmentName` |
| `branch` | Variable de entorno `GITHUB_REF_NAME` / `GIT_BRANCH` |
| `commit` | Extraído del sufijo `-g{hash}` de la versión informacional |
| `buildDate` | `DateTime.UtcNow` en el momento de la request |

---

## Inicio rápido

### Requisitos previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Git (con al menos un commit en el repositorio)

### Clonar y ejecutar

```powershell
# Clonar con historial completo (necesario para NBGV)
git clone --depth=0 https://github.com/CarlosMoralesRyndem/VersioningPoC.git
cd VersioningPoC

# Restaurar dependencias
dotnet restore

# Ejecutar la API (F5 en Visual Studio hace lo mismo)
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

El archivo `.github/workflows/dotnet-ci.yml` ejecuta automáticamente:

| Paso | Descripción |
|---|---|
| Checkout | `fetch-depth: 0` para que NBGV acceda al historial completo |
| Setup .NET 10 | Instala el SDK |
| Detect version | Calcula y muestra la versión con NBGV CLI |
| Restore | `dotnet restore` |
| Build | `dotnet build --configuration Release` |
| Test | `dotnet test` con recolección de cobertura |
| Publish | `dotnet publish` de la API |
| Upload artifact | Sube el binario nombrado con la versión calculada |
| Summary | Tabla de resumen en la UI de GitHub Actions |

### Ramas que disparan el CI

- `main`
- `feature/**`
- `release/**`
- `hotfix/**`
- Pull Requests hacia cualquier rama

---

## Casos de prueba del versionado

Los siguientes escenarios ilustran el comportamiento esperado:

### Caso 1 — Rama `main`

```
Rama   : main
Versión: 1.0.0
Tipo   : Release público (sin sufijo)
```

### Caso 2 — Rama `feature/nuevo-login`

```
Rama   : feature/nuevo-login
Versión: 1.1.0-preview.1
Tipo   : Pre-release con incremento de MINOR
```

### Caso 3 — Rama `release/1.1`

```
Rama   : release/1.1
Versión: 1.1.0-rc.1
Tipo   : Release candidate
```

### Caso 4 — Rama `hotfix/error-login`

```
Rama   : hotfix/error-login
Versión: 1.0.2
Tipo   : Patch de corrección urgente
```

---

## Worker Service

El proyecto `Versioning.Worker` demuestra que **el mismo mecanismo de versionado funciona para cualquier tipo de proyecto .NET**, no solo APIs.

### Cómo expone la versión

Al iniciar, `VersionLoggerService` emite un log estructurado:

```
info: VersionLoggerService[0]
      Worker started | Version=1.0.15-g1a2b3c4 | Branch=main | Commit=1a2b3c4 | Environment=Production
```

| Campo | Origen |
|---|---|
| `Version` | `ThisAssembly.AssemblyInformationalVersion` (NBGV) |
| `Branch` | Variable de entorno `GITHUB_REF_NAME` / `GIT_BRANCH` |
| `Commit` | Extraído del sufijo `-g{hash}` de la versión informacional |
| `Environment` | `IHostEnvironment.EnvironmentName` |

### Ejecutar el Worker

```powershell
dotnet run --project src/Versioning.Worker
```

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
2. **Agregar** la referencia a NBGV en cada `.csproj` (API, Worker, o librería):
   ```xml
   <PackageReference Include="Nerdbank.GitVersioning" PrivateAssets="All" />
   ```
3. Según el tipo de proyecto, exponer la versión:
   - **API**: agregar el endpoint `GET /version`.
   - **Worker**: loguear `ThisAssembly.AssemblyInformationalVersion` en `StartAsync` o en el `BackgroundService`.
4. **Copiar** el workflow de GitHub Actions y agregar un step de publish por cada ejecutable.
5. Confirmar que el repositorio tiene **al menos un commit** antes de compilar (NBGV lo requiere para calcular la versión).

---

## Licencia

MIT — libre para uso y adaptación en proyectos propios.
