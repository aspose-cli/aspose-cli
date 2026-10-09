; Current analyzer diagnostics required by Roslyn's release-tracking validation

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
APCLI001 | Aspose.Cli.ProductDiscovery | Error | Duplicate product ids are rejected at compile time.
APCLI002 | Aspose.Cli.ProductDiscovery | Error | Product module entry types must satisfy the static module contract.
APCLI003 | Aspose.Cli.ProductSerialization | Error | Product JSON roots require source-generated metadata in a product-owned context.
APCLI004 | Aspose.Cli.ProductDiscovery | Error | Every product assembly must export exactly one generated product module identity.
APCLI005 | Aspose.Cli.ProductDiscovery | Error | Product build metadata requires a complete compiler-visible catalog identity.
APCLI006 | Aspose.Cli.ProductIsolation | Error | Aspose SDK types cannot cross public, Contracts, or Commands boundaries.
APCLI007 | Aspose.Cli.ProductIsolation | Error | Product module definitions cannot access files, process state, threads, or Aspose SDK initialization.
APCLI008 | Aspose.Cli.ProductIsolation | Error | Product option aliases must be static and cannot reuse host-reserved or command-template aliases.
APCLI009 | Aspose.Cli.ProductIsolation | Error | Product implementation layers cannot introduce reverse, lateral, or cross-product dependencies.
APCLI010 | Aspose.Cli.ProductIsolation | Error | Commands and Engine implementation types cannot be publicly visible.
APCLI011 | Aspose.Cli.ProductIsolation | Error | Product code cannot build or bind commands through the host command seam StandardOptions.
APCLI012 | Aspose.Cli.OperationContracts | Error | Operation records must be listed in their camelCase JSON context and describe every member with a supported type and a requirement or default.
APCLI013 | Aspose.Cli.ResultContracts | Error | Result records must describe every serialized member with a summary and a supported type, and publish each relative schema id once.
