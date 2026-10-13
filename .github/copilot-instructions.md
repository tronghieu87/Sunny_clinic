# Copilot Instructions for Clinic

Follow this project standard for all design and frontend work.

## Core product and user model
- Product: Enterprise SaaS / software lifecycle and asset management dashboard.
- Users: engineers, QA, PMs, DevOps, IT admins.
- Goal: minimal, data-focused, high-density but clean experience with low cognitive load.

## Layout and spacing rules
- Use 8px spacing scale only: 4, 8, 12, 16, 24, 32, 48, 64.
- Sidebar: 240px default, 64px collapsed.
- Header: fixed 64px height with 24px horizontal padding.
- Main content: 24px outer page padding.
- Cards: 16px or 24px internal padding; 16px or 24px gaps.
- Radius: 8px for cards/modals/drawers; 4px for inputs/tags/small buttons.
- Tables: 12px vertical, 16px horizontal padding; row height around 40-44px compact.
- Icon button spacing: 8px.

## Typography rules
- H1: 24px / 600
- H2: 18px / 600
- Body / table data: 14px / 400-500
- Caption / tag / badge: 12px / 400

## Responsive behavior
- Desktop: sidebar + content + optional right drawer.
- Tablet: collapse sidebar to icons; reduce table columns; allow horizontal scroll.
- Mobile: hide sidebar; use stacked cards instead of dense tables; filter menus become a single button opening a bottom sheet or modal.

## Required feature output format
For every design or feature request, provide:
1. User flow
2. Wireframe structure
3. Detailed technical specs
4. UI states
5. Responsive solution

## Design principles that must always be respected
1. Consistent 8px spacing system and strict layout rhythm.
2. Dense but clear data-first design with low cognitive load.
3. Responsive behavior across desktop, tablet, and mobile without losing usability.

Do not introduce decorative, bloated, consumer-style UI patterns. Keep enterprise clarity and functional speed as the top priorities.

## Response and delivery protocol
- Keep project responses concise, direct, and non-repetitive. Do not add greetings or explain theory unless requested.
- Before coding, read `PROGRESS.md` and its latest dated entry, then inspect only the relevant source path and nearby tests. Preserve the established architecture and scope the work to the current request.
- Before any frontend, screen, wireframe, or UI/UX change, read and follow the repository's `UI_DESIGN.md` as well as these instructions. Treat its HD viewport, control sizing, sparse/dense states, overflow, and accessibility checks as required.
- For each coding task, validate the changed behavior after build. Run the narrowest relevant automated tests when available; otherwise give 2-3 concrete manual checkpoints.
- Use these headings in project-work responses: `[Mục tiêu]`, `[Code mới/Sửa đổi]`, `[Kịch bản Test]`, `[Lưu ý nhanh]`, `[Nhật ký công việc]`. Keep each section brief; summarize changes instead of reproducing large files.
- End every project-work response with a short changelog table using columns `Tính năng đã làm`, `File bị tác động`, `Trạng thái`, `Ghi chú kỹ thuật ngắn`. After completed code work, append the same essential facts, verification, and remaining limitations to `PROGRESS.md`.
- Never record secrets or sensitive patient data in responses or the work log.

## Work log requirement
- Before starting a task, read `PROGRESS.md` and its latest dated entry for project state and recent decisions.
- After a feature or meaningful fix, append a concise dated summary to `PROGRESS.md`, including changed behavior/files, checks run, and remaining setup or limitations.
- Do not record passwords, tokens, SMTP credentials, patient data, or other secrets. Use the checkpoint to avoid rediscovering context from the full source tree.
