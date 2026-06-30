# Pruebas de Versionado — Resultados Reales

Validación de la estrategia de versionado con NBGV alineada al **Estándar de Gobernanza de Git y Estrategia de Integración para Release Management** de Ryndem Studio.

**Fecha de ejecución:** 2026-06-30  
**Repo:** [CarlosMoralesRyndem/VersioningPoC](https://github.com/CarlosMoralesRyndem/VersioningPoC)

---

## Configuración activa (`version.json`)

```json
{
  "version": "1.0",
  "publicReleaseRefSpec": [
    "^refs/heads/main$",
    "^refs/heads/hotfix/.*$",
    "^refs/heads/R\\d+-main$"
  ],
  "fork": null
}
```

**Regla NBGV:**
- Ramas en `publicReleaseRefSpec` → versión limpia `1.0.X` (public release).
- Resto de ramas → `1.0.X-g{hash}` (non-public, NuGetPackageVersion).
- Lógica custom en CI → `1.0.X-pr{N}` cuando el PR apunta a `dev`, `qa` o `uat`.

---

## Escenario 1 — push a `main` (public release)

| Campo | Valor |
|---|---|
| Trigger | `push` |
| Rama | `main` |
| fork en version.json | `null` |
| Versión esperada | `1.0.X` |
| Run CI | [28451459582](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28451459582) |
| NuGetPackageVersion | `1.0.19` |
| AssemblyInformationalVersion | `1.0.19+{hash}` |
| Resultado | PASS |

**Observación:** rama en `publicReleaseRefSpec` → versión limpia sin sufijo.

---

## Escenario 2 — push a `hotfix/*` (public release)

| Campo | Valor |
|---|---|
| Trigger | `push` |
| Rama | `hotfix/test-governance` |
| fork en version.json | `null` |
| Versión esperada | `1.0.X` |
| Run CI | [28451543142](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28451543142) |
| NuGetPackageVersion | `1.0.20` |
| AssemblyInformationalVersion | `1.0.20+{hash}` |
| Resultado | PASS |

**Observación:** `hotfix/*` está en `publicReleaseRefSpec` → versión limpia. Cumple flujo Hotfix L3 del estándar.

---

## Escenario 3 — push a `feature/*` (non-public)

| Campo | Valor |
|---|---|
| Trigger | `push` |
| Rama | `feature/test-governance` |
| fork en version.json | `null` |
| Versión esperada | `1.0.X-g{hash}` |
| Run CI | [28451608056](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28451608056) |
| NuGetPackageVersion | `1.0.20-g0f3ac5b846` |
| AssemblyInformationalVersion | `1.0.20+0f3ac5b846` |
| Resultado | PASS |

**Observación:** rama no está en `publicReleaseRefSpec` → NBGV agrega `-g{hash}` en NuGet.

---

## Escenario 4 — push a `debt/*` (non-public)

| Campo | Valor |
|---|---|
| Trigger | `push` |
| Rama | `debt/test-governance` |
| fork en version.json | `null` |
| Versión esperada | `1.0.X-g{hash}` |
| Run CI | [28451612895](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28451612895) |
| NuGetPackageVersion | `1.0.20-g6953d5b7c1` |
| AssemblyInformationalVersion | `1.0.20+6953d5b7c1` |
| Resultado | PASS |

**Observación:** `debt/*` (deuda técnica general) → non-public. Cumple flujo Deuda Técnica del estándar.

---

## Escenario 5 — push a `warranty/*` (non-public)

| Campo | Valor |
|---|---|
| Trigger | `push` |
| Rama | `warranty/test-governance` |
| fork en version.json | `null` |
| Versión esperada | `1.0.X-g{hash}` |
| Run CI | [28451619297](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28451619297) |
| NuGetPackageVersion | `1.0.20-gde30d20418` |
| AssemblyInformationalVersion | `1.0.20+de30d20418` |
| Resultado | PASS |

**Observación:** `warranty/*` (garantía post-liberación en fork) → non-public. Cumple flujo Garantía del estándar.

---

## Escenario 6 — PR hacia `dev` sin fork (alternativa custom)

| Campo | Valor |
|---|---|
| Trigger | `pull_request` |
| Rama origen | `feature/test-pr-dev` |
| Rama destino | `dev` |
| fork en version.json | `null` |
| Versión esperada | `1.0.X-pr3` |
| PR # | [#3](https://github.com/CarlosMoralesRyndem/VersioningPoC/pull/3) |
| Run CI | [28451731321](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28451731321) |
| NuGetPackageVersion | `1.0.21-pr3` |
| AssemblyInformationalVersion | `1.0.21-pr3` |
| Resultado | PASS |

**Observación:** lógica custom del CI detecta PR hacia `dev` y sustituye versión por `-pr{N}`. Permite trazar qué PR está desplegado en el ambiente.

---

## Escenario 7 — PR hacia `qa` sin fork (alternativa custom)

| Campo | Valor |
|---|---|
| Trigger | `pull_request` |
| Rama origen | `feature/test-pr-qa` |
| Rama destino | `qa` |
| fork en version.json | `null` |
| Versión esperada | `1.0.X-pr4` |
| PR # | [#4](https://github.com/CarlosMoralesRyndem/VersioningPoC/pull/4) |
| Run CI | [28451734425](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28451734425) |
| NuGetPackageVersion | `1.0.21-pr4` |
| AssemblyInformationalVersion | `1.0.21-pr4` |
| Resultado | PASS |

**Observación:** mismo mecanismo que Escenario 6, aplicado a ambiente `qa`.

---

## Escenario 8 — PR hacia `dev` con fork R14 (alternativa custom)

| Campo | Valor |
|---|---|
| Trigger | `pull_request` |
| Rama origen | `feature/test-pr-fork` |
| Rama destino | `dev` |
| fork en version.json | `"R14"` |
| Versión esperada | `1.14.X-pr5` |
| PR # | [#5](https://github.com/CarlosMoralesRyndem/VersioningPoC/pull/5) |
| Run CI | [28451860990](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28451860990) |
| NuGetPackageVersion | `1.14.21-pr5` |
| AssemblyInformationalVersion | `1.14.21-pr5` |
| Resultado | PASS |

**Observación:** el MINOR refleja el número de fork (`R14` → `14`). Permite identificar en qué fork está desplegado el PR desde la versión.

---

## Escenario 9 — push a `R14-main` (public release de fork)

| Campo | Valor |
|---|---|
| Trigger | `push` |
| Rama | `R14-main` |
| fork en version.json | `null` |
| Versión esperada | `1.0.X` |
| Run CI | [28451969214](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28451969214) |
| NuGetPackageVersion | `1.0.21` |
| AssemblyInformationalVersion | `1.0.21+{hash}` |
| Resultado | PASS |

**Observación:** `R14-main` cumple el patrón `R\d+-main` en `publicReleaseRefSpec` → versión limpia. Refleja que la rama de producción de un fork genera builds de release oficial.

> **Nota:** requirió agregar `R*-main` a los triggers de push del workflow (ausencia detectada durante esta prueba).

---

## Escenario 10 — PR hacia `main` (NBGV puro, sin override)

| Campo | Valor |
|---|---|
| Trigger | `pull_request` |
| Rama origen | `feature/test-pr-main` |
| Rama destino | `main` |
| fork en version.json | `null` |
| Versión esperada | `1.0.X-g{hash}` |
| PR # | [#6](https://github.com/CarlosMoralesRyndem/VersioningPoC/pull/6) |
| Run CI | [28452050635](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28452050635) |
| NuGetPackageVersion | `1.0.22-gf1cf55553b` |
| AssemblyInformationalVersion | `1.0.22+f1cf55553b` |
| Resultado | PASS |

**Observación:** PR hacia `main` no activa la lógica custom de `-pr{N}` — NBGV calcula versión non-public normal. Confirma que el override es exclusivo de ambientes `dev`/`qa`/`uat`.

---

## Resumen de resultados

| # | Trigger | Rama / Destino | fork | Versión real | Resultado |
|---|---|---|---|---|---|
| 1 | push | `main` | `null` | `1.0.19` | PASS |
| 2 | push | `hotfix/test-governance` | `null` | `1.0.20` | PASS |
| 3 | push | `feature/test-governance` | `null` | `1.0.20-g0f3ac5b846` | PASS |
| 4 | push | `debt/test-governance` | `null` | `1.0.20-g6953d5b7c1` | PASS |
| 5 | push | `warranty/test-governance` | `null` | `1.0.20-gde30d20418` | PASS |
| 6 | pull_request | → `dev` (sin fork) | `null` | `1.0.21-pr3` | PASS |
| 7 | pull_request | → `qa` (sin fork) | `null` | `1.0.21-pr4` | PASS |
| 8 | pull_request | → `dev` (fork R14) | `"R14"` | `1.14.21-pr5` | PASS |
| 9 | push | `R14-main` | `null` | `1.0.21` | PASS |
| 10 | pull_request | → `main` | `null` | `1.0.22-gf1cf55553b` | PASS |

**Total: 10/10 PASS**

---

## Validación de deploy en servidor (`http://poctest.runasp.net/version`)

Deploy automático habilitado vía WebDeploy en PRs hacia `dev`, `qa` y `uat`.

### Resultado validado — PR #3 → `dev`

```json
{
  "version": "1.0.28-pr3",
  "environment": "Production",
  "branch": "unknown",
  "commit": "unknown",
  "buildDate": "2026-06-30T15:29:05.6764025Z"
}
```

| Campo | Observación |
|---|---|
| `version` | Correcta: `1.0.28-pr3` refleja el número de PR |
| `branch` / `commit` | `"unknown"` esperado — hosting compartido sin acceso a git ni variables de GitHub |
| Run CI | [28454735266](https://github.com/CarlosMoralesRyndem/VersioningPoC/actions/runs/28454735266) |

---

## Hallazgos durante la ejecución

| Hallazgo | Acción tomada |
|---|---|
| `R*-main` no estaba en triggers de push del workflow | Agregado `R*-main` en `dotnet-ci.yml` |
| Rama `develop` usada en lugar de `dev` | Corregido a `dev` para alinearse al estándar |
| `release/*` en `publicReleaseRefSpec` sin respaldo en el estándar | Removido; reemplazado por `R\d+-main` |
| Job `deploy` no aparecía en runs de PR | La rama del PR debe tener el workflow actualizado — se resuelve mergeando `main` antes de abrir el PR |
| `ERROR_FILE_IN_USE` al deployar | Agregado `-enableRule:AppOffline` al comando msdeploy |
| `-p:AssemblyInformationalVersion` no sobreescribe la versión NBGV | NBGV ignora ese override; solución: escribir `ci-version.txt` en el publish output y leerlo en runtime |
