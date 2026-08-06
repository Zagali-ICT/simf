using System.Runtime.CompilerServices;

// Infrastructure registers Application-internal use-case
// services (NotificationDispatcher, NotificationService, …) in its DI
// composition root. Each Application service stays `internal sealed`
// (it is not a public-API surface — the contract is the interface
// it implements); this hook gives the composition root the visibility
// it needs without leaking the impl type out of Application.
[assembly: InternalsVisibleTo("SIMF.Infrastructure")]
