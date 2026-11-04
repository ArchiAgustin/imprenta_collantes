Necesito un REPORTE DE ESTADO REAL del sistema web de la Imprenta Collantes E.I.R.L.
(ASP.NET Core + SQL Server). Revisa el código fuente del repositorio y el historial de Git.
No inventes nada: si algo no existe o no puedes confirmarlo, escribe "NO ENCONTRADO".
Responde en español, en Markdown, con estas 6 secciones exactas:

### 1. Estado de las historias de usuario
Tabla con columnas: HU | Historia | Estado | % avance | Evidencia en el código | Ruta/URL de la pantalla
- Estado: TERMINADO (funciona de punta a punta), EN PROGRESO o NO INICIADO.
- Evidencia: controlador, vista, servicio o entidad que la implementa (nombre de archivo).
- Ruta/URL: la dirección para abrir la pantalla en el navegador (ej. /Clientes/Create).
Historias a evaluar:
HU01 Iniciar sesión
HU02 Gestionar usuarios y roles (Admin, Vendedor, Operador)
HU03 Consultar bitácora de auditoría
HU04 Registrar clientes con validación de DNI/RUC
HU05 Ver historial del cliente (cotizaciones y pedidos)
HU06 Registrar proveedores
HU07 Gestionar catálogo de productos e insumos con stock
HU08 Crear cotización con cálculo automático de IGV 18%
HU09 Prellenar cotización con IA a partir de un mensaje de texto
HU10 Convertir cotización aprobada en pedido (transacción con descuento de stock)
HU11 Registrar entradas de insumos y ver kárdex
HU12 Ver alertas de stock mínimo
HU13 Ver tablero de producción por estados (En Cola, En Impresión, Listo, Entregado)
HU14 Ver dashboard de BI con KPI (ventas, conversión, stock, entregas)
HU15 Exportar reportes a Excel o PDF
HU16 Generar cotización en PDF
HU17 Enviar cotización por correo
Si encuentras funcionalidades que no están en esta lista, agrégalas al final como HU18, HU19...

### 2. Pantallas existentes
Lista de todas las vistas o pantallas que existen hoy, con su ruta y si funcionan o dan error.

### 3. Historial de trabajo según Git
Ejecuta y resume:
  git log --pretty=format:"%h|%an|%ad|%s" --date=short
Agrupa los commits por AUTOR y por SEMANA:
  Semana 0 (antes del 21/10/2026), Semana 1 (21–27/10), Semana 2 (28/10–03/11),
  Semana 3 (04/11–10/11).
Para cada autor y semana: número de commits, fecha del primero y del último,
y qué módulos tocó. Indica si hay código heredado de un proyecto anterior (2025).

### 4. Base de datos
Lista de tablas/entidades del DbContext y migraciones aplicadas, con su fecha.

### 5. Pendientes y problemas conocidos
Errores, TODOs en el código, funciones incompletas o sin probar.

### 6. Tecnologías y versiones reales
Versión de .NET, EF Core, SQL Server, Bootstrap, Chart.js y librerías de PDF/Excel/IA
(revisa el .csproj, package.json y las librerías en wwwroot/lib).Necesito un REPORTE DE ESTADO REAL del sistema web de la Imprenta Collantes E.I.R.L.
(ASP.NET Core + SQL Server). Revisa el código fuente del repositorio y el historial de Git.
No inventes nada: si algo no existe o no puedes confirmarlo, escribe "NO ENCONTRADO".
Responde en español, en Markdown, con estas 6 secciones exactas:

### 1. Estado de las historias de usuario
Tabla con columnas: HU | Historia | Estado | % avance | Evidencia en el código | Ruta/URL de la pantalla
- Estado: TERMINADO (funciona de punta a punta), EN PROGRESO o NO INICIADO.
- Evidencia: controlador, vista, servicio o entidad que la implementa (nombre de archivo).
- Ruta/URL: la dirección para abrir la pantalla en el navegador (ej. /Clientes/Create).
Historias a evaluar:
HU01 Iniciar sesión
HU02 Gestionar usuarios y roles (Admin, Vendedor, Operador)
HU03 Consultar bitácora de auditoría
HU04 Registrar clientes con validación de DNI/RUC
HU05 Ver historial del cliente (cotizaciones y pedidos)
HU06 Registrar proveedores
HU07 Gestionar catálogo de productos e insumos con stock
HU08 Crear cotización con cálculo automático de IGV 18%
HU09 Prellenar cotización con IA a partir de un mensaje de texto
HU10 Convertir cotización aprobada en pedido (transacción con descuento de stock)
HU11 Registrar entradas de insumos y ver kárdex
HU12 Ver alertas de stock mínimo
HU13 Ver tablero de producción por estados (En Cola, En Impresión, Listo, Entregado)
HU14 Ver dashboard de BI con KPI (ventas, conversión, stock, entregas)
HU15 Exportar reportes a Excel o PDF
HU16 Generar cotización en PDF
HU17 Enviar cotización por correo
Si encuentras funcionalidades que no están en esta lista, agrégalas al final como HU18, HU19...

### 2. Pantallas existentes
Lista de todas las vistas o pantallas que existen hoy, con su ruta y si funcionan o dan error.

### 3. Historial de trabajo según Git
Ejecuta y resume:
  git log --pretty=format:"%h|%an|%ad|%s" --date=short
Agrupa los commits por AUTOR y por SEMANA:
  Semana 0 (antes del 21/10/2026), Semana 1 (21–27/10), Semana 2 (28/10–03/11),
  Semana 3 (04/11–10/11).
Para cada autor y semana: número de commits, fecha del primero y del último,
y qué módulos tocó. Indica si hay código heredado de un proyecto anterior (2025).

### 4. Base de datos
Lista de tablas/entidades del DbContext y migraciones aplicadas, con su fecha.

### 5. Pendientes y problemas conocidos
Errores, TODOs en el código, funciones incompletas o sin probar.

### 6. Tecnologías y versiones reales
Versión de .NET, EF Core, SQL Server, Bootstrap, Chart.js y librerías de PDF/Excel/IA
(revisa el .csproj, package.json y las librerías en wwwroot/lib).