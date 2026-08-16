using System.Runtime.CompilerServices;

// Infrastructure registers Application-internal use-case
// services (NotificationDispatcher, NotificationService, …) in its DI
// composition root. Each Application service stays `internal sealed`
// (it is not a public-API surface — the contract is the interface
// it implements); this hook gives the composition root the visibility
// it needs without leaking the impl type out of Application.
[assembly: InternalsVisibleTo("SIMF.Infrastructure")]

// The Api test project resolves Application-internal seams from the DI container
// to assert on them directly (IFileStorageProvider, whose visibility is what makes
// "all file access goes through IFileService" a compile-time property rather than
// a convention).
[assembly: InternalsVisibleTo("SIMF.Api.Tests")]
