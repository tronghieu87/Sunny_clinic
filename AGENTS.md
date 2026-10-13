# Clinic UI/UX & Frontend Architecture Standards

This project follows a strict enterprise UI/UX and frontend architecture standard. Any future design, wireframe, component implementation, feature request, or UI adjustment must comply with the rules below.

## Response and delivery protocol
- Be concise and direct; avoid greetings, repetition, and unsolicited theory.
- Before coding, read `PROGRESS.md` and its latest dated entry, then inspect the relevant code path and nearby tests only. Preserve existing architecture and address only the current request.
- Before any frontend, screen, wireframe, or UI/UX change, read and follow `UI_DESIGN.md` in addition to these project standards. Its HD viewport, sizing, sparse/dense state, overflow, and accessibility checks are required.
- After a build, run the narrowest relevant automated test. If none exists, provide 2-3 specific manual checkpoints.
- For project-work responses use `[Mục tiêu]`, `[Code mới/Sửa đổi]`, `[Kịch bản Test]`, `[Lưu ý nhanh]`, `[Nhật ký công việc]`; keep each section short and do not dump large source files.
- End each project-work response with a brief changelog table: `Tính năng đã làm | File bị tác động | Trạng thái | Ghi chú kỹ thuật ngắn`. After completed code work, record the changes, verification, and outstanding limitations in `PROGRESS.md`.
- Never put passwords, tokens, SMTP credentials, or patient data in responses or logs.

## Work log
- Before starting project work, read `PROGRESS.md` and its latest dated entry for current behavior, pending work, and recent decisions; do not read the whole source tree just to reconstruct this context.
- After completing a feature or meaningful fix, append a concise dated entry to `PROGRESS.md` with behavior, important file paths, verification performed, and any remaining setup or limitations.
- Never put passwords, tokens, SMTP credentials, patient data, or other secrets in the work log.

## 1) Project context
- Product type: Web Enterprise / B2B SaaS for software lifecycle and asset management.
- Target users: Software Engineers, QA/Testers, Project Managers, DevOps, IT Admins.
- Core principle: minimal, data-focused, high-density but clean, low cognitive load.

## 2) Layout system and spacing
Apply an 8px spacing scale: 4, 8, 12, 16, 24, 32, 48, 64.

- App shell:
  - Left sidebar: fixed 240px, collapsible to 64px when needed.
  - Header: fixed 64px height with 24px left/right padding.
  - Main content: outer page padding 24px on all sides.
- Content blocks:
  - Card internal padding: 16px or 24px depending on scale.
  - Card margin / block gap: fixed 16px or 24px.
  - Radius: 8px for cards, modals, drawers; 4px for form controls, tags, small buttons.
- Data table:
  - Cell padding: 12px vertical, 16px horizontal.
  - Row height: approximately 40px to 44px in compact mode.
  - Action icon gap: fixed 8px.

## 3) Typography
Use a system font stack based on Inter / Roboto / SF Pro.

- Page title (H1): 24px, line-height 1.4, weight 600.
- Section title (H2): 18px, line-height 1.4, weight 600.
- Body text / table data: 14px, line-height 1.5, weight 400 or 500.
- Caption / subtext / tag / badge: 12px, line-height 1.4, weight 400.

## 4) Responsive strategy
### Desktop (>=1200px)
- Keep all three zones visible: sidebar, main content, optional right drawer.
- Use full data table with 6-8 columns when required.

### Tablet / small laptop (768px - 1023px)
- Collapse sidebar to icon-only (64px) or hide behind a hamburger button.
- Reduce secondary table columns and keep: ID / Name, Status, Owner, Actions.
- Allow smooth horizontal scroll for dense tables when needed.

### Mobile (<768px)
- Hide sidebar completely and use a hamburger menu.
- Convert data tables into stacked card lists.
- Collapse filters into a single "Lọc" button that opens a bottom sheet or full-screen modal.

## 5) Mandatory output format for each requested feature
When a user requests a design or feature, the result must follow this structure:

1. User Flow / User interaction steps
2. Wireframe layout structure
3. Detailed technical specifications
4. UI States (loading, empty, error, hover, active)
5. Responsive solution for that feature

## 6) Design requirements for every implementation
- Prefer clean layout over decorative density.
- Generate business identifiers and codes on the server; do not ask users to enter or edit them. Official source codes may be imported as reference data and shown read-only.
- Make the active tenant explicit before tenant-scoped create, edit, or lookup operations. APIs must enforce the authenticated tenant scope and never trust client-supplied ownership.
- Global standardized reference catalogs are visible from every tenant and editable only by platform Super Admin; other accounts have read-only access.
- Prioritize data clarity and fast action execution.
- Keep the interface consistent with the 8px spacing scale.
- Use strong hierarchy, compact tables, and no unnecessary visual noise.
- Design for enterprise workflows, not consumer-first UI patterns.
- Respect accessibility, touch targets, and readable contrast.

## 7) Working rules for AI-assisted coding
- All generated components must match the spacing, radius, typography, and layout rules above.
- For tables, prefer compact row heights and consistent padding.
- For forms, keep inputs aligned, minimal, and clear.
- For dashboard or administration pages, maintain predictable content density.
- For mobile, always convert dense tables to a card-based layout.
- For desktop, use full-width structured layout with focused information hierarchy.

## 8) Summary of the three most important design principles
1. Consistent 8px spacing system and strict layout rhythm.
2. Dense but clear data-first design with low cognitive load.
3. Responsive behavior that preserves usability across desktop, tablet, and mobile.

This standard is mandatory for all future feature designs, UI implementations, wireframes, and coding work in this project.
