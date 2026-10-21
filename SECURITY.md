# Security policy

## Scope

The current development line is a local research prototype, not a hardened
network service or a medical device. The command listener binds to loopback
only, limits active clients and queued requests, bounds frame sizes and cancels
expired commands before dispatch. It has no authentication: any process able
to connect on the same host can issue commands.

Do not expose port 8765 to a network through a proxy or port-forward without
adding an appropriate authenticated boundary. No real servo output exists in
this phase.

## Reporting

Report vulnerabilities through GitHub's private vulnerability reporting when
available: [report a vulnerability](https://github.com/jzjzzzzzzz/PhysicsUpperBodyAvatar/security/advisories/new).
Do not put credentials, identifying data, private recordings or exploit details
in a public issue. For non-sensitive defects, use the normal issue template.

Maintainers review reports on a best-effort basis; there is no response-time or
long-term-support guarantee. Supported fixes target the current development
branch.
