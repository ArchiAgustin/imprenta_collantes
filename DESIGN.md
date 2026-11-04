---
name: Imprenta Collantes
description: Fichas de taller para gestión comercial
colors:
  paper: "#f5f3ec"
  sheet: "#fffef9"
  ink: "#18383b"
  subtle: "#425c5c"
  rule: "#cdd3c9"
  action: "#196369"
  action-hover: "#104b50"
  marker: "#a74a28"
typography:
  display:
    fontFamily: "Barlow Condensed, Arial Narrow, sans-serif"
    fontSize: "clamp(2.7rem, 5vw, 4.8rem)"
    fontWeight: 700
    lineHeight: 1.05
  body:
    fontFamily: "system-ui, Segoe UI, sans-serif"
    fontSize: "1rem"
    fontWeight: 400
rounded:
  control: ".25rem"
spacing:
  field: "1.15rem"
  panel: "clamp(1.2rem, 3.6vw, 3.6rem)"
components:
  button-primary:
    backgroundColor: "{colors.action}"
    textColor: "{colors.sheet}"
    rounded: "{rounded.control}"
    padding: ".57rem 1rem"
  button-primary-hover:
    backgroundColor: "{colors.action-hover}"
    textColor: "{colors.sheet}"
  module-item:
    backgroundColor: "{colors.sheet}"
    textColor: "{colors.ink}"
---

# Design System: Imprenta Collantes

## Overview

**Creative North Star: "Fichas de taller"**

An indexed working archive, not a marketing dashboard. Navigation identifies the current module; sheets give forms and records a quiet, repeatable work surface. The original imprint mark is at `ProyectoFinal/wwwroot/img/marca-collantes.svg`. Charts and operational claims must derive from real data, never placeholders.

**Key Characteristics:** visible module index, legible data, warm sheets against deep-ink navigation, one primary action per task.

## Colors

`paper` grounds public views; `sheet` is the working surface; `ink` supplies text and the navigation rail. `action` is reserved for task links and primary controls. `marker` is a restrained focus/error accent, not a second CTA color. See `ProyectoFinal/wwwroot/css/site.css` for the CSS custom properties.

## Typography

Self-hosted Barlow Condensed 500/700 supplies headings and the brand; body copy uses the system UI stack. Keep body text readable in tables and forms, with small labels reserved for index navigation.

## Layout

Desktop uses a 220–250px dark navigation rail and a flexible working sheet. Below 992px, navigation becomes a labeled collapse control; below 576px, module entries stack and wide tables scroll inside their container.

## Elevation & Depth

Work sheets use a quiet offset-and-blurred shadow; tables, module entries and controls use fine rules for hierarchy rather than nested cards.

## Shapes

Controls use small 0.2–0.25rem radii. The mark and the module index use geometric registration; avoid ornamental print textures behind data.

## Components

`_PanelLayout.cshtml` owns the module rail, active state and mobile menu. `_Layout.cshtml` owns the public/auth shell. `.module-list` and `.module-item` are action links, not statistics. Forms show inline validation and a clear way back; empty lists invite the first record.

The admin-only analytics sheet at `Views/Analytics/Index.cshtml` uses the same rules and restrained colors: one reconciled gross-sales figure (net + IGV), a visibly qualified cost-coverage section, then an accessible weekly/monthly bar list and tabular rankings. Date filters are close to the figures. Current stock and workshop states are explicitly separated from period-filtered sales. No fabricated values or decorative charts.

## Do's and Don'ts

- Do describe only implemented modules and indicate accounts awaiting permissions.
- Do use meaningful customer/provider names instead of IDs in UI choices.
- Don't present estimated profit when costs are missing as a complete or accounting-grade margin.
- Don't fabricate sales figures, inventory alerts or order states.
