# Structural Layout Validation — Specification Corpus

**Reviewer:** Coordinator (Elephant context — not an independent Goldfish review)  
**Scope:** Full specification corpus (9 documents)  
**Date:** 2026-11-24  
**Note:** The egm-structural Goldfish reviewer failed with RESOURCE_EXHAUSTED (429). This analysis is performed in the coordinator's context. It is not claimed as an independent multi-agent review.

---

## 1. HEADING AND NUMBERING

| # | Document | Issue | Severity |
|---|----------|-------|----------|
| 1 | **README.md** | Two sections numbered "9" (lines 102 and 114). Section 9 "Documentation map" is immediately followed by section "9. Important known limitations." | 🟡 Major |
| 2 | **prd.md** | Section "3. Sasaran produk" contains the priority provider paragraph duplicated at lines 33-34 and again at lines 36-37. The numbered goals (1-6) restart after the duplicate paragraph. | 🟡 Major |
| 3 | **architecture.md** | Section "1. Architectural drivers" contains the priority provider paragraph duplicated at lines 9-10 and again at lines 12-13. | 🟡 Major |
| 4 | **prd.md** | REQ-007 is listed under §4.2 (Should) but no REQ is numbered between REQ-006 and REQ-008 in §4.1 (Must). The numbering gap from REQ-006 to REQ-008 in the Must table is intentional (REQ-007 is extensions, a Should), but could confuse a reader expecting contiguous IDs in §4.1. | 🟢 Nit |

## 2. CROSS-REFERENCE INTEGRITY

| # | Source | Reference | Target | Issue | Severity |
|---|--------|-----------|--------|-------|----------|
| 1 | README.md L47 | `cd Switchly-Browser` | Filesystem | No `Switchly-Browser` directory is defined anywhere. The solution is `Switchly.sln`. | 🟡 Major |
| 2 | All documents | Links to `./prd.md`, `./design.md`, etc. | Root files | ✅ All cross-document links target existing files. | — |
| 3 | architecture.md L133 | CEF docs URL | External | URL points to CEF 156.0 docs which may move; acceptable for a draft. | 🟢 Nit |
| 4 | architecture.md L133 | CEF docs URL for CefRequestContext | External (145.0) | References CEF 145.0 docs, while the candidate version is 156.x. Version mismatch in doc URL. | 🟡 Major |

## 3. ID SYSTEM AUDIT

### Complete ID inventory:

| ID Prefix | Range Used | Gaps | Document Owner |
|---|---|---|---|
| REQ | 001-018 | REQ-007 is in §4.2 not §4.1 | prd.md |
| AC | 001-018 | None | prd.md |
| TEST | 001-026 | None | testing.md |
| ADR | 001-010 | None | decisions.md |
| TASK | 001-043 | None; TASK-015A, 033A, 038A use letter suffixes | plan.md |
| FLOW | 001-005 | None | prd.md |
| UI | 001-013 | None | design.md |
| ENT | 001-012 | None | schema.md |
| NFR-PERF | 001-004 | None | prd.md |
| OPEN | 001-007 | None | decisions.md |
| M | 001-009 | None | plan.md |

**Findings:**
- 🟢 Nit — TASK IDs use letter suffixes (015A, 033A, 038A) which break the numeric sorting convention. Minor but could confuse automated tooling.
- ✅ No ID overlaps or duplicates found across documents.
- ✅ REQ↔AC numbering is 1:1 for 001-014; ACs 015-018 track additional Should-level REQs.

## 4. TABLE AND DIAGRAM ISSUES

| # | Document | Issue | Severity |
|---|----------|-------|----------|
| 1 | architecture.md | Mermaid flowchart (L42-61) is syntactically valid. | ✅ |
| 2 | schema.md | Mermaid ER diagram (L168-179) is syntactically valid. | ✅ |
| 3 | prd.md §8 | "Asumsi dan hal terbuka" table uses mixed Indonesian status labels (Asumsi, Dikonfirmasi, Terbuka, Dikonfirmasi sebagian) while decisions.md uses English (Proposed, Confirmed, Open). | 🟡 Major |

## 5. LANGUAGE CONSISTENCY

| # | Issue | Severity |
|---|-------|----------|
| 1 | prd.md, design.md, schema.md §9 heading, and architecture.md are primarily Indonesian with English technical terms. decisions.md, rules.md, testing.md, README.md, and AGENTS.md are primarily English. This is a consistent pattern (product/UX docs in Indonesian, engineering docs in English) but is never stated as a policy. | 🟡 Major |
| 2 | Status labels are inconsistent: prd.md uses "Terbuka," "Asumsi," "Dikonfirmasi"; decisions.md uses "Proposed," "Confirmed," "Open." These map 1:1 conceptually but use different languages. | 🟡 Major |
| 3 | prd.md §4.1/§4.2 requirements are in Indonesian; testing.md test cases referencing the same requirements are in English. Cross-referencing requires translation. | 🟢 Nit |

## 6. DUPLICATE CONTENT

| # | Location A | Location B | Content | Severity |
|---|------------|------------|---------|----------|
| 1 | prd.md L33-34 | prd.md L36-37 | Verbatim duplicate: "Layanan prioritas untuk uji kompatibilitas awal..." paragraph | 🟡 Major |
| 2 | architecture.md L9-10 | architecture.md L12-13 | Verbatim duplicate: "Priority provider compatibility targets..." paragraph | 🟡 Major |
| 3 | architecture.md §5.1 | rules.md §3 | Near-duplicate content about profile isolation rules, though rules.md adds enforcement language | 🟢 Nit (acceptable cross-reference reinforcement) |

---

## Summary Counts

| Severity | Count |
|----------|-------|
| 🔴 Blocker | 0 |
| 🟡 Major | 9 |
| 🟢 Nit | 5 |
