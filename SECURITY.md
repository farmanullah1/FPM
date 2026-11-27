# Security policy

FPM performs privileged system maintenance, so security reports are taken seriously and handled
privately.

## Reporting a vulnerability

**Do not open a public issue.** Use GitHub **Security → Report a vulnerability** (private security
advisories) on this repository. If that is unavailable, contact the maintainer through the GitHub
profile <https://github.com/farmanullah1> and ask for a private channel.

Please include:

- FPM version (About page) and whether it is the installer or portable build.
- Windows version/build, architecture, and whether the elevated helper was involved.
- Reproduction steps and the smallest example that shows the problem.
- Impact: what data or system state could be affected. **Never include tokens, keys or personal
  file contents.**

## What to expect

- Acknowledgement within 7 days.
- An assessment and, where valid, a fix in a private branch; the advisory is published after a
  release containing the fix, with credit if you want it.
- Reporters acting in good faith will not be pursued for research on their own installation.

## Scope

In scope: unvalidated helper input, path traversal/junction escapes, approval replay or tampering,
privilege escalation, unsafe ADB command construction, arbitrary script or command execution,
telemetry or network calls the user did not start, secrets in logs or exports, and update/download
verification failures.

Out of scope: issues that require the user to run a modified build or to hand over administrator
rights to an untrusted party; the inherent risk of debloating a phone without a backup (documented
in the app); and Microsoft or Google product issues.

## Supported versions

Only the latest release receives security fixes. If you are on an older build, reproduce on the
latest before reporting when you can.
