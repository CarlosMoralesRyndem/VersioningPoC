# Ejercicios de validación — VersioningPoC

Estos ejercicios cubren los escenarios clave de la implementación. Ejecútalos en orden; cada uno construye sobre el anterior.

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
- `NuGetPackageVersion` → sin sufijo de pre-release (ej. `1.0.15`)
- `PublicRelease` → `True`
- `CommitId` → hash del commit actual

> La "altura" es el número de commits desde la versión base definida en `version.json`.

---

## Ejercicio 2 — API: endpoint `/version`

**Objetivo:** verificar que la API expone la versión embebida en el ensamblado.

```powershell
dotnet run --project src/Versioning.Api

# En otra terminal:
curl http://localhost:5000/version
```

**Respuesta esperada (en `main`, local):**
```json
{
  "version": "1.0.15+g1a2b3c4",
  "environment": "Development",
  "branch": "local",
  "commit": "1a2b3c4",
  "buildDate": "..."
}
```

**Qué validar:**
- [ ] El campo `version` no está vacío
- [ ] Sigue el patrón `MAJOR.MINOR.PATCH`
- [ ] El campo `commit` coincide con `git rev-parse --short HEAD`
- [ ] `environment` es `Development` en local

> En `main` la versión lleva `+g{hash}` como build metadata (public release). El campo `commit` se extrae del sufijo `+g`.

---

## Ejercicio 3 — Worker: log de versión al arranque

**Objetivo:** verificar que el Worker Service loguea la versión al iniciar.

```powershell
dotnet run --project src/Versioning.Worker
```

**Salida esperada en consola:**
```
info: VersionLoggerService[0]
      Worker started | Version=1.0.15+g1a2b3c4 | Branch=local | Commit=1a2b3c4 | Environment=Development
```

**Qué validar:**
- [ ] Aparece el log con los 4 campos (Version, Branch, Commit, Environment)
- [ ] El hash de `Commit` coincide con `git rev-parse --short HEAD`
- [ ] El proceso termina limpio (es un BackgroundService de ciclo corto en este PoC)

---

## Ejercicio 4 — Rama `feature/*`: non-public release con hash

**Objetivo:** verificar que ramas fuera de `publicReleaseRefSpec` generan versión con sufijo `-g{hash}`.

```powershell
git checkout -b feature/mi-nueva-funcionalidad
git commit --allow-empty -m "chore: commit de prueba en feature"

nbgv get-version
```

**Qué validar:**
- [ ] `PublicRelease` → `False`
- [ ] `NuGetPackageVersion` → `1.0.{altura}-g{hash}` (sufijo con hash del commit)
- [ ] `AssemblyInformationalVersion` → contiene `-g{hash}`

```powershell
# Confirmar que el ensamblado refleja la versión non-public
dotnet build src/Versioning.Api --configuration Release
dotnet run --project src/Versioning.Api
curl http://localhost:5000/version
# "version" debe contener "-g{hash}" (pre-release del commit)
```

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
- [ ] `AssemblyInformationalVersion` → `1.0.{altura}+g{hash}` (solo build metadata, sin pre-release)

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
- [ ] La versión calculada sigue el patrón `1.0.{altura}-pr{N}` (ej. `1.0.15-pr3`)
- [ ] Los artefactos se llaman `versioning-api-1.0.15-pr3` y `versioning-worker-1.0.15-pr3`
- [ ] El Summary muestra la versión con el número de PR

**Contraejemplo** — PR hacia `main`:
- [ ] El step detecta `is_env_pr=false`
- [ ] La versión usa el esquema NBGV normal (`1.0.15-g{hash}`)
- [ ] El número de PR **no** aparece en la versión

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
- [ ] La versión sigue el patrón `1.14.{altura}-pr{N}` (ej. `1.14.15-pr3`)
- [ ] Artefactos: `versioning-api-1.14.15-pr3` y `versioning-worker-1.14.15-pr3`
- [ ] El MAJOR (`1`) y el PATCH/height (`15`) no se alteran

**Qué validar en push a `main` (con fork configurado):**
- [ ] La versión sigue siendo `1.0.{altura}` sin cambios (el fork solo afecta PR builds a ambientes)

**Rollback:**
```powershell
# Restaurar fork a null para el repositorio principal
# "fork": "R14"  →  "fork": null
```

---

## Tabla de resumen — resultados esperados

| Trigger | Rama / Destino | fork en version.json | Versión | PublicRelease |
|---|---|---|---|---|
| push | `main` | cualquiera | `1.0.15` | ✅ |
| push | `release/1.1` | cualquiera | `1.0.15` | ✅ |
| push | `hotfix/bug` | cualquiera | `1.0.15` | ✅ |
| push | `feature/algo` | cualquiera | `1.0.15-g1a2b3c4` | ❌ |
| pull_request | → `develop` | `null` | `1.0.15-pr42` | — |
| pull_request | → `qa` | `null` | `1.0.15-pr7` | — |
| pull_request | → `uat` | `"R14"` | `1.14.15-pr3` | — |
| pull_request | → `main` | cualquiera | `1.0.15-g1a2b3c4` | — |

> NBGV distingue solo entre **public** y **non-public** release. Para labeling semántico por tipo de rama (`preview`, `rc`, incremento de MINOR), usar **GitVersion**.

---

## Checklist final

- [ ] Ej. 1 — NBGV calcula versión correctamente en `main` (PublicRelease=True, sin sufijo)
- [ ] Ej. 2 — API `/version` devuelve JSON con todos los campos
- [ ] Ej. 3 — Worker loguea versión al arrancar
- [ ] Ej. 4 — `feature/*` produce versión non-public con `-g{hash}`
- [ ] Ej. 5 — `release/*` produce versión limpia (public release)
- [ ] Ej. 6 — `hotfix/*` produce versión limpia (public release)
- [ ] Ej. 7 — 5/5 tests de integración pasan
- [ ] Ej. 8 — CI genera artefactos `api` y `worker` con versión en el nombre
- [ ] Ej. 9 — Versión está embebida en el DLL publicado
- [ ] Ej. 10 — PR a `develop`/`qa`/`uat` genera versión con número de PR (`-pr{N}`)
- [ ] Ej. 11 — Fork configurado inserta número en el MINOR (`1.14.{altura}-pr{N}`)
