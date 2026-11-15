# PROMPT PARA EL AGENTE DE IA — ENTREGABLE 1 (Semana 5)

> Copia todo lo que está debajo de la línea y pégalo en tu agente (Claude Code, Copilot, Cursor, etc.) abierto en la carpeta de tu sistema.
> Antes de pegarlo, completa los 4 datos de la sección "DATOS DEL EQUIPO".

---

## ROL Y CONTEXTO

Actúas como asistente de ingeniería de software de un equipo universitario del curso **Evolución y Configuración de Software** (UPN, docente Ms. Ronny Sánchez Vargas). Trabajas sobre el sistema de este repositorio. El proyecto **no** consiste en construir un sistema nuevo: consiste en **mejorar la mantenibilidad** del código existente y **organizar sus cambios y versiones** con Git y GitHub.

El proyecto tiene dos entregables acumulativos:
- **Entregable 1 (AHORA, semana 5):** análisis, diagnóstico, plan, gestión de cambios y un avance práctico inicial.
- **Entregable 2 (DESPUÉS, semana 8):** implementación completa de mejoras, registro de cambios, versión final. **NO lo hagas todavía**, pero deja todo preparado para continuar (ver sección "PREPARACIÓN PARA EL ENTREGABLE 2").

### DATOS DEL EQUIPO (completar)
- Integrantes (Apellidos, Nombres y usuario de GitHub): `________`
- URL del repositorio en GitHub (si ya existe): `________`
- Módulo que queremos mejorar (si ya lo sabemos; si no, propónlo tú): `________`
- Herramienta de IA usada (nombre): `________`

### REGLAS OBLIGATORIAS
1. **No cambies el comportamiento observable** del sistema al refactorizar. Primero pruebas, después refactorización, en pasos pequeños.
2. **No inventes datos.** Todo hallazgo debe tener evidencia real: ruta del archivo, clase, método y rango de líneas. Si algo no lo puedes verificar, dilo.
3. **No hagas commits a nombre de otros integrantes** ni falsifiques autores o fechas. Prepara el trabajo y la lista de commits sugeridos por integrante; cada uno debe hacer sus propios commits desde su cuenta (la participación se evalúa con el historial).
4. **No subas secretos** (cadenas de conexión con contraseña, API keys). Usa `.gitignore`, variables de entorno o un archivo de ejemplo (`appsettings.Example.json`, `.env.example`).
5. Explica cada sugerencia que hagas de forma que un estudiante pueda defenderla ante el docente.
6. Antes de cada paso que modifique código, muéstrame el plan y espera mi confirmación.
7. Responde en español.

---

## FASE 0 — Reconocimiento (ingeniería inversa)

Analiza el repositorio con **análisis estático** (estructura, clases, imports/using, dependencias, archivos de configuración, scripts de BD) y, si puedes ejecutarlo, **análisis dinámico** (correr la app y los flujos principales).

Genera `docs/arquitectura.md` con:
- Nombre del sistema, propósito, usuarios/roles y 4–6 funciones principales.
- Stack: lenguaje y versión, framework, base de datos, librerías clave, framework de pruebas (si existe), cómo se compila y ejecuta.
- Tabla de módulos/capas: `Módulo | Responsabilidad | Depende de | Archivos principales`.
- **Diagrama de módulos y dependencias en Mermaid** (`flowchart LR`), sencillo (máx. ~12 nodos), centrado en el módulo elegido.
- Un diagrama de secuencia en Mermaid del flujo principal del módulo elegido (desde la entrada —controlador/vista— hasta la BD).
- Reglas de negocio identificadas en el módulo.
- Si no hay módulo definido: recomienda **uno** (el que concentre más lógica de negocio y más problemas) y justifica.

## FASE 1 — Diagnóstico de mantenibilidad

Busca en el módulo elegido **3 a 5 problemas concretos** ("code smells") relacionados con los principios del curso:
- **SRP:** clases/métodos que mezclan reglas de negocio, acceso a datos, validación, presentación o notificaciones.
- **DRY:** la misma regla o conocimiento duplicado en varios lugares (no simple similitud de texto).
- **OCP:** cadenas de `if/else` o `switch` por tipo que obligan a modificar el mismo método por cada variante nueva.
- **IoC/DI:** clases que crean sus dependencias con `new` (conexiones, repositorios, servicios) y no se pueden sustituir en pruebas.
- Otros: métodos muy largos, nombres poco claros, ausencia de pruebas, configuración/secretos en el código.

Genera `docs/diagnostico.md` con una tabla:
`ID | Problema | Principio afectado | Ubicación exacta (archivo:líneas, clase, método) | Evidencia (fragmento breve o métrica: nº de líneas, nº de duplicados) | Impacto en el mantenimiento | Mejora propuesta | Entregable (E1 o E2)`.

Prioriza: un problema **SRP** y uno **DRY** para el Entregable 1 (bajo riesgo), y uno **OCP/IoC** para el Entregable 2.

## FASE 2 — Gestión de configuración (preparar el repositorio)

1. Si no existe repositorio Git, inicialízalo. Verifica `.gitignore` adecuado al stack (p. ej. `bin/`, `obj/`, `node_modules/`, `.vs/`, `.env`).
2. **Línea base:** asegúrate de que el código original (sin modificaciones de refactorización) quede en un commit identificado y propón la etiqueta anotada `v1.0.0` con mensaje "Línea base: código original antes de cambios". Dame los comandos exactos.
3. Ramas: `main` (solo versiones estables etiquetadas) y `develop` (integración). Ramas de trabajo: `tipo/CR-XX-descripcion-corta` (tipos: `feature`, `fix`, `refactor`, `test`, `docs`, `chore`).
4. Crea `CONTRIBUTING.md` con:
   - Convención de ramas (arriba).
   - **Conventional Commits** con CR: `tipo(alcance): descripción en imperativo (CR-XX)`. Tipos: feat, fix, refactor, test, docs, chore, ci. Con 3 ejemplos aplicados al sistema.
   - Flujo de Pull Request: toda integración a `develop`/`main` por PR, revisado por **otro integrante**, con pruebas aprobadas.
   - Versionado semántico: `v1.0.0` línea base, `v1.0.1` avance E1 (refactor sin cambio de comportamiento), `v1.1.0` versión final E2.
5. Crea `CHANGELOG.md` (formato Keep a Changelog) con las entradas `[1.0.0]` y `[Unreleased]`.
6. Crea `docs/configuracion/inventario-ci.md` con los elementos de configuración: `ID | Elemento | Archivos | Versión/estado | Responsable` → CI-01 código fuente, CI-02 archivos de proyecto/dependencias, CI-03 script o esquema de BD, CI-04 configuración (sin secretos), CI-05 pruebas automatizadas, CI-06 documentación y registros CR.
7. Crea plantillas de GitHub: `.github/ISSUE_TEMPLATE/solicitud-de-cambio.md` y `.github/pull_request_template.md` (con campos: CR asociado, qué cambia, cómo se probó, checklist).
8. Crea `.github/workflows/ci.yml` (GitHub Actions) que compile y ejecute las pruebas en cada push/PR a `develop` y `main`.
9. Crea/actualiza `README.md` con: descripción, requisitos, cómo clonar, configurar la BD, ejecutar la app y ejecutar las pruebas.

## FASE 3 — Gestión del cambio (registrar CR)

Crea `docs/cambios/` con un archivo por solicitud, usando esta plantilla:

```
# CR-XX — <título>
| Campo | Detalle |
|---|---|
| Código | CR-XX |
| Fecha | AAAA-MM-DD |
| Solicitante / rol | |
| Sistema y versión base | <sistema> v1.0.0 |
| Estado | Registrada / En análisis / Aprobada / Rechazada / En implementación / Verificada / Cerrada |
| Tipo de mantenimiento | Correctivo / Adaptativo / Perfectivo / Preventivo |
## Descripción y motivo
## Alcance (incluye / no incluye)
## Análisis de impacto
- Clases y archivos afectados:
- Base de datos (¿cambia el esquema? ¿por qué?):
- Compatibilidad (¿el comportamiento actual se mantiene?):
- Riesgos y mitigación:
- Esfuerzo estimado:
- Pruebas necesarias:
## Decisión (quién, cuándo, condiciones)
## Implementación (rama, commits, PR, CI modificados)
## Verificación (pruebas ejecutadas y resultados)
## Cierre (versión resultante, responsable)
```

Registra:
- **CR-01 (E1):** refactorización SRP del problema prioritario → se implementa ahora.
- **CR-02 (E2):** DRY → estado "En análisis".
- **CR-03 (E2):** OCP + inversión de control / inyección de dependencias → estado "En análisis".
- **CR-04 (E2):** una mejora funcional pequeña y realista (perfectivo) que aproveche el diseño refactorizado (p. ej. un filtro de búsqueda, un nuevo tipo/variante, una validación) → estado "Registrada".

Además, dame el texto listo para crear cada CR como **Issue en GitHub** (título, cuerpo, etiquetas).

## FASE 4 — Avance práctico (CR-01)

1. Rama: `test/CR-01-pruebas-caracterizacion` (o dentro de `refactor/CR-01-...`).
2. **Pruebas de caracterización** del módulo: si no existe proyecto de pruebas, créalo con el framework estándar del stack. Escribe 5–10 pruebas que fijen el comportamiento actual (caso normal, límites y error). Deben pasar **sobre la línea base sin modificar el código de producción**. Si necesitas una costura mínima para poder probar, explícala y justifícala.
3. Rama `refactor/CR-01-<descripcion>`: aplica la refactorización SRP en **pasos pequeños** (extraer clase/método, mover método). Después de cada paso, ejecuta las pruebas. **Las mismas pruebas deben seguir pasando sin modificarse.**
4. Muéstrame el **antes y después** (fragmentos breves) y explica por qué mejora la mantenibilidad.
5. Propón la secuencia de commits (con mensaje Conventional Commits) y **a qué integrante le corresponde cada uno**, el PR hacia `develop` (título y descripción), y los comandos para fusionar a `main` y etiquetar `v1.0.1`.
6. Actualiza `CHANGELOG.md` (sección `[1.0.1]`), el estado de CR-01 y `docs/configuracion/inventario-ci.md`.
7. Registra el uso de IA en `docs/ia/registro-uso-ia.md`: `Fecha | Tarea | Sugerencia de la IA | Decisión (aceptada/modificada/descartada) | Cómo se validó | Integrante que revisó`.

## FASE 5 — REPORTE PARA EL INFORME (lo más importante)

Genera `docs/entregables/REPORTE_E1.md` con **exactamente** estas secciones, usando datos reales del repositorio (no texto genérico). Este archivo se usará para completar el informe Word.

```
## 0. Datos generales
- Nombre del sistema / título sugerido del proyecto:
- URL del repositorio:
- Integrantes y usuario GitHub:

## 1. Descripción del proyecto
- Tipo de aplicación, propósito, usuarios/roles, funciones principales (4–6).

## 2. Alcance y tecnologías
- Lenguaje y versión, framework, BD, librerías, framework de pruebas, IDE.
- Módulo elegido y por qué.
- Incluye / no incluye.

## 3. Diagnóstico (tabla de 3 problemas para el informe)
| Problema | Ubicación / evidencia | Impacto | Mejora propuesta |
(P1 SRP, P2 DRY, P3 OCP/IoC; con archivo:líneas reales y enlaces a GitHub en el tag v1.0.0)

## 4. Propuesta de evolución
- Tipos de mantenimiento aplicados y su justificación.
- Qué se refactoriza y por qué; nombres reales de las clases nuevas/propuestas.
- Principios elegidos (SRP, DRY, OCP, IoC) aplicados a este código concreto.
- Diagrama Mermaid de módulos y dependencias (copiado de docs/arquitectura.md).
- Lista de módulos: módulo — responsabilidad — depende de.
- Uso de IA: tareas y cómo se validó.

## 5. Plan de mantenimiento
| Actividad | Tipo | Prioridad | Responsable | Criterio de éxito |

## 6. Gestión de cambios y versiones
- Proceso CR aplicado, convención de ramas y commits (con ejemplos reales), versiones/etiquetas.
- Cambio concreto CR-01 (resumen) y CR-02..CR-04 registrados.

## 7. Avance práctico (evidencias)
- Enlace al repo, al tag v1.0.0, a la rama/PR de CR-01, al Issue de CR-01, a la release v1.0.1.
- Lista de commits: hash corto | autor | mensaje.
- Archivos creados/modificados.
- Resultado de pruebas: comando ejecutado, nº de pruebas, aprobadas/fallidas (antes y después del refactor), salida resumida.
- Fragmento antes/después (máx. 15 líneas cada uno).

## 8. Capturas que debemos tomar
- Lista de capturas exactas (qué pantalla, qué comando, qué URL) para las Figuras 2, 3 y 4.

## 9. Pendientes para el Entregable 2
```

---

## PREPARACIÓN PARA EL ENTREGABLE 2 (no ejecutar ahora)

Deja todo listo para que, cuando te lo pida, puedas continuar con:
- Implementar **CR-02 (DRY)**, **CR-03 (OCP + IoC/DI)** y **CR-04 (perfectivo)**, cada uno con su rama, PR revisado, pruebas y actualización de su archivo CR hasta "Cerrada".
- Registrar inyección de dependencias en el punto de composición de la app y usar dobles de prueba (fakes/mocks).
- Liberar `v1.1.0` (merge a `main`, tag anotado, GitHub Release con notas, CHANGELOG).
- Generar `docs/entregables/REPORTE_E2.md` con: resumen y cambios respecto al E1; tabla `Problema inicial | Cambio implementado | Técnica/principio | Evidencia`; plan de mantenimiento final `Componente | Tipo | Actividad | Prioridad/responsable`; registro de cada CR (identificador, motivo e impacto, responsable/revisión, evidencia, validación y estado); gestión de versiones (ramas usadas, commits representativos, versión final, participación por integrante, instrucciones de ejecución); tabla de pruebas `Prueba | Esperado | Obtenido | Estado | Evidencia`; conclusiones y mejoras futuras.

Para facilitarlo, en `docs/entregables/REPORTE_E1.md` sección 9 deja la lista de pendientes, y mantén la estructura de carpetas `docs/arquitectura.md`, `docs/diagnostico.md`, `docs/cambios/`, `docs/configuracion/`, `docs/ia/`, `docs/entregables/`.

**Empieza por la FASE 0 y muéstrame el resultado antes de seguir.**
