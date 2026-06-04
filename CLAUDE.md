# AsyncNetcodeRpc — Project Instructions

## Runtime Overview

The `Runtime/` directory defines the core async RPC system built on top of Unity Netcode for GameObjects and UniTask.

### Key Types

**`AsyncRpcManager`** (`NetworkBehaviour`)
- Tracks in-flight async RPCs via two dictionaries keyed on `AsyncRpcToken`:
  - `_sourceDictionary`: `UniTaskCompletionSource<AsyncRpcResult>` — resolves the awaited task
  - `_tokenDictionary`: `CancellationTokenSource` — handles early cancellation
- `AwaitCompletion(token, cancellationToken)` — called on the **client** after sending a ServerRpc; returns a `UniTask<AsyncRpcResult>` that resolves when the server signals completion
- `SignalCompletedTask(token, result)` — called **internally on the server** to push the result back to the originating client via a targeted ClientRpc
- Cancellation is linked to both the caller-supplied token and the `NetworkObject` despawn lifecycle

**`AsyncRpcToken`** (`readonly record struct`, `INetworkSerializeByMemcpy`)
- `(ulong clientId, int tokenId)` — uniquely identifies an in-flight RPC call

**`AsyncRpcResult`** (`readonly record struct`, `INetworkSerializeByMemcpy`)
- `bool success` — placeholder for richer results in future
- Static singletons: `AsyncRpcResult.Success`, `AsyncRpcResult.Failure`

### Extension Methods (`AsyncRpcManagerExtensions`)

**`PerformAsyncRpcAction(manager, Action<AsyncRpcToken> rpcAction, CancellationToken)`**
- Client-side helper: creates a token, registers `AwaitCompletion`, then invokes the provided RPC action (which should send the ServerRpc with the token)
- Returns the `UniTask<AsyncRpcResult>` to await

**`PerformServerRpcAction(manager, AsyncRpcToken token, UniTask<AsyncRpcResult> serverAction)`**
- Server-side helper: wraps the async server work in an `AsyncRpcInstance` (via `using`) so that `SignalCompletedTask` is called automatically on dispose regardless of success or failure

### `AsyncRpcInstance` (IDisposable)
- Holds the manager + token; `Result` defaults to `AsyncRpcResult.Success`
- `Dispose()` calls `manager.SignalCompletedTask(token, Result)` — always signals the client, even on exception paths

## Usage Pattern

```csharp
// Client side
var result = await asyncRpcManager.PerformAsyncRpcAction(
    token => MyServerRpc(token),
    cancellationToken);

// Server side (inside the ServerRpc handler)
await asyncRpcManager.PerformServerRpcAction(token, DoWorkAsync());
```
