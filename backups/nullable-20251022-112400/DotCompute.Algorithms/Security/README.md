# DotCompute Security System

The DotCompute Security System provides comprehensive security validation for plugin assemblies, implementing multiple layers of protection to ensure safe plugin execution in .NET applications.

## Overview

The security system consists of several interconnected components that work together to provide defense-in-depth for plugin loading and execution:

1. **Digital Signature Validation** - Authenticode signature verification
2. **Strong Name Validation** - .NET strong name signature checking
3. **Malware Scanning** - Multi-method malware detection
4. **Security Policy Management** - Rule-based security evaluation
5. **Code Access Security (CAS)** - Permission set restrictions
6. **Assembly Metadata Analysis** - Suspicious pattern detection

## Components

### 1. AuthenticodeValidator

Validates Authenticode digital signatures on assemblies using Windows certificate APIs.

**Features:**
- Certificate chain validation
- Revocation checking
- Trusted publisher verification
- Batch validation support
- Certificate information extraction

**Usage:**
```csharp
using var validator = new AuthenticodeValidator(logger);
var result = await validator.ValidateAsync(assemblyPath);

if (result.IsValid && result.TrustLevel >= TrustLevel.Medium)
{
    // Assembly is properly signed and trusted
    Console.WriteLine($"Signed by: {result.SignerName}");
    Console.WriteLine($"Trust Level: {result.TrustLevel}");
}
```

### 2. MalwareScanningService

Multi-method malware detection system with pluggable scanning engines.

**Scanning Methods:**
- **Hash-based Detection** - Known malware hash comparison
- **Pattern-based Scanning** - Suspicious string pattern matching
- **Windows Defender Integration** - Native antivirus scanning
- **Behavioral Analysis** - File characteristic analysis
- **External Scanner Integration** - Third-party antivirus support

**Usage:**
```csharp
var options = new MalwareScanningOptions
{
    EnableWindowsDefender = true,
    EnablePatternMatching = true,
    SuspiciousPatternThreshold = 5
};

using var scanner = new MalwareScanningService(logger, options);
var result = await scanner.ScanAssemblyAsync(assemblyPath);

if (!result.IsClean)
{
    Console.WriteLine($"Threat detected: {result.ThreatDescription}");
    Console.WriteLine($"Risk level: {result.ThreatLevel}");
}
```

### 3. SecurityPolicy

Centralized security policy management with configurable rules and evaluation.

**Features:**
- Rule-based security evaluation
- Trusted publisher management
- Directory-based policies
- Security level enforcement
- JSON configuration serialization

**Built-in Security Rules:**
- **FileSizeSecurityRule** - Assembly size validation
- **DigitalSignatureSecurityRule** - Signature verification
- **StrongNameSecurityRule** - Strong name validation
- **MetadataAnalysisSecurityRule** - Suspicious pattern detection
- **DirectoryPolicySecurityRule** - Location-based policies
- **BlocklistSecurityRule** - Assembly blocklist checking

**Usage:**
```csharp
var policy = new SecurityPolicy(logger);
policy.RequireDigitalSignature = true;
policy.RequireStrongName = true;
policy.MinimumSecurityLevel = SecurityLevel.High;

// Add trusted publishers
policy.AddTrustedPublisher("1234567890ABCDEF...");

// Configure directory policies
policy.DirectoryPolicies["/trusted/plugins"] = SecurityLevel.High;

// Evaluate assembly
var context = new SecurityEvaluationContext
{
    AssemblyPath = assemblyPath,
    AssemblyBytes = File.ReadAllBytes(assemblyPath)
};

var result = policy.EvaluateRules(context);
```

### 4. CodeAccessSecurityManager

Implements Code Access Security (CAS) permission restrictions for plugin assemblies.

**Features:**
- Security zone-based permissions
- File system access restrictions
- Network access controls
- Reflection limitations
- Resource usage limits

**Security Zones:**
- **MyComputer** - Full trust permissions
- **Intranet** - High trust with some restrictions
- **Internet** - Limited permissions for web-based content
- **Untrusted** - Minimal execution permissions only

**Usage:**
```csharp
var options = new CodeAccessSecurityOptions
{
    DefaultSecurityZone = SecurityZone.Internet,
    EnableFileSystemRestrictions = true,
    EnableNetworkRestrictions = true
};

using var casManager = new CodeAccessSecurityManager(logger, options);

// Create restricted permission set
var permissionSet = casManager.CreateRestrictedPermissionSet(
    assemblyPath, SecurityZone.Internet);

// Check if operation is permitted
var allowed = casManager.IsOperationPermitted(
    assemblyPath, SecurityOperation.FileRead, "/temp/data.txt");
```

### 5. Security Rules

Extensible security rule system for custom validation logic.

**Custom Rule Example:**
```csharp
public class CustomSecurityRule : SecurityRule
{
    public override SecurityEvaluationResult Evaluate(SecurityEvaluationContext context)
    {
        var result = new SecurityEvaluationResult();
        
        // Custom validation logic
        if (Path.GetFileName(context.AssemblyPath).Contains("unsafe"))
        {
            result.IsAllowed = false;
            result.Violations.Add("Assembly name contains 'unsafe'");
        }
        
        return result;
    }
}

// Register custom rule
securityPolicy.AddSecurityRule("CustomRule", new CustomSecurityRule());
```

## Integration with Plugin Manager

The security system is integrated into the `AlgorithmPluginManager` and `PluginLoader` classes:

```csharp
var options = new AlgorithmPluginManagerOptions
{
    EnableSecurityValidation = true,
    RequireDigitalSignature = true,
    RequireStrongName = true,
    EnableMalwareScanning = true,
    MinimumSecurityLevel = SecurityLevel.High,
    MaxAssemblySize = 50 * 1024 * 1024 // 50 MB
};

// Add trusted publishers
options.TrustedPublishers.Add("Microsoft Corporation");

using var pluginManager = new AlgorithmPluginManager(accelerator, logger, options);

// Security validation is automatically performed during plugin loading
var loadedCount = await pluginManager.DiscoverAndLoadPluginsAsync(pluginDirectory);
```

## Configuration

### Security Policy Configuration (JSON)

```json
{
  "RequireDigitalSignature": true,
  "RequireStrongName": true,
  "MinimumSecurityLevel": "High",
  "EnableMetadataAnalysis": true,
  "EnableMalwareScanning": true,
  "MaxAssemblySize": 52428800,
  
  "TrustedPublishers": [
    "1234567890ABCDEF1234567890ABCDEF12345678"
  ],
  
  "DirectoryPolicies": {
    "/trusted/plugins": "High",
    "/system/plugins": "Critical",
    "/temp": "Low"
  }
}
```

### Code Access Security Configuration

```json
{
  "DefaultSecurityZone": "Internet",
  "EnableFileSystemRestrictions": true,
  "EnableNetworkRestrictions": true,
  "EnableReflectionRestrictions": true,
  "MaxMemoryUsage": 268435456,
  "MaxExecutionTime": "00:05:00",
  
  "AllowedFileSystemPaths": [
    "/app/data",
    "/app/temp"
  ],
  
  "AllowedNetworkEndpoints": [
    "https://api.example.com"
  ]
}
```

## Security Validation Process

When a plugin assembly is loaded, the following validation steps are performed:

1. **File System Validation**
   - File existence check
   - Size validation
   - Directory policy enforcement

2. **Digital Signature Validation**
   - Authenticode signature verification
   - Certificate chain validation
   - Trusted publisher checking
   - Revocation status verification

3. **Strong Name Validation**
   - Public key extraction
   - Signature integrity verification
   - Key size validation

4. **Malware Scanning**
   - Hash-based detection
   - Pattern matching
   - Behavioral analysis
   - External scanner integration

5. **Metadata Analysis**
   - Suspicious namespace detection
   - Dangerous type identification
   - Unsafe code pattern analysis
   - Dynamic code generation detection

6. **Security Policy Evaluation**
   - Rule-based validation
   - Security level determination
   - Violation reporting

7. **Permission Restriction**
   - CAS permission set creation
   - Security zone assignment
   - Resource limit enforcement

## Best Practices

### For Production Deployment

1. **Always enable signature validation** for production environments
2. **Use trusted publishers** and maintain an up-to-date certificate store
3. **Configure appropriate security zones** based on assembly source
4. **Enable comprehensive malware scanning** with multiple detection methods
5. **Implement directory-based policies** to restrict plugin locations
6. **Regular security policy updates** to address new threats
7. **Monitor security events** and maintain audit logs

### For Development Environment

1. **Use separate security policies** for development vs. production
2. **Allow self-signed certificates** only in development
3. **Configure verbose logging** for debugging security issues
4. **Test security validation** with both valid and invalid assemblies
5. **Maintain test certificates** for development plugin signing

### Performance Considerations

1. **Cache validation results** for frequently loaded assemblies
2. **Use batch validation** for multiple assemblies
3. **Configure appropriate timeouts** for scanning operations
4. **Limit concurrent scans** to prevent resource exhaustion
5. **Optimize hash databases** for faster malware detection

## Error Handling

The security system provides comprehensive error handling and logging:

```csharp
try
{
    var result = await validator.ValidateAsync(assemblyPath);
    if (!result.IsValid)
    {
        logger.LogWarning("Security validation failed: {Error}", result.ErrorMessage);
        // Handle validation failure
    }
}
catch (SecurityException ex)
{
    logger.LogError(ex, "Security validation threw exception for: {Assembly}", assemblyPath);
    // Handle security exception
}
```

## Logging and Monitoring

The security system provides detailed logging at multiple levels:

- **Debug**: Detailed validation steps and internal operations
- **Information**: Successful validations and configuration changes
- **Warning**: Security policy violations and validation failures
- **Error**: System errors and critical security issues

## Extensibility

The security system is designed for extensibility:

1. **Custom Security Rules** - Implement `SecurityRule` base class
2. **Custom Scanners** - Extend `MalwareScanningService` 
3. **Custom Validators** - Create specialized validation components
4. **Policy Extensions** - Add new policy types and rules

## Dependencies

- .NET 9.0+
- System.Security.Cryptography.X509Certificates
- Microsoft.Extensions.Logging
- Microsoft.Extensions.DependencyInjection
- System.Text.Json

## Compatibility

- **Windows**: Full feature support including Windows Defender integration
- **Linux**: Core features supported (excluding Windows-specific APIs)
- **macOS**: Core features supported (excluding Windows-specific APIs)

## Known Limitations

1. **Code Access Security**: .NET Core/.NET 5+ has limited CAS support compared to .NET Framework
2. **Windows Defender**: Only available on Windows systems
3. **Certificate Store**: Some operations require Windows certificate stores
4. **Reflection Restrictions**: Limited enforcement in modern .NET compared to .NET Framework

For complete examples and usage scenarios, see the `SecurityValidationExample.cs` file.