# Guía de contribución — Imprenta Collantes

Convenciones de gestión de configuración del equipo (curso Evolución y Configuración de Software, UPN).

## Ramas

| Rama | Uso |
|---|---|
| `main` | Solo versiones estables y etiquetadas (v1.0.0, v1.0.1…). No se trabaja directo aquí. |
| `develop` | Integración del trabajo en curso. Todas las ramas de trabajo salen de aquí. |
| `tipo/CR-XX-descripcion-corta` | Una rama por solicitud de cambio. |

Tipos de rama: `feature/` (nueva funcionalidad), `fix/` (corrección), `refactor/` (mejora interna sin cambiar el comportamiento), `test/` (pruebas), `docs/` (documentación) y `chore/` (configuración y mantenimiento).

Ejemplos: `chore/CR-00-convenciones`, `test/CR-01-pruebas-documento`, `refactor/CR-01-validador-documento`.

```bash
git switch develop
git pull --ff-only origin develop
git switch -c refactor/CR-01-validador-documento
```

## Commits

Formato [Conventional Commits](https://www.conventionalcommits.org/es/v1.0.0/):

```
tipo(ámbito): descripción en imperativo (CR-XX)
```

Tipos: `feat`, `fix`, `refactor`, `test`, `docs`, `chore` y `ci`.

Ejemplos:
- `test(identificacion): caracterizar DNI y RUC (CR-01)`
- `refactor(identificacion): separar validación de documentos (CR-01)`
- `chore(configuracion): documentar cambios y activar CI (CR-00)`

Cada integrante hace sus commits **desde su propia cuenta**.

## Flujo de trabajo

1. Registrar o revisar la ficha del cambio en `docs/cambios/CR-XX.md`.
2. Crear la rama desde `develop`.
3. Hacer commits pequeños y ejecutar `dotnet test ProyectoFinal.sln` antes de subir.
4. Abrir un **Pull Request hacia `develop`**. Debe revisarlo y aprobarlo **otro integrante**, y GitHub Actions debe quedar en verde.
5. Actualizar `CHANGELOG.md` y el estado de la ficha CR.
6. Para liberar una versión: PR de `develop` a `main`, etiqueta anotada y publicación.

## Versiones

Se usa [versionado semántico](https://semver.org/lang/es/) `MAJOR.MINOR.PATCH`:

| Versión | Contenido |
|---|---|
| v1.0.0 | Línea base: sistema antes de los cambios del curso. |
| v1.0.1 | Entregable 1: CR-00 y CR-01 (refactor sin cambio de comportamiento). |
| v1.1.0 | Entregable 2: CR-02, CR-03 y CR-04. |

```bash
git tag -a v1.0.1 -m "Entregable 1: CR-00 y CR-01"
git push origin v1.0.1
```

## Secretos

No se versionan claves ni contraseñas (Groq, SMTP, cadenas de conexión con contraseña). Usar User Secrets o variables de entorno.
