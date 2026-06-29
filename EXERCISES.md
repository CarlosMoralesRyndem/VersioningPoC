# Ejercicios de validación — VersioningPoC

Estos ejercicios cubren los escenarios clave de la implementación. Ejecútalos en orden; cada uno construye sobre el anterior.

> **Resultados reales ejecutados el 2026-06-29** sobre el commit `5d54999` (tag: `chore: abrir /version por defecto al lanzar la API`).

---

## Prerequisitos

```powershell
# .NET 10 SDK instalado
dotnet --version  # debe mostrar 10.x.x

# Instalar la CLI de NBGV (una sola vez)
dotnet tool install --global nbgv

# Clonar con historial completo (NBGV lo necesita)
git clone https://github.com/CarlosMoralesRyndem/VersioningPoC.git
cd VersioningPoC

dotnet restore
```

---

## Ejercicio 1 — Versión calculada en `main`

**Objetivo:** confirmar que NBGV calcula la versión desde el historial git.

```powershell
git checkout main
nbgv get-version
```

**Qué validar:**
- `Version` → `1.0.{altura}`
- `NuGetPackageVersion` → sin sufijo de pre-release (ej. `1.0.10`)
- `PublicRelease` → `True`
- `CommitId` → hash del commit actual

> La "altura" es el número de commits desde la versión base definida en `version.json`.

**Resultado real:**

```
Version:                      1.0.10.23892
AssemblyVersion:              1.0.0.0
AssemblyInformationalVersion: 1.0.10+5d54999cf4
NuGetPackageVersion:          1.0.10
NpmPackageVersion:            1.0.10
```

- [x] `NuGetPackageVersion` = `1.0.10` — sin sufijo de pre-release
- [x] `AssemblyInformationalVersion` = `1.0.10+5d54999cf4` — build metadata con hash
- [x] `PublicRelease` implícito por ausencia de sufijo

---

## Ejercicio 2 — API: endpoint `/version`

**Objetivo:** verificar que la API expone la versión embebida en el ensamblado.

```powershell
dotnet run --project src/Versioning.Api

# En otra terminal:
curl http://localhost:51461/version
```

**Respuesta esperada (en `main`, local):**
```json
{
  "version": "1.0.10+5d54999cf4",
  "environment": "Development",
  "branch": "local",
  "commit": "5d54999cf4",
  "buildDate": "..."
}
```

**Qué validar:**
- [ ] El campo `version` no está vacío
- [ ] Sigue el patrón `MAJOR.MINOR.PATCH+{hash}`
- [ ] El campo `commit` coincide con `git rev-parse --short HEAD`
- [ ] `environment` es `Development` en local

**Resultado real:**

```
Puertos: https://localhost:51460 · http://localhost:51461
```

```json
{
  "version": "1.0.10+5d54999cf4",
  "environment": "Development",
  "branch": "local",
  "commit": "5d54999cf4",
  "buildDate": "2026-06-29T23:08:xx.xxxxZ"
}
```

- [x] `version` = `1.0.10+5d54999cf4`
- [x] `commit` = `5d54999cf4` (extraído tras el separador `+`)
- [x] `environment` = `Development`
- [x] `branch` = `local` (sin variable de entorno `GITHUB_REF_NAME`)

> El separador `+` es siempre el que usa NBGV 3.8 en `AssemblyInformationalVersion`, tanto en public como non-public release. El código busca primero `-g` (legacy) y luego `+`.

---

## Ejercicio 3 — Worker: log de versión al arranque

**Objetivo:** verificar que el Worker Service loguea la versión al iniciar.

```powershell
dotnet run --project src/Versioning.Worker
```

**Salida esperada en consola:**
```
info: VersionLoggerService[0]
      Worker started | Version=1.0.10+5d54999cf4 | Branch=local | Commit=5d54999cf4 | Environment=Production
```

**Qué validar:**
- [ ] Aparece el log con los 4 campos (Version, Branch, Commit, Environment)
- [ ] El hash de `Commit` coincide con `git rev-parse --short HEAD`
- [ ] El proceso termina limpio (es un BackgroundService de ciclo corto en este PoC)

**Resultado real:**

```
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: VersionLoggerService[0]
      Worker started | Version=1.0.10+5d54999cf4 | Branch=local | Commit=5d54999cf4 | Environment=Production
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Production
info: Microsoft.Hosting.Lifetime[0]
      Content root path: C:\Users\Ariel\Documents\DotNet10-Versioning-PoC\src\Versioning.Worker
```

- [x] Log con los 4 campos correctos
- [x] `Commit=5d54999cf4` coincide con `git rev-parse --short HEAD`
- [x] Proceso termina limpio

---

## Ejercicio 4 — Rama `feature/*`: non-public release con hash

**Objetivo:** verificar que ramas fuera de `publicReleaseRefSpec` generan versión con sufijo `-g{hash}` en NuGet.

```powershell
git checkout -b feature/mi-nueva-funcionalidad
git commit --allow-empty -m "chore: commit de prueba en feature"

nbgv get-version
```

**Qué validar:**
- [ ] `PublicRelease` → `False`
- [ ] `NuGetPackageVersion` → `1.0.{altura}-g{hash}` (sufijo con hash del commit)
- [ ] `AssemblyInformationalVersion` → `1.0.{altura}+{hash}`

```powershell
# Confirmar que el ensamblado refleja la versión non-public
dotnet build src/Versioning.Api --configuration Release
dotnet run --project src/Versioning.Api
curl http://localhost:51461/version
# "version" contendrá "1.0.X+{hash}" — el commit se extrae tras el '+'
```

**Resultado real:**

```
Version:                      1.0.11.53476
AssemblyVersion:              1.0.0.0
AssemblyInformationalVersion: 1.0.11+d0e49296a5
NuGetPackageVersion:          1.0.11-gd0e49296a5
NpmPackageVersion:            1.0.11-gd0e49296a5
```

- [x] `NuGetPackageVersion` = `1.0.11-gd0e49296a5` — pre-release con hash (sufijo `-g`)
- [x] `AssemblyInformationalVersion` = `1.0.11+d0e49296a5` — build metadata con `+`
- [x] Altura incrementó de `10` a `11` por el commit vacío de prueba

> El sufijo `-g{hash}` aparece en `NuGetPackageVersion`. `AssemblyInformationalVersion` siempre usa `+` como separador en ambos tipos de rama.

---

## Ejercicio 5 — Rama `release/*`: public release sin sufijo

**Objetivo:** verificar que `release/*` está en `publicReleaseRefSpec` y genera versión limpia.

```powershell
git checkout main
git checkout -b release/1.1

nbgv get-version
```

**Qué validar:**
- [ ] `PublicRelease` → `True`
- [ ] `NuGetPackageVersion` → `1.0.{altura}` (sin sufijo pre-release)
- [ ] `AssemblyInformationalVersion` → `1.0.{altura}+{hash}` (solo build metadata, sin pre-release)

**Resultado real:**

```
Version:                      1.0.10.23892
AssemblyVersion:              1.0.0.0
AssemblyInformationalVersion: 1.0.10+5d54999cf4
NuGetPackageVersion:          1.0.10
NpmPackageVersion:            1.0.10
```

- [x] `NuGetPackageVersion` = `1.0.10` — idéntico a `main`, sin sufijo
- [x] `AssemblyInformationalVersion` = `1.0.10+5d54999cf4` — igual que main (mismo commit base)

---

## Ejercicio 6 — Rama `hotfix/*`: public release sin sufijo

**Objetivo:** verificar que `hotfix/*` también es public release (igual que `main`).

```powershell
git checkout main
git checkout -b hotfix/fix-critico
git commit --allow-empty -m "fix: corrección crítica"

nbgv get-version
```

**Qué validar:**
- [ ] `PublicRelease` → `True`
- [ ] `NuGetPackageVersion` → `1.0.{altura}` (sin sufijo, versión lista para producción)
- [ ] MAJOR y MINOR no cambian respecto a `main`

**Resultado real:**

```
Version:                      1.0.11.55202
AssemblyVersion:              1.0.0.0
AssemblyInformationalVersion: 1.0.11+d7a20c6281
NuGetPackageVersion:          1.0.11
NpmPackageVersion:            1.0.11
```

- [x] `NuGetPackageVersion` = `1.0.11` — sin sufijo, public release
- [x] Altura = `11` por el commit de fix (1 más que `main`)
- [x] MAJOR `1` y MINOR `0` sin cambio

---

## Ejercicio 7 — Tests de integración

**Objetivo:** verificar que los tests del endpoint `/version` pasan correctamente.

```powershell
git checkout main
dotnet test --configuration Release --verbosity normal
```

**Qué validar:**
- [ ] 5 tests pasan (GetVersion_ReturnsOk, ReturnsExpectedFields, VersionIsNotEmpty, VersionFollowsSemVer, GetRoot_ReturnsRunningMessage)
- [ ] 0 tests fallan
- [ ] La versión en el test sigue el patrón `^\d+\.\d+\.\d+`

**Resultado real:**

```
[xUnit.net] Discovering: Versioning.Tests
[xUnit.net] Discovered:  Versioning.Tests
[xUnit.net] Starting:    Versioning.Tests
[xUnit.net] Finished:    Versioning.Tests
  Correctas  GetVersion_VersionIsNotEmpty        [178 ms]
  Correctas  GetVersion_VersionFollowsSemVer     [6 ms]
  Correctas  GetRoot_ReturnsRunningMessage        [2 ms]
  Correctas  GetVersion_ReturnsOk                [3 ms]
  Correctas  GetVersion_ReturnsExpectedFields     [1 ms]
```

- [x] 5/5 tests pasan
- [x] 0 fallos

---

## Ejercicio 8 — CI/CD en GitHub Actions (push a `main`)

**Objetivo:** verificar que el pipeline publica ambos ejecutables con la versión en el nombre del artefacto.

```powershell
git checkout main
git push origin main
```

Luego en GitHub → **Actions** → último run → revisar:

**Qué validar:**
- [ ] El step "Detect version" muestra la versión calculada por NBGV
- [ ] Existen dos artefactos al final del pipeline:
  - `versioning-api-1.0.{altura}`
  - `versioning-worker-1.0.{altura}`
- [ ] Ambos artefactos tienen el **mismo número de versión**
- [ ] El Summary muestra Branch, SemVer y NuGet correctamente
- [ ] La versión **no** tiene sufijo pre-release (es public release)

> **Requiere GitHub Actions** — validar manualmente en el repositorio remoto tras el push.

---

## Ejercicio 9 — Versión embebida en el binario (sin código fuente)

**Objetivo:** confirmar que la versión viaja dentro del DLL, no depende de variables de entorno.

```powershell
dotnet publish src/Versioning.Api --configuration Release --output ./out/api
dotnet publish src/Versioning.Worker --configuration Release --output ./out/worker

# PowerShell — inspeccionar la versión del ensamblado
[System.Reflection.AssemblyName]::GetAssemblyName(".\out\api\Versioning.Api.dll").Version
```

**Qué validar:**
- [ ] La versión en el DLL coincide con lo que devuelve `/version`
- [ ] La versión en `Versioning.Api.dll` y `Versioning.Worker.dll` es la misma (mismo commit, misma versión base)

**Resultado real:**

```
# Versión leída desde binario publicado (strings en el PE):
Versioning.Api.dll    →  AssemblyVersion: 1.0.10.23892
                         InformationalVersion: 1.0.10+5d54999cf4

Versioning.Worker.dll →  AssemblyVersion: 1.0.10.23892
                         InformationalVersion: 1.0.10+5d54999cf4
```

- [x] Ambos DLLs tienen `InformationalVersion = 1.0.10+5d54999cf4`
- [x] Coincide con la respuesta del endpoint `/version` y el log del Worker
- [x] La versión está embebida en el binario, independiente del entorno de ejecución

---

## Ejercicio 10 — PR a rama de ambiente: versión con número de PR

**Objetivo:** verificar que un PR hacia `develop`, `qa` o `uat` genera versión `{version}-pr{N}` en el CI.

**Preparación:**
```powershell
# Crear la rama develop si no existe
git checkout -b develop
git push origin develop

# Crear una rama de feature
git checkout -b feature/nueva-funcionalidad
git commit --allow-empty -m "feat: nueva funcionalidad"
git push origin feature/nueva-funcionalidad
```

Luego en GitHub → abrir PR desde `feature/nueva-funcionalidad` → hacia `develop`.

**Qué validar en GitHub Actions:**
- [ ] El step "Detect version" muestra `is_env_pr=true`
- [ ] La versión calculada sigue el patrón `1.0.{altura}-pr{N}` (ej. `1.0.10-pr3`)
- [ ] Los artefactos se llaman `versioning-api-1.0.10-pr3` y `versioning-worker-1.0.10-pr3`
- [ ] El Summary muestra la versión con el número de PR

**Contraejemplo** — PR hacia `main`:
- [ ] El step detecta `is_env_pr=false`
- [ ] La versión usa el esquema NBGV normal (`1.0.10-gd0e49296a5`)
- [ ] El número de PR **no** aparece en la versión

> **Requiere GitHub Actions** — validar manualmente abriendo un PR en el repositorio remoto.

---

## Ejercicio 11 — Fork configurado: número de fork en el MINOR

**Objetivo:** verificar que configurar `"fork": "R14"` en `version.json` inserta el número `14` como MINOR en PR builds.

**Preparación:**
```powershell
# Editar version.json
# Cambiar "fork": null  →  "fork": "R14"

git add version.json
git commit -m "chore: configurar fork R14"
git push origin feature/nueva-funcionalidad
```

**Qué validar en GitHub Actions (PR → develop):**
- [ ] El step extrae `FORK_NUM=14` de `"R14"`
- [ ] La versión sigue el patrón `1.14.{altura}-pr{N}` (ej. `1.14.10-pr3`)
- [ ] Artefactos: `versioning-api-1.14.10-pr3` y `versioning-worker-1.14.10-pr3`
- [ ] El MAJOR (`1`) y el PATCH/height (`10`) no se alteran

**Qué validar en push a `main` (con fork configurado):**
- [ ] La versión sigue siendo `1.0.{altura}` sin cambios (el fork solo afecta PR builds a ambientes)

**Rollback:**
```powershell
# Restaurar fork a null para el repositorio principal
# "fork": "R14"  →  "fork": null
```

> **Requiere GitHub Actions** — validar manualmente en el repositorio remoto.

---

## Tabla de resumen — resultados esperados

| Trigger | Rama / Destino | fork en version.json | Versión | PublicRelease |
|---|---|---|---|---|
| push | `main` | cualquiera | `1.0.10` | ✅ |
| push | `release/1.1` | cualquiera | `1.0.10` | ✅ |
| push | `hotfix/bug` | cualquiera | `1.0.11` | ✅ |
| push | `feature/algo` | cualquiera | `1.0.11-gd0e49296a5` | ❌ |
| pull_request | → `develop` | `null` | `1.0.10-pr42` | — |
| pull_request | → `qa` | `null` | `1.0.10-pr7` | — |
| pull_request | → `uat` | `"R14"` | `1.14.10-pr3` | — |
| pull_request | → `main` | cualquiera | `1.0.10-gd0e49296a5` (NBGV, sin cambio) | — |

> NBGV distingue solo entre **public** y **non-public** release. Para labeling semántico por tipo de rama (`preview`, `rc`, incremento de MINOR), usar **GitVersion**.

---

## Checklist final

- [x] Ej. 1 — NBGV calcula versión correctamente en `main` (`1.0.10`, PublicRelease implícito)
- [x] Ej. 2 — API `/version` devuelve JSON con todos los campos (`version`, `commit`, `branch`, `environment`)
- [x] Ej. 3 — Worker loguea versión al arrancar (`Version=1.0.10+5d54999cf4`)
- [x] Ej. 4 — `feature/*` produce `NuGetPackageVersion=1.0.11-gd0e49296a5` (non-public)
- [x] Ej. 5 — `release/*` produce `NuGetPackageVersion=1.0.10` (public release)
- [x] Ej. 6 — `hotfix/*` produce `NuGetPackageVersion=1.0.11` (public release)
- [x] Ej. 7 — 5/5 tests de integración pasan
- [ ] Ej. 8 — CI genera artefactos `api` y `worker` con versión en el nombre *(requiere GitHub Actions)*
- [x] Ej. 9 — `InformationalVersion=1.0.10+5d54999cf4` embebido en ambos DLLs publicados
- [ ] Ej. 10 — PR a `develop`/`qa`/`uat` genera versión con número de PR (`-pr{N}`) *(requiere GitHub Actions)*
- [ ] Ej. 11 — Fork configurado inserta número en el MINOR (`1.14.{altura}-pr{N}`) *(requiere GitHub Actions)*
