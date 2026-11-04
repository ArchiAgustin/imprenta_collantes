# Analítica · ficha de actividad

- Primary target: `ProyectoFinal/Views/Analytics/Index.cshtml`
- Mode: Operate
- Audience: Admin que revisa ventas, costos y taller.
- Task: Filtrar periodo, entender ventas con IGV, distinguir utilidad parcial y localizar productos o stock que requieren atención.

## Direction contract

**THESIS:** Una ficha de actividad verificable, no una vitrina de cifras ni un tablero de tarjetas iguales. La primera cifra siempre se puede reconciliar con su subtotal e IGV.

**OWN-WORLD:** Heredar las fichas claras, reglas finas, tinta oscura y barra lateral existente; reservar el acento para acciones y estados, no para decorar datos.

**STORY:** Filtrar por confirmación, leer venta con impuestos, comprender cobertura de costos, luego pasar a evolución, cotizaciones, productos, clientes y cola de taller. Los costos ausentes nunca se infieren.

**FIRST VIEWPORT:** Encabezado con rango editable arriba; debajo, cifra amplia de ventas con IGV a la izquierda y desglose subtotal/IGV y cobertura del costo a la derecha. Un aviso directo explica cómo se calculará la utilidad.

**FORM:** Extensión precisa del panel operativo existente; tabla de desglose, serie semanal/mensual accesible y rankings con acumulado Pareto. Sin imágenes ni cifras de demostración.

**FINISH:** Rutas verificadas para administrador y operador, CSS responsivo a 992/576 px y casos vacíos cubiertos por pruebas HTTP. El panel autenticado se renderizó mediante HTTP sobre una base temporal SQL Server migrada, y la migración se aplicó con respaldo previo a la base local. No hay raster ni fuente externa de datos. No se realizó captura en navegador; la vista mantiene la identidad descrita en `DESIGN.md` y no exhibe utilidad total cuando faltan costos.
