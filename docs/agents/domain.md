# Domain Docs

Use this repository's domain documentation when exploring code or making domain and architecture decisions.

## Before exploring

Read `GLOSSARY.md` at the repository root, or `GLOSSARY-MAP.md` if it exists. Also read ADRs in `docs/adr/` that touch the area being changed. If these files do not exist, proceed silently; do not create placeholder documents just to satisfy this configuration.

## Layout

This is a single-context repository:

```text
/
├── GLOSSARY.md
├── docs/adr/
└── Assets/ and project source
```

The `/domain-modeling` skill creates `GLOSSARY.md` or ADRs lazily when domain terminology or decisions are actually resolved.

## Vocabulary and conflicts

Use the glossary's vocabulary in issue titles, proposals, refactors, and tests. If a required concept is absent, treat that as a signal to reconsider the terminology or record the gap for `/domain-modeling`.

If a proposed change contradicts an existing ADR, call out the conflict explicitly rather than silently overriding the decision.
