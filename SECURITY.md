# Security Policy — Pilgrim Cleaner

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

---

## Security Principles

Pilgrim Cleaner was engineered from day one to eliminate the risks traditionally associated with aggressive system cleaners:

- **Non-Destructive Operations:** Pilgrim Cleaner prioritizes system stability over aggressive cleanup statistics.
- **Elevation Transparency:** Pilgrim Cleaner operates cleanly in standard user mode without requiring permanent administrator elevation. Operations requiring administrator privileges (such as DISM store cleanup) fail gracefully or request explicit UAC escalation.
- **Zero Third-Party Downloads:** The Driver Health module connects exclusively to native Microsoft Windows Update infrastructure via COM (`WUApiLib`). No executable binaries or driver packages are fetched from third-party servers.

---

## Reporting a Vulnerability

If you discover a security vulnerability or a potential risk in Pilgrim Cleaner:

1. **Please do NOT open a public GitHub issue.**
2. Send an email to the maintainers or open a **Private Vulnerability Report** directly on GitHub under the **Security $\rightarrow$ Advisories** tab of the repository.
3. Provide a clear description of the issue, affected versions, and steps to reproduce.
4. We will acknowledge receipt within 48 hours and work with you on a coordinated disclosure and patch release.
