---
version: 1
slug: "proyectofinal-views-cotizacions-create-cshtml"
primary_target: "ProyectoFinal/Views/Cotizacions/Create.cshtml"
related_targets: ["ProyectoFinal/Views/Cotizacions/Edit.cshtml","ProyectoFinal/Views/Cotizacions/Details.cshtml","ProyectoFinal/Views/Cotizacions/Index.cshtml","ProyectoFinal/Views/Cotizacions/Delete.cshtml","ProyectoFinal/Views/Admin/Index.cshtml","ProyectoFinal/Views/Roles/Index.cshtml","ProyectoFinal/Areas/Identity/Pages/Shared/_PanelLayout.cshtml","ProyectoFinal/wwwroot/css/site.css"]
---

Scope: Extend the existing Imprenta Collantes back-office for complete productized quotations and distinct Users/Roles administration. Mode Operate. Administrators manage registered accounts; Admin is the only currently supported operational access role, while custom roles remain assignable categories. Their task is to compose auditable quotations from existing catalog products and manage access without losing role assignments.

## Direction contract
THESIS: A quotation is a working folio whose customer header and product lines stay visible together; it is not a single generic CRUD form or a manually typed grand total.
OWN-WORLD: Inherit the pinned Fichas de taller system: warm sheet, deep ink, mineral-teal primary controls, quiet rules, current section in the dark index. Keep data controls familiar, compact, keyboard-first, and avoid nested card decoration.
STORY: The admin selects a customer and validity in the left card, adds catalog products and agreed quantities/prices on the right, sees estimated subtotal/18% IGV/total update, then saves. The server independently validates and recomputes totals inside one transaction. Edit preloads existing lines and atomically replaces the persisted set with the validated form set; details show the saved breakdown. Delete is blocked while a Pedido references the quotation (Pedido management is not currently exposed). The panel presents Users and Roles as separate modules; the Users table exposes every assigned role and role changes add/remove individual assignments.
FIRST VIEWPORT: On desktop, one working sheet with a short page title and a two-column 5/7 split: quotation header card left, line editor right. The add-line action sits beside the detail heading; totals remain anchored below the lines and the save action is in the right card footer. At narrow widths the header card precedes detail; quantity and price stay side by side on the narrowest screens, with each line amount and the save action in the flow.
FORM: Existing two-card composition request, inherited from the pinned visual world; this is a surface extension, so no concept-seed was run. Admin lists use a dense accessible table with separate Users and Roles routes; consistent filled primary and outlined secondary buttons carry actions. Back/cancel links always point to a known module.
FINISH: unreviewed and undocumented is unfinished; this build ends with the finish review, the verdict, DESIGN.md, and every shipping raster carrying its provenance
