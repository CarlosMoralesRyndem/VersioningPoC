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
- `NuGetPackageVersion` → sin sufijo de pre-release
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

**Respuesta esperada:**
```json
{
  "version": "1.0.X-gABCDEF",
  "environment": "Development",
  "branch": "local",
  "commit": "ABCDEF",
  "buildDate": "..."
}
```

**Qué validar:**
- [ ] El campo `version` no está vacío
- [ ] Sigue el patrón `MAJOR.MINOR.PATCH`
- [ ] El campo `commit` coincide con `git rev-parse --short HEAD`
- [ ] `environment` es `Development` en local

---

## Ejercicio 3 — Worker: log de versión al arranque

**Objetivo:** verificar que el Worker Service loguea la versión al iniciar.

```powershell
dotnet run --project src/Versioning.Worker
```

**Salida esperada en consola:**
```
info: VersionLoggerService[0]
      Worker started | Version=1.0.X-gABCDEF | Branch=local | Commit=ABCDEF | Environment=Development
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
- [ ] `AssemblyInformationalVersion` → `1.0.{altura}+{hash}`

```powershell
# Confirmar que el ensamblado refleja la versión non-public
dotnet build src/Versioning.Api --configuration Release
dotnet run --project src/Versioning.Api
curl http://localhost:5000/version
# "version" debe contener "+{hash}" (build metadata del commit)
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
- [ ] `NuGetPackageVersion` → `1.0.{altura}` (sin sufijo)
- [ ] `AssemblyInformationalVersion` → `1.0.{altura}+{hash}` (metadata pero sin pre-release)

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

## Ejercicio 8 — CI/CD en GitHub Actions

**Objetivo:** verificar que el pipeline publica ambos ejecutables con la versión en el nombre del artefacto.

```powershell
git checkout main
git push origin main
```

Luego en GitHub → **Actions** → último run → revisar:

**Qué validar:**
- [ ] El step "Detect version" muestra la versión calculada por NBGV
- [ ] Existen dos artefactos al final del pipeline:
  - `versioning-api-{version}`
  - `versioning-worker-{version}`
- [ ] Ambos artefactos tienen el **mismo número de versión**
- [ ] El Summary muestra Branch, SemVer y NuGet correctamente

---

## Ejercicio 9 — Versión embebida en el binario (sin código fuente)

**Objetivo:** confirmar que la versión viaja dentro del DLL, no depende de variables de entorno.

```powershell
dotnet publish src/Versioning.Api --configuration Release --output ./out/api
dotnet publish src/Versioning.Worker --configuration Release --output ./out/worker

# Inspeccionar la versión del ensamblado
dotnet-ildasm ./out/api/Versioning.Api.dll 2>/dev/null | grep -i version
# O con PowerShell:
[System.Reflection.AssemblyName]::GetAssemblyName(".\out\api\Versioning.Api.dll").Version
```

**Qué validar:**
- [ ] La versión en el DLL coincide con lo que devuelve `/version`
- [ ] La versión en `Versioning.Api.dll` y `Versioning.Worker.dll` es la misma (mismo commit, misma versión base)

---

## Tabla de resumen — resultados esperados por rama

| Rama | NuGetPackageVersion | PublicRelease |
|---|---|---|
| `main` | `1.0.15` | ✅ |
| `release/1.1` | `1.0.15` | ✅ |
| `hotfix/bug` | `1.0.15` | ✅ |
| `feature/algo` | `1.0.15-g1a2b3c4` | ❌ |
| cualquier otra | `1.0.15-g1a2b3c4` | ❌ |

> NBGV distingue solo entre **public** y **non-public** release. Para labeling semántico por tipo de rama (`preview`, `rc`, incremento de MINOR), usar **GitVersion**.

---

## Checklist final

- [ ] Ej. 1 — NBGV calcula versión correctamente en `main`
- [ ] Ej. 2 — API `/version` devuelve JSON con todos los campos
- [ ] Ej. 3 — Worker loguea versión al arrancar
- [ ] Ej. 4 — `feature/*` produce versión `preview` con MINOR incrementado
- [ ] Ej. 5 — `release/*` produce versión `rc`
- [ ] Ej. 6 — `hotfix/*` produce patch sin sufijo
- [ ] Ej. 7 — 5/5 tests de integración pasan
- [ ] Ej. 8 — CI genera artefactos `api` y `worker` con versión en el nombre
- [ ] Ej. 9 — Versión está embebida en el DLL publicado
