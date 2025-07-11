# Security Policy

## Supported Versions

We release patches for security vulnerabilities. Which versions are eligible for receiving such patches depends on the CVSS v3.0 Rating:

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |
| < 1.0   | :x:                |

## Reporting a Vulnerability

Please report (suspected) security vulnerabilities to **security@dotcompute.io**. You will receive a response from us within 48 hours. If the issue is confirmed, we will release a patch as soon as possible depending on complexity but historically within a few days.

Please include the following information in your report:

- Type of issue (e.g., buffer overflow, code injection, cross-site scripting, etc.)
- Full paths of source file(s) related to the manifestation of the issue
- The location of the affected source code (tag/branch/commit or direct URL)
- Any special configuration required to reproduce the issue
- Step-by-step instructions to reproduce the issue
- Proof-of-concept or exploit code (if possible)
- Impact of the issue, including how an attacker might exploit the issue

## Security Update Process

1. Security report received and acknowledged
2. Vulnerability confirmed and assessed
3. Fix developed and tested
4. Security advisory prepared
5. Patch released with advisory
6. Public disclosure (after patch available)

## Security Best Practices

When using DotCompute:

1. **Keep dependencies updated** - Regularly update to the latest version
2. **Validate inputs** - Always validate kernel parameters
3. **Memory safety** - Use bounds checking in debug builds
4. **Access control** - Implement proper authentication for compute services
5. **Monitor usage** - Use built-in telemetry to detect anomalies

## Known Security Considerations

### Native Code Execution
DotCompute compiles and executes native code. Ensure:
- Kernels come from trusted sources
- Input validation prevents buffer overflows
- Memory access is bounds-checked

### Resource Limits
Implement resource limits to prevent:
- Excessive memory allocation
- Infinite kernel execution
- GPU resource exhaustion

### Multi-tenancy
When sharing compute resources:
- Isolate user kernels
- Implement quotas
- Monitor resource usage
- Clean up resources properly

## Contact

For any security concerns, please email: security@dotcompute.io

For general questions, use GitHub Discussions.

Thank you for helping keep DotCompute and its users safe!