/*
This document is the property of Oversight Technologies Ltd that reserves its rights document and to
the data / invention / content herein described.This document, including the fact of its existence, is not to be
disclosed, in whole or in part, to any other party, and it shall not be duplicated, used, or copied in any
form, without the express prior written permission of Oversight authorized person. Acceptance of this document
will be construed as acceptance of the foregoing conditions.
*/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;

#nullable enable
namespace AsyncNetcodeRpc.Runtime
{
    public class AsyncRpcManager : NetworkBehaviour
    {
        private readonly Dictionary<AsyncRpcToken, UniTaskCompletionSource<AsyncRpcResult>> _sourceDictionary = new();
        private readonly Dictionary<AsyncRpcToken, CancellationTokenSource> _tokenDictionary = new();

        private CancellationToken DespawnCancellationToken => _despawnCts?.Token ?? CancellationToken.None;
        private CancellationTokenSource? _despawnCts;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _despawnCts ??= CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
        }

        public override void OnNetworkDespawn()
        {
            _despawnCts?.Cancel();
            _despawnCts?.Dispose();
            _despawnCts = null;
            base.OnNetworkDespawn();
        }

        /// <summary>
        /// Await this function after sending out a server RPC to know if it's been completed
        /// </summary>
        /// <param name="serverToken"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public UniTask<AsyncRpcResult> AwaitCompletion(AsyncRpcToken serverToken,
            CancellationToken cancellationToken)
        {
            var completionSource = new UniTaskCompletionSource<AsyncRpcResult>();
            var cancelSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, DespawnCancellationToken);
            _sourceDictionary[serverToken] = completionSource;
            _tokenDictionary[serverToken] = cancelSource;
            cancelSource.Token.Register(() => EarlyCancel(serverToken));
            return completionSource.Task;
        }

        /// <summary>
        /// The server should execute this function when an awaited task completes
        /// </summary>
        /// <param name="token"></param>
        /// <param name="result"></param>
        internal void SignalCompletedTask(AsyncRpcToken token, AsyncRpcResult result)
        {
            var targetParams = RpcTarget.Single(token.clientId, RpcTargetUse.Temp);
            OnClientMessage_ClientRpc(token, result, targetParams);
        }

        private void EarlyCancel(AsyncRpcToken token)
        {
            if (_tokenDictionary.Remove(token, out var tSource))
            {
                tSource.Cancel();
                tSource.Dispose();
            }

            if (_sourceDictionary.Remove(token, out var uSource))
            {
                uSource.TrySetResult(new AsyncRpcResult(false));
            }
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void OnClientMessage_ClientRpc(AsyncRpcToken token, AsyncRpcResult result, RpcParams cParams)
        {
            if (_sourceDictionary.Remove(token, out var completionSource))
            {
                completionSource.TrySetResult(result);
            }

            if (_tokenDictionary.Remove(token, out var tSource))
            {
                tSource.Dispose();
            }
        }
    }

    public readonly record struct AsyncRpcToken(ulong clientId, int tokenId) : INetworkSerializeByMemcpy;

    //Placeholder to allow for more complete result objects in future
    public readonly record struct AsyncRpcResult(bool success) : INetworkSerializeByMemcpy
    {
        public static readonly AsyncRpcResult Success = new(true);
        public static readonly AsyncRpcResult Failure = new(false);
    }
}