using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Vms.Api.Extensions;

namespace Vms.Api.Hubs;

[Authorize(Policy = AppPolicies.OperatorOrAdministrator)]
public sealed class CommandCenterHub : Hub;
