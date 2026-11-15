# PROMPT DE CONTINUACIÓN — ENTREGABLE 1 (pegar en OpenCode)

> Completa los datos del equipo antes de pegarlo. Todo lo que está debajo de la línea va al agente.

---

Gracias por la FASE 0 y la FASE 1. Están aprobadas, con estas **decisiones del equipo**:

## Decisiones tomadas (resuelven tus observaciones)

1. **Contradicción DRY:** tienes razón. **E1 = solo CR-01 (SRP).** CR-02 (DRY), CR-03 (OCP + IoC, incluye P3 y P4) y CR-04 son del Entregable 2. Ignora la frase de la FASE 1 que pedía SRP y DRY en E1.
2. **CR-01:** extraer las reglas puras de formato y normalización del documento (DNI/RUC) de `IdentificacionService` a una clase nueva sin dependencias de EF ni de MVC. Nombre propuesto: `ValidadorDocumentoIdentidad` (en `ProyectoFinal/Services/`). Si propones otro nombre, justifícalo y avísame. No cambies mensajes, resultados, rutas ni el esquema de BD.
3. **CR-04 (perfectivo, E2):** soportar un nuevo tipo de documento, **carné de extranjería (CE)**, implementado como una estrategia más tras CR-03. Así se demuestra el OCP. Solo regístralo ahora (estado «Registrada»); no lo implementes.
4. **Groq:** la clave editable por el administrador queda **fuera** del alcance académico. No la mezcles con ningún CR del curso ni con sus versiones.
5. **Línea base v1.0.0:** antes de etiquetar, muéstrame la lista de cambios sin confirmar (`git status` y un resumen de `git diff --stat`) y espera mi decisión. La línea base será un commit real del sistema funcionando **antes de cualquier cambio de E1**, con `dotnet build` y `dotnet test` en verde. No etiquetes nada sin mi autorización.
6. **Remoto:** yo creo el repositorio en GitHub y te paso la URL. Luego me das los comandos para: agregar el remoto, renombrar `master` a `main`, crear `develop`, subir ambas ramas y la etiqueta `v1.0.0`.

## Datos del equipo

- Integrantes (somos **5**; Apellidos, Nombres — usuario de GitHub — 20 % cada uno):
  1. CHIGNE LINARES, Joe Daniel — GitHub: `________`
  2. COLLANTES CORREA, Peter Anthony — GitHub: `________`
  3. PERALTA GARCIA, Hugo Alexander — GitHub: `________`
  4. PORTILLA CHUNGA, Marco Antonio — GitHub: `________`
  5. ULLOA RUIZ, Archi Agustin — GitHub: `________`
- URL del repositorio en GitHub: `________`
- IDE usado: `________`

## Qué hacer ahora (pide confirmación antes de cada paso que modifique código)

Continúa con las **FASES 2 a 5** del prompt original, aplicando las decisiones de arriba:

- **FASE 2:** `.gitignore` (agrega `.env` y verifica que no haya claves de Groq ni de SMTP en archivos versionados; si las hay, avísame, porque habría que rotarlas). También `CONTRIBUTING.md`, `CHANGELOG.md`, `docs/configuracion/inventario-ci.md`, plantillas de Issue y PR, `.github/workflows/ci.yml` (`dotnet restore`, `build` y `test` de `ProyectoFinal.sln` con .NET 8). Actualiza el `README.md` sin borrar lo que ya tiene.
- **FASE 3:** `docs/cambios/CR-01.md` a `CR-04.md` y el texto para crear cada Issue en GitHub.
- **FASE 4:** pruebas de caracterización (5 a 10) de `IdentificacionService` en `ProyectoFinal.Tests`: DNI y RUC válidos e inválidos, duplicado por tipo y documento, dirección y distrito obligatorios con RUC, ubigeo opcional con DNI, distrito inexistente, y diferencia entre cliente y proveedor. Deben pasar **sin tocar el código de producción**. Después aplica CR-01 en pasos pequeños y ejecuta `dotnet test ProyectoFinal.sln` tras cada paso. Guarda la salida **antes y después** del refactor.
- **Commits:** dame la lista de commits sugeridos (Conventional Commits con `(CR-01)` cuando aplique) **repartidos entre los 5 integrantes**, para que cada uno los haga desde su cuenta. Usa este reparto: Chigne Linares, arquitectura y diagnóstico; Collantes Correa, repositorio, línea base y ramas; Peralta Garcia, pruebas de caracterización; Portilla Chunga, refactor CR-01; Ulloa Ruiz, convenciones, plantillas y CI. Para el E2: CR-02 Ulloa Ruiz; CR-03 Chigne Linares y Peralta Garcia; CR-04 Collantes Correa y Portilla Chunga. Cada PR lo revisa un integrante distinto del autor. Todos deben tener al menos un commit en el E1. No hagas commits a nombre de otros.
- **FASE 5:** genera `docs/entregables/REPORTE_E1.md` con las secciones 0 a 9 del prompt original, pero **enfocado en lo que falta del informe**:
  - Sección 7 completa con datos reales: URL, enlaces a la etiqueta, al Issue, a la rama y al PR de CR-01, y a la release v1.0.1; tabla `hash | autor | mensaje`; archivos modificados; número de pruebas aprobadas antes y después (con el comando y una salida resumida); y un fragmento antes/después de máximo 15 líneas cada uno.
  - Sección 8: la lista exacta de capturas para las Figuras 3 (historial y ramas en GitHub), 4 (`dotnet test` o GitHub Actions en verde) y 5 (PR de CR-01 con su revisión).
  - Si algo de las secciones 1 a 6 cambió respecto a tu respuesta anterior (líneas, nombres de clases, responsables), indícalo en una lista «Correcciones al informe».
  - Sección 9: pendientes para el E2 (CR-02, CR-03, CR-04 y la release v1.1.0).

Empieza por el punto 5 (estado del árbol de trabajo para la línea base) y espera mi respuesta.
